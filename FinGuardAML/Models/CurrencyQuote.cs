namespace FinGuardAML.Models;

public sealed record CurrencyQuote(
    string Currency,
    decimal RubPerUnit,
    DateOnly RateDate,
    bool IsStale,
    DateTimeOffset FetchedAtUtc);
