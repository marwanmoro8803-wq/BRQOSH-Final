using Brqosh.Domain.Companies;
using Brqosh.Domain.Treasury;

namespace Brqosh.Infrastructure.Reporting;

internal static class ConsolidatedFxRateResolver
{
    public static async Task<decimal> ResolveAsync(
        Treasury.ExchangeRateService rateService,
        Company company,
        string reportingCurrency,
        DateOnly asOfDate,
        DateOnly? periodStart,
        FxRateKind kind,
        IReadOnlyDictionary<long, decimal>? manualRates,
        CancellationToken ct = default)
    {
        if (string.Equals(company.FunctionalCurrencyCode, reportingCurrency, StringComparison.OrdinalIgnoreCase))
            return 1m;

        if (manualRates is not null && manualRates.TryGetValue(company.Id, out var manual))
            return manual;

        return kind switch
        {
            FxRateKind.Average when periodStart is not null => await rateService.GetAverageRateAsync(
                company.Id, company.FunctionalCurrencyCode, reportingCurrency, periodStart.Value, asOfDate, ct),
            _ => await rateService.GetRateAsync(
                company.Id, company.FunctionalCurrencyCode, reportingCurrency, asOfDate, ct),
        };
    }
}
