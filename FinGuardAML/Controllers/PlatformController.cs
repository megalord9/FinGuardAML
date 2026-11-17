using FinGuardAML.Models;
using FinGuardAML.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinGuardAML.Controllers;

/// <summary>Совместный анализ на основе FinGuard AML и дополнительного API.</summary>
[ApiController]
[Route("api/v1/platform")]
[Produces("application/json")]
public sealed class PlatformController : ControllerBase
{
    private readonly RiskScoringService _scoring;
    private readonly TransactionStore _store;
    private readonly PartnerAnalyticsService _partner;

    public PlatformController(RiskScoringService scoring, TransactionStore store,
        PartnerAnalyticsService partner)
    {
        _scoring = scoring;
        _store = store;
        _partner = partner;
    }

    /// <summary>Проверить доступность дополнительного API.</summary>
    /// <response code="200">Состояние интеграции возвращено.</response>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        return Ok(new
        {
            service = "FinGuard AML Platform",
            ownApi = "AVAILABLE",
            partnerApi = await _partner.GetStatusAsync(ct),
            notice = "Учебная платформа. Не использовать реальные персональные данные."
        });
    }

    /// <summary>Выполнить оценку операции и объединить её с аналитикой второго API.</summary>
    /// <remarks>
    /// Сначала FinGuard AML рассчитывает собственный riskScore. Затем, если
    /// настроен второй API, платформа отправляет ему статистику операции по HTTP POST.
    /// При недоступности второго API возвращается частичный результат без вымышленных баллов.
    /// </remarks>
    /// <param name="input">Данные учебной транзакции.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    /// <response code="200">Объединённый или частичный результат расчёта.</response>
    /// <response code="400">Ошибка формата или валидации транзакции.</response>
    /// <response code="503">Недоступен сервис курсов валют без сохранённого курса.</response>
    [HttpPost("analyze")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Analyze([FromBody] TransactionRequest input,
        CancellationToken ct)
    {
        try
        {
            var own = await _scoring.EvaluateAsync(input, ct);
            _store.Add(own);

            var partner = await _partner.AnalyzeAsync(new PartnerAnalyticsRequest(
                input.ClientId, own.AmountRub, input.OperationsLast30Days,
                input.TotalAmountLast30Days), ct);

            int combined = Math.Min(100, own.RiskScore + partner.AdditionalScore);
            string level = combined >= 60 ? "HIGH" : combined >= 30 ? "MEDIUM" : "LOW";
            string recommendation = own.ScreeningStatus is "NOT_CONFIGURED" or "CHECK_FAILED"
                ? "MANUAL_REVIEW: внешний скрининг не завершён"
                : combined >= 60 || partner.AdditionalScore > 0
                    ? "MANUAL_REVIEW: требуется дополнительная проверка"
                    : own.Recommendation;

            return Ok(new
            {
                integrationStatus = partner.Status == "OK" ? "COMBINED" : "PARTIAL",
                ownAssessment = own,
                partnerAnalytics = partner,
                combinedRiskScore = combined,
                combinedRiskLevel = level,
                combinedRecommendation = recommendation
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ExternalServiceException ex)
        {
            return StatusCode(503, new { error = ex.Message });
        }
    }
}
