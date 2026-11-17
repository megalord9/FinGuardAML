using FinGuardAML.Models;
using FinGuardAML.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinGuardAML.Controllers;

/// <summary>
/// Получение курсов валют на основе официального XML-сервиса Банка России.
/// </summary>
[ApiController]
[Route("api/v1/rates")]
[Produces("application/json")]
public sealed class CurrencyController : ControllerBase
{
    private readonly CbrCurrencyService _rates;

    public CurrencyController(CbrCurrencyService rates) => _rates = rates;

    /// <summary>Получить курс одной единицы валюты к российскому рублю.</summary>
    /// <remarks>Для RUB возвращается курс 1. Для иностранных валют используется XML-сервис ЦБ РФ. Курс может быть получен из кеша.</remarks>
    /// <param name="currency">Трёхбуквенный код валюты, например USD, EUR или RUB.</param>
    /// <param name="ct">Токен отмены HTTP-запроса.</param>
    /// <returns>Курс, дата, код валюты и признак устаревшего значения.</returns>
    /// <response code="200">Курс получен успешно.</response>
    /// <response code="400">Код валюты некорректен или отсутствует у ЦБ РФ.</response>
    /// <response code="503">ЦБ РФ недоступен и сохранённого курса нет.</response>
    [HttpGet("{currency}")]
    [ProducesResponseType(typeof(CurrencyQuote), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CurrencyQuote>> GetRate(
        string currency, CancellationToken ct)
    {
        if (currency.Length != 3 || !currency.All(char.IsLetter))
            return BadRequest(new { error = "Используйте трёхбуквенный код валюты." });

        try
        {
            return Ok(await _rates.GetQuoteAsync(currency, ct));
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
