using FinGuardAML.Models;

namespace FinGuardAML.Services;

public sealed class RiskScoringService
{
    private readonly CbrCurrencyService _rates;
    private readonly OpenSanctionsScreeningService _screening;

    public RiskScoringService(CbrCurrencyService rates,
        OpenSanctionsScreeningService screening)
    {
        _rates = rates;
        _screening = screening;
    }

    public async Task<RiskAssessment> EvaluateAsync(TransactionRequest input, CancellationToken ct)
    {
        var quote = await _rates.GetQuoteAsync(input.Currency, ct);
        decimal amountRub = Math.Round(input.Amount * quote.RubPerUnit, 2);
        var screening = await _screening.CheckAsync(
            input.CounterpartyName,
            input.CounterpartyType,
            ct);
        int score = 0;
        var flags = new List<string>();

        // Правила демонстрационные: это не юридические критерии из 115-ФЗ.
        if (amountRub >= 600_000m)
        {
            score += 35;
            flags.Add("LARGE_AMOUNT: сумма от 600 000 руб. (+35)");
        }
        if (input.OperationsLast30Days >= 20)
        {
            score += 20;
            flags.Add("FREQUENT_OPERATIONS: от 20 операций за 30 дней (+20)");
        }
        if (input.TotalAmountLast30Days >= 2_000_000m)
        {
            score += 15;
            flags.Add("HIGH_TURNOVER: оборот за 30 дней от 2 млн руб. (+15)");
        }
        if (!string.Equals(input.SenderCountry, input.ReceiverCountry,
            StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
            flags.Add("CROSS_BORDER: разные страны отправителя и получателя (+15)");
        }
        if (screening.Status == "POSSIBLE_MATCH")
        {
            score += 50;
            flags.Add("WATCHLIST_MATCH: возможное совпадение во внешнем перечне (+50)");
        }

        score = Math.Min(score, 100);
        string level = score >= 60 ? "HIGH" : score >= 30 ? "MEDIUM" : "LOW";
        bool screeningUnavailable = screening.Status is "NOT_CONFIGURED" or "CHECK_FAILED";
        string recommendation = screeningUnavailable
            ? "MANUAL_REVIEW: внешний скрининг не завершён"
            : screening.Status == "POSSIBLE_MATCH" || level == "HIGH"
                ? "MANUAL_REVIEW: требуется дополнительная проверка"
                : level == "MEDIUM" ? "REVIEW: рекомендуется проверка"
                : "PASS: учебная рекомендация";

        return new RiskAssessment
        {
            TransactionId = Guid.NewGuid(),
            ClientId = input.ClientId,
            Amount = input.Amount,
            Currency = input.Currency.Trim().ToUpperInvariant(),
            AmountRub = amountRub,
            RubPerUnit = quote.RubPerUnit,
            RateDate = quote.RateDate,
            RateIsStale = quote.IsStale,
            RiskScore = score,
            RiskLevel = level,
            TriggeredFlags = flags,
            ScreeningStatus = screening.Status,
            ScreeningMatchScore = screening.MatchScore,
            ScreeningMatchedName = screening.MatchedName,
            Recommendation = recommendation,
            CalculatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
