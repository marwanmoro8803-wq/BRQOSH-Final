using Brqosh.Application.Treasury;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

public class FxRateSyncService(
    BrqoshDbContext db,
    IFxRateProvider provider,
    ExchangeRateService rateService) : IFxRateSyncService
{
    private static readonly string[] DefaultTargets = ["USD", "EUR", "GBP", "AED"];

    public async Task<FxRateSyncResult> SyncCompanyRatesAsync(
        long companyId,
        DateOnly? asOfDate = null,
        IReadOnlyList<string>? targetCurrencies = null,
        CancellationToken ct = default)
    {
        if (companyId <= 0)
            throw new AccountingException("معرّف الشركة مطلوب.");

        var company = await db.Companies.SingleOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new AccountingException("الشركة غير موجودة.");

        var functional = ExchangeRate.NormalizeCurrency(company.FunctionalCurrencyCode);
        var asOf = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var targets = targetCurrencies is { Count: > 0 }
            ? targetCurrencies
            : await ResolveTargetCurrenciesAsync(company, functional, ct);

        var quotes = await provider.FetchRatesAsync(functional, targets, asOf, ct);
        foreach (var quote in quotes)
        {
            await rateService.RecordRateAsync(
                companyId,
                quote.FromCurrency,
                quote.ToCurrency,
                quote.EffectiveDate,
                quote.Rate,
                quote.Source,
                ct);
        }

        return new FxRateSyncResult(provider.ProviderId, asOf, quotes.Count, quotes);
    }

    private async Task<IReadOnlyList<string>> ResolveTargetCurrenciesAsync(
        Domain.Companies.Company company,
        string functional,
        CancellationToken ct)
    {
        var set = new HashSet<string>(DefaultTargets, StringComparer.OrdinalIgnoreCase);

        if (company.CompanyGroupId is long groupId)
        {
            var reporting = await db.CompanyGroups.AsNoTracking()
                .Where(g => g.Id == groupId)
                .Select(g => g.ReportingCurrencyCode)
                .SingleOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(reporting))
                set.Add(reporting);
        }

        var bankCurrencies = await db.BankAccounts.AsNoTracking()
            .Where(b => b.CompanyId == company.Id && b.IsActive)
            .Select(b => b.CurrencyCode)
            .Distinct()
            .ToListAsync(ct);
        foreach (var c in bankCurrencies)
            set.Add(c);

        set.Remove(functional);
        return set.ToList();
    }
}
