var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "FinGuard Analytics API", status = "OK" }));
app.MapGet("/api/v1/analytics/health", () =>
    Results.Ok(new { service = "FinGuard Analytics API", status = "OK" }));

app.MapPost("/api/v1/analytics/analyze", (AnalyticsRequest input) =>
{
    if (input.AmountRub <= 0 || input.OperationsLast30Days < 0 ||
        input.TotalAmountLast30Days < 0 || string.IsNullOrWhiteSpace(input.ClientId))
        return Results.BadRequest(new { error = "Некорректные входные данные." });

    decimal? average = input.OperationsLast30Days > 0
        ? Math.Round(input.TotalAmountLast30Days / input.OperationsLast30Days, 2)
        : null;
    decimal? ratio = average.HasValue && average.Value > 0
        ? Math.Round(input.AmountRub / average.Value, 2) : null;

    // Независимый учебный сигнал: операция значительно выше обычной средней.
    bool anomalous = ratio.HasValue && ratio.Value >= 3m && input.AmountRub >= 10_000m;
    return Results.Ok(new
    {
        additionalScore = anomalous ? 10 : 0,
        signal = anomalous ? "UNUSUAL_VS_AVERAGE" : "NORMAL_ACTIVITY",
        explanation = anomalous
            ? "Сумма операции как минимум втрое превышает среднюю по истории клиента (+10)."
            : "Отклонение от средней суммы не превысило учебный порог.",
        typicalAmountRub = average,
        deviationRatio = ratio
    });
});

app.Run();

public sealed record AnalyticsRequest(
    string ClientId,
    decimal AmountRub,
    int OperationsLast30Days,
    decimal TotalAmountLast30Days);
