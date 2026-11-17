using System.Net.Http.Json;
using System.Text.Json;

namespace FinGuardAML.Services;

public sealed record PartnerAnalyticsRequest(
    string ClientId,
    decimal AmountRub,
    int OperationsLast30Days,
    decimal TotalAmountLast30Days);

public sealed record PartnerAnalyticsResult(
    string Status,
    int AdditionalScore,
    string Signal,
    string Explanation,
    decimal? TypicalAmountRub = null,
    decimal? DeviationRatio = null);

public sealed class PartnerAnalyticsService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public PartnerAnalyticsService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    private Uri? GetUri(string relativePath)
    {
        string? baseUrl = _config["PartnerAnalytics:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var root))
            return null;
        // HTTP допускается только для локальной разработки.
        if (root.Scheme != Uri.UriSchemeHttps &&
            !(root.Scheme == Uri.UriSchemeHttp && root.IsLoopback))
            return null;
        return new Uri(root, relativePath);
    }

    public async Task<string> GetStatusAsync(CancellationToken ct)
    {
        var uri = GetUri("api/v1/analytics/health");
        if (uri is null) return "NOT_CONFIGURED";
        try
        {
            using var response = await _http.GetAsync(uri, ct);
            return response.IsSuccessStatusCode ? "AVAILABLE" : "UNAVAILABLE";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return "UNAVAILABLE";
        }
    }

    public async Task<PartnerAnalyticsResult> AnalyzeAsync(
        PartnerAnalyticsRequest input, CancellationToken ct)
    {
        var uri = GetUri("api/v1/analytics/analyze");
        if (uri is null)
            return new PartnerAnalyticsResult("NOT_CONFIGURED", 0, "NONE",
                "Второй аналитический API пока не подключён.");

        try
        {
            using var response = await _http.PostAsJsonAsync(uri, input, ct);
            if (!response.IsSuccessStatusCode)
                return new PartnerAnalyticsResult("UNAVAILABLE", 0, "NONE",
                    "Второй API вернул ошибку HTTP.");

            var data = await response.Content.ReadFromJsonAsync<PartnerResponse>(
                cancellationToken: ct);
            if (data is null || data.AdditionalScore < 0 || data.AdditionalScore > 30 ||
                string.IsNullOrWhiteSpace(data.Signal))
                return new PartnerAnalyticsResult("BAD_RESPONSE", 0, "NONE",
                    "Второй API вернул неожиданный формат JSON.");

            return new PartnerAnalyticsResult("OK", data.AdditionalScore,
                data.Signal, data.Explanation ?? "",
                data.TypicalAmountRub, data.DeviationRatio);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   or JsonException or NotSupportedException)
        {
            return new PartnerAnalyticsResult("UNAVAILABLE", 0, "NONE",
                "Не удалось выполнить запрос ко второму API.");
        }
    }

    public sealed class PartnerResponse
    {
        public int AdditionalScore { get; set; }
        public string Signal { get; set; } = "";
        public string? Explanation { get; set; }
        public decimal? TypicalAmountRub { get; set; }
        public decimal? DeviationRatio { get; set; }
    }
}
