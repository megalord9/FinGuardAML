namespace FinGuardAML.Models;

public sealed record ScreeningResult(
    string Status,
    decimal? MatchScore,
    string? MatchedName);
