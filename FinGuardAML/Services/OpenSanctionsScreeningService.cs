using System.Net.Http.Json;
using System.Text.Json;
using FinGuardAML.Models;

namespace FinGuardAML.Services;

public sealed class OpenSanctionsScreeningService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;

    public OpenSanctionsScreeningService(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _configuration = configuration;
    }

    public async Task<ScreeningResult> CheckAsync(
        string name, string entityType, CancellationToken ct)
    {
        string? apiKey = _configuration["OpenSanctions:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = Environment.GetEnvironmentVariable("OPENSANCTIONS_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return new ScreeningResult("NOT_CONFIGURED", null, null);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                "https://api.opensanctions.org/match/default");
            request.Headers.TryAddWithoutValidation("Authorization", $"ApiKey {apiKey}");
            request.Content = JsonContent.Create(new
            {
                queries = new
                {
                    q = new
                    {
                        schema = entityType.Equals(
                            "COMPANY",
                            StringComparison.OrdinalIgnoreCase)
                            ? "Company"
                            : "Person",
                        properties = new { name = new[] { name } }
                    }
                }
            });

            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            await using var body = await response.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            var results = json.RootElement.GetProperty("responses")
                .GetProperty("q").GetProperty("results");

            decimal bestScore = 0m;
            string? bestName = null;
            foreach (var item in results.EnumerateArray())
            {
                if (!item.TryGetProperty("score", out var scoreElement) ||
                    !scoreElement.TryGetDecimal(out var score))
                    continue;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestName = item.TryGetProperty("caption", out var caption)
                        ? caption.GetString() : null;
                }
            }

            // 0.85 — демонстрационный порог, не официальное подтверждение совпадения.
            bool matched = bestScore >= 0.85m;
            return new ScreeningResult(matched ? "POSSIBLE_MATCH" : "NO_MATCH",
                bestScore, matched ? bestName : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   or JsonException or KeyNotFoundException
                                   or InvalidOperationException)
        {
            // Не выдаём ошибку проверки за отсутствие совпадений.
            return new ScreeningResult("CHECK_FAILED", null, null);
        }
    }
}
