using Brqosh.Application.Treasury;
using Brqosh.Domain.Treasury;

namespace Brqosh.Application.Tests.Treasury;

/// <summary>مزوّد ثابت للاختبارات — بدون شبكة.</summary>
public sealed class StubFxRateProvider : IFxRateProvider
{
    private readonly IReadOnlyDictionary<(string From, string To), decimal> _rates;

    public StubFxRateProvider(IReadOnlyDictionary<(string From, string To), decimal> rates)
    {
        _rates = rates;
    }

    public string ProviderId => "stub";

    public Task<IReadOnlyList<FxRateQuote>> FetchRatesAsync(
        string baseCurrency,
        IReadOnlyList<string> targetCurrencies,
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        var functional = ExchangeRate.NormalizeCurrency(baseCurrency);
        var quotes = new List<FxRateQuote>();

        foreach (var target in targetCurrencies)
        {
            var foreign = ExchangeRate.NormalizeCurrency(target);
            if (string.Equals(foreign, functional, StringComparison.OrdinalIgnoreCase))
                continue;

            if (_rates.TryGetValue((foreign, functional), out var rate))
                quotes.Add(new FxRateQuote(foreign, functional, asOfDate, rate, ProviderId));
        }

        return Task.FromResult<IReadOnlyList<FxRateQuote>>(quotes);
    }
}
