using FinGuardAML.Models;
using FinGuardAML.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinGuardAML.Controllers;

[ApiController]
[Route("api/v1/rates")]
public sealed class CurrencyController : ControllerBase
{
    private readonly CbrCurrencyService _rates;

    public CurrencyController(CbrCurrencyService rates) => _rates = rates;

    // GET: реальный курс ЦБ РФ за одну единицу валюты.
    [HttpGet("{currency}")]
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
