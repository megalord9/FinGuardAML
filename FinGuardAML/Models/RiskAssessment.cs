namespace FinGuardAML.Models;

public sealed class RiskAssessment
{
    public Guid TransactionId { get; init; }
    public string ClientId { get; init; } = "";
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "";
    public decimal AmountRub { get; init; }
    public decimal RubPerUnit { get; init; }
    public DateOnly RateDate { get; init; }
    public bool RateIsStale { get; init; }
    public int RiskScore { get; init; }
    public string RiskLevel { get; init; } = "";
    public List<string> TriggeredFlags { get; init; } = new();
    public string ScreeningStatus { get; init; } = "";
    public decimal? ScreeningMatchScore { get; init; }
    public string? ScreeningMatchedName { get; init; }
    public string Recommendation { get; init; } = "";
    public DateTimeOffset CalculatedAtUtc { get; init; }
}
