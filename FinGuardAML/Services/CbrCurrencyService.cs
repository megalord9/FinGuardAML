using System.Globalization;
using System.Xml.Linq;
using FinGuardAML.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FinGuardAML.Services;

public sealed class CbrCurrencyService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;

    public CbrCurrencyService(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<CurrencyQuote> GetQuoteAsync(string currency, CancellationToken ct)
    {
        string code = currency.Trim().ToUpperInvariant();
        if (code == "RUB")
        {
            return new CurrencyQuote("RUB", 1m,
                DateOnly.FromDateTime(DateTime.UtcNow), false, DateTimeOffset.UtcNow);
        }

        string cacheKey = $"cbr:{code}";
        _cache.TryGetValue(cacheKey, out CurrencyQuote? oldQuote);
        if (oldQuote is not null && oldQuote.FetchedAtUtc.UtcDateTime.Date == DateTime.UtcNow.Date)
        {
            return oldQuote;
        }

        try
        {
            // Официальный XML-сервис Банка России (без ключа).
            using var response = await _http.GetAsync(
                "https://www.cbr.ru/scripts/XML_daily.asp", ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var xml = await XDocument.LoadAsync(stream, LoadOptions.None, ct);

            var item = xml.Root?.Elements("Valute")
                .FirstOrDefault(v => string.Equals(
                    (string?)v.Element("CharCode"), code,
                    StringComparison.OrdinalIgnoreCase));

            if (item is null)
                throw new ArgumentException($"Валюта {code} отсутствует в ежедневном списке ЦБ РФ.");

            var ru = CultureInfo.GetCultureInfo("ru-RU");
            decimal nominal = decimal.Parse((string?)item.Element("Nominal") ?? "1", ru);
            decimal value = decimal.Parse((string?)item.Element("Value") ?? "0", ru);
            if (nominal <= 0m)
                throw new FormatException("Некорректный номинал курса ЦБ РФ.");

            string dateText = (string?)xml.Root?.Attribute("Date") ?? "";
            var date = DateOnly.ParseExact(dateText, "dd.MM.yyyy", CultureInfo.InvariantCulture);
            var quote = new CurrencyQuote(code, Math.Round(value / nominal, 6),
                date, false, DateTimeOffset.UtcNow);

            _cache.Set(cacheKey, quote, TimeSpan.FromDays(7));
            return quote;
        }
        catch (ArgumentException) // Неподдерживаемая валюта — ошибка запроса.
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                   or System.Xml.XmlException or FormatException)
        {
            // Для учебного проекта разрешён ранее загруженный курс с явной пометкой.
            if (oldQuote is not null)
                return oldQuote with { IsStale = true };

            throw new ExternalServiceException(
                "Не удалось получить курс ЦБ РФ; ранее сохранённого курса нет.", ex);
        }
    }
}
