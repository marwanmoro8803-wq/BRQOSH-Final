using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Brqosh.Application.Treasury;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>
/// مزوّد SAMA/بنك مركزي — يتطلب نقطة نهاية مُعدّة (ملف JSON أو HTTP داخلي).
/// ليس تغذية حية من هيئة دون إعداد تشغيلي — يُستخدم قبل Frankfurter في السلسلة.
/// </summary>
public sealed class SamaFxRateProvider : IFxRateProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly string? _endpointUrl;
    private readonly string? _jsonFilePath;

    public SamaFxRateProvider(string? endpointUrl = null, string? jsonFilePath = null)
    {
        _endpointUrl = string.IsNullOrWhiteSpace(endpointUrl) ? null : endpointUrl.Trim();
        _jsonFilePath = string.IsNullOrWhiteSpace(jsonFilePath) ? null : jsonFilePath.Trim();
    }

    public static SamaFxRateProvider FromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable("BRQOSH_SAMA_FX_URL");
        var file = Environment.GetEnvironmentVariable("BRQOSH_SAMA_FX_JSON");
        return new SamaFxRateProvider(url, file);
    }

    public string ProviderId => "sama";

    public async Task<IReadOnlyList<FxRateQuote>> FetchRatesAsync(
        string baseCurrency,
        IReadOnlyList<string> targetCurrencies,
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        if (_endpointUrl is null && _jsonFilePath is null)
        {
            throw new AccountingException(
                "مزوّد SAMA غير مُعدّ — عيّن BRQOSH_SAMA_FX_URL أو BRQOSH_SAMA_FX_JSON.");
        }

        var functional = ExchangeRate.NormalizeCurrency(baseCurrency);
        var payload = _jsonFilePath is not null
            ? await LoadFromFileAsync(_jsonFilePath, ct)
            : await LoadFromHttpAsync(_endpointUrl!, ct);

        if (payload is null)
            throw new AccountingException("استجابة SAMA فارغة.");

        var effective = DateOnly.TryParse(payload.Date, out var parsed) ? parsed : asOfDate;
        var quotes = new List<FxRateQuote>();

        foreach (var target in targetCurrencies)
        {
            var foreign = ExchangeRate.NormalizeCurrency(target);
            if (string.Equals(foreign, functional, StringComparison.OrdinalIgnoreCase))
                continue;

            var key = $"{foreign}_{functional}";
            if (!payload.Rates.TryGetValue(key, out var rate) || rate <= 0)
            {
                if (!payload.Rates.TryGetValue(foreign, out rate) || rate <= 0)
                    continue;
            }

            quotes.Add(new FxRateQuote(foreign, functional, effective, rate, ProviderId));
        }

        if (quotes.Count == 0)
            throw new AccountingException($"لا توجد أسعار SAMA لـ {functional} بتاريخ {asOfDate:yyyy-MM-dd}.");

        return quotes;
    }

    private static async Task<SamaFxPayload?> LoadFromFileAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            throw new AccountingException($"ملف SAMA FX غير موجود: {path}");
        await using var stream = File.OpenRead(path);
        return await System.Text.Json.JsonSerializer.DeserializeAsync<SamaFxPayload>(stream, cancellationToken: ct);
    }

    private static async Task<SamaFxPayload?> LoadFromHttpAsync(string url, CancellationToken ct)
    {
        try
        {
            return await Http.GetFromJsonAsync<SamaFxPayload>(url, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new AccountingException($"تعذّر جلب أسعار SAMA: {ex.Message}");
        }
    }

    private sealed class SamaFxPayload
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        /// <summary>مفاتيح: "USD_SAR" أو "USD" — وحدات الهدف لكل 1 مصدر.</summary>
        [JsonPropertyName("rates")]
        public Dictionary<string, decimal> Rates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
