using Brqosh.Application.Treasury;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>يجرب مزوّدين بالتسلسل — الأول الناجح يُستخدم (مثلاً SAMA ثم Frankfurter).</summary>
public sealed class ChainedFxRateProvider(params IFxRateProvider[] providers) : IFxRateProvider
{
    private readonly IFxRateProvider[] _providers = providers.Length > 0
        ? providers
        : throw new ArgumentException("At least one FX provider required.", nameof(providers));

    public string ProviderId => string.Join('+', _providers.Select(p => p.ProviderId));

    public async Task<IReadOnlyList<FxRateQuote>> FetchRatesAsync(
        string baseCurrency,
        IReadOnlyList<string> targetCurrencies,
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        AccountingException? last = null;
        foreach (var provider in _providers)
        {
            try
            {
                var quotes = await provider.FetchRatesAsync(baseCurrency, targetCurrencies, asOfDate, ct);
                if (quotes.Count > 0)
                    return quotes;
            }
            catch (AccountingException ex)
            {
                last = ex;
            }
        }

        throw last ?? new AccountingException("لا يتوفر أي مزوّد أسعار صرف لتاريخ الطلب.");
    }
}
