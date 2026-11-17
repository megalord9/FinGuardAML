using FinGuardAML.Models;
using FinGuardAML.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinGuardAML.Controllers;

/// <summary>
/// Методы расчёта рисков и просмотра ранее обработанных транзакций.
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public sealed class RiskController : ControllerBase
{
    private readonly RiskScoringService _scoring;
    private readonly TransactionStore _store;

    public RiskController(RiskScoringService scoring, TransactionStore store)
    {
        _scoring = scoring;
        _store = store;
    }

    /// <summary>Оценить риск транзакции и сохранить результат.</summary>
    /// <remarks>
    /// В теле запроса передаются данные клиента, сумма, валюта, тип операции,
    /// страны сторон, данные контрагента и статистика за 30 дней.
    /// Сервис рассчитывает riskScore (0–100), категорию LOW/MEDIUM/HIGH,
    /// список сработавших правил и рекомендацию.
    /// </remarks>
    /// <param name="request">Данные учебной транзакции для оценки риска.</param>
    /// <param name="ct">Токен отмены HTTP-запроса.</param>
    /// <returns>Созданная оценка риска с идентификатором транзакции.</returns>
    /// <response code="201">Результат расчёта успешно создан.</response>
    /// <response code="400">Некорректные входные данные или неизвестная валюта.</response>
    /// <response code="503">Внешний сервис курсов валют недоступен и нет сохранённого курса.</response>
    [HttpPost("risk-score")]
    [ProducesResponseType(typeof(RiskAssessment), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RiskAssessment>> Calculate(
        [FromBody] TransactionRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _scoring.EvaluateAsync(request, ct);
            _store.Add(result);
            return CreatedAtAction(nameof(GetTransaction),
                new { id = result.TransactionId }, result);
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

    /// <summary>Получить список обработанных транзакций.</summary>
    /// <remarks>Пока результаты хранятся в памяти приложения и теряются после перезапуска.</remarks>
    /// <returns>Массив оценок риска, либо пустой массив.</returns>
    /// <response code="200">Список возвращён успешно.</response>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(IReadOnlyList<RiskAssessment>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<RiskAssessment>> GetTransactions() =>
        Ok(_store.GetAll());

    /// <summary>Получить сохранённую транзакцию по идентификатору GUID.</summary>
    /// <param name="id">Уникальный идентификатор транзакции (GUID).</param>
    /// <returns>Ранее рассчитанная оценка риска.</returns>
    /// <response code="200">Транзакция найдена.</response>
    /// <response code="404">Транзакция с указанным GUID не найдена.</response>
    [HttpGet("transactions/{id:guid}")]
    [ProducesResponseType(typeof(RiskAssessment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<RiskAssessment> GetTransaction(Guid id)
    {
        var found = _store.Find(id);
        return found is null ? NotFound() : Ok(found);
    }
}
