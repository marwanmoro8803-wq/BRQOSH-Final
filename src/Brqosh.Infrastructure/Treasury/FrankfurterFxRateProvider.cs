using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Brqosh.Application.Treasury;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>
/// أسعار ECB عبر Frankfurter (مجاني، بدون مفتاح) — مناسب للتطوير والتجميع متعدد العملات.
/// </summary>
public sealed class FrankfurterFxRateProvider : IFxRateProvider
{
    public const string DefaultBaseUrl = "https://api.frankfurter.app";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly string _baseUrl;

    public FrankfurterFxRateProvider(string? baseUrl = null)
    {
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
    }

    public string ProviderId => "frankfurter";

    public async Task<IReadOnlyList<FxRateQuote>> FetchRatesAsync(
        string baseCurrency,
        IReadOnlyList<string> targetCurrencies,
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        var functional = ExchangeRate.NormalizeCurrency(baseCurrency);
        var targets = targetCurrencies
            .Select(ExchangeRate.NormalizeCurrency)
            .Where(c => !string.Equals(c, functional, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets.Count == 0)
            return [];

        var quotes = new List<FxRateQuote>();
        foreach (var foreign in targets)
        {
            var url =
                $"{_baseUrl}/{asOfDate:yyyy-MM-dd}?from={Uri.EscapeDataString(foreign)}&to={Uri.EscapeDataString(functional)}";
            FrankfurterResponse? payload;
            try
            {
                payload = await Http.GetFromJsonAsync<FrankfurterResponse>(url, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException)
            {
                throw new AccountingException(
                    $"تعذّر جلب سعر {foreign}→{functional} من Frankfurter: {ex.Message}");
            }

            if (payload?.Rates is null || !payload.Rates.TryGetValue(functional, out var rate) || rate <= 0)
                throw new AccountingException(
                    $"لا يتوفر سعر {foreign}→{functional} بتاريخ {asOfDate:yyyy-MM-dd} من Frankfurter.");

            // ‏(ن-٤٨) بثقافةٍ صريحة — انظر `SamaFxRateProvider` للسبب: على `fa-IR` كانت
            // ‏«2026-01-05» تُقرأ **2647-03-25**، و`ExchangeRateService` ينتقي
            // ‏`EffectiveDate <= asOfDate` — **فالسعرُ يُكتَب ولا يُرى أبداً**.
            var effective = DateOnly.TryParse(payload.Date, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed)
                ? parsed
                : asOfDate;

            quotes.Add(new FxRateQuote(foreign, functional, effective, rate, ProviderId));
        }

        return quotes;
    }

    private sealed class FrankfurterResponse
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("rates")]
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}
