using FinGuardAML.Models;
using FinGuardAML.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinGuardAML.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class RiskController : ControllerBase
{
    private readonly RiskScoringService _scoring;
    private readonly TransactionStore _store;

    public RiskController(RiskScoringService scoring, TransactionStore store)
    {
        _scoring = scoring;
        _store = store;
    }

    // POST: принимает данные операции, рассчитывает риск и сохраняет результат.
    [HttpPost("risk-score")]
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

    // GET: возвращает все обработанные транзакции.
    [HttpGet("transactions")]
    public ActionResult<IReadOnlyList<RiskAssessment>> GetTransactions() =>
        Ok(_store.GetAll());

    // GET: возвращает результат по ID или 404.
    [HttpGet("transactions/{id:guid}")]
    public ActionResult<RiskAssessment> GetTransaction(Guid id)
    {
        var found = _store.Find(id);
        return found is null ? NotFound() : Ok(found);
    }
}
