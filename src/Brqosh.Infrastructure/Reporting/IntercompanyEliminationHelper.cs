using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public record IntercompanyEliminationRow(
    string AccountCode,
    decimal EliminatedDebit,
    decimal EliminatedCredit);

/// <summary>حساب وإزالة معاملات بين الشركات ضمن المجموعة.</summary>
public static class IntercompanyEliminationHelper
{
    public static async Task<Dictionary<string, (decimal Debit, decimal Credit)>> ComputeByAccountAsync(
        BrqoshDbContext db,
        ExchangeRateService rateService,
        CompanyGroup group,
        IReadOnlyList<Company> companies,
        DateOnly fromDate,
        DateOnly toDate,
        FxRateKind rateKind,
        IReadOnlyDictionary<long, decimal>? manualRates = null,
        CancellationToken ct = default)
    {
        var companyIds = companies.Select(c => c.Id).ToHashSet();
        var mappedCodes = await db.IntercompanyAccountMaps
            .Where(m => m.CompanyGroupId == group.Id)
            .Select(m => m.AccountCode)
            .ToListAsync(ct);
        HashSet<string>? icAccountCodes = mappedCodes.Count > 0
            ? mappedCodes.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;

        var entries = await db.JournalEntries
            .Where(e => companyIds.Contains(e.CompanyId)
                        && e.IsIntercompany
                        && e.CounterpartyCompanyId != null
                        && companyIds.Contains(e.CounterpartyCompanyId.Value)
                        && (e.Status == JournalEntryStatus.Posted
                            || e.Status == JournalEntryStatus.Reversed)
                        && e.EntryDate >= fromDate
                        && e.EntryDate <= toDate)
            .ToListAsync(ct);

        var result = new Dictionary<string, (decimal Debit, decimal Credit)>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var company = companies.First(c => c.Id == entry.CompanyId);
            var rate = await ConsolidatedFxRateResolver.ResolveAsync(
                rateService, company, group.ReportingCurrencyCode, toDate,
                rateKind == FxRateKind.Average ? fromDate : null,
                rateKind, manualRates, ct);

            foreach (var line in entry.Lines)
            {
                if (icAccountCodes is not null && !icAccountCodes.Contains(line.AccountCode))
                    continue;

                var debit = FxConversion.Convert(line.Debit, rate);
                var credit = FxConversion.Convert(line.Credit, rate);
                if (!result.TryGetValue(line.AccountCode, out var bucket))
                    bucket = (0m, 0m);
                result[line.AccountCode] = (bucket.Debit + debit, bucket.Credit + credit);
            }
        }

        return result;
    }

    public static void SubtractFromTrialBalance(
        Dictionary<string, (string NameAr, AccountType Type, decimal Debit, decimal Credit, HashSet<long> Companies)> aggregated,
        IReadOnlyDictionary<string, (decimal Debit, decimal Credit)> eliminations)
    {
        foreach (var (code, (elimDebit, elimCredit)) in eliminations)
        {
            if (!aggregated.TryGetValue(code, out var bucket))
                continue;

            aggregated[code] = (
                bucket.NameAr,
                bucket.Type,
                bucket.Debit - elimDebit,
                bucket.Credit - elimCredit,
                bucket.Companies);
        }
    }

    public static void SubtractFromAmounts(
        Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)> aggregated,
        IReadOnlyDictionary<string, (decimal Debit, decimal Credit)> eliminations,
        bool treatAsRevenue)
    {
        foreach (var (code, (elimDebit, elimCredit)) in eliminations)
        {
            if (!aggregated.TryGetValue(code, out var bucket))
                continue;

            var elimAmount = treatAsRevenue
                ? elimCredit - elimDebit
                : elimDebit - elimCredit;

            aggregated[code] = (bucket.NameAr, bucket.Amount - elimAmount, bucket.Companies);
        }
    }

    public static void SubtractFromBalanceSheet(
        Dictionary<string, (string NameAr, AccountType Type, decimal Amount, HashSet<long> Companies)> aggregated,
        IReadOnlyDictionary<string, (decimal Debit, decimal Credit)> eliminations,
        AccountType type)
    {
        foreach (var (code, (elimDebit, elimCredit)) in eliminations)
        {
            if (!aggregated.TryGetValue(code, out var bucket) || bucket.Type != type)
                continue;

            var elimAmount = type switch
            {
                AccountType.Asset => elimDebit - elimCredit,
                AccountType.Liability or AccountType.Equity => elimCredit - elimDebit,
                _ => 0m,
            };

            aggregated[code] = (bucket.NameAr, bucket.Type, bucket.Amount - elimAmount, bucket.Companies);
        }
    }
}

public class IntercompanyEliminationQuery(BrqoshDbContext db, ExchangeRateService rateService)
{
    public async Task<IReadOnlyList<IntercompanyEliminationRow>> ExecuteAsync(
        long groupId,
        DateOnly fromDate,
        DateOnly toDate,
        FxRateKind rateKind = FxRateKind.Closing,
        CancellationToken ct = default)
    {
        Brqosh.Infrastructure.Licensing.LicenseGate.Ensure(Brqosh.Application.Licensing.LicensedCapability.Consolidation);
        var group = await db.CompanyGroups.SingleOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new AccountingException("مجموعة الشركات غير موجودة.");

        var companies = await db.Companies.Where(c => c.CompanyGroupId == groupId).ToListAsync(ct);
        if (companies.Count == 0)
            return [];

        var elim = await IntercompanyEliminationHelper.ComputeByAccountAsync(
            db, rateService, group, companies, fromDate, toDate, rateKind, null, ct);

        return elim
            .Where(kv => kv.Value.Debit != 0 || kv.Value.Credit != 0)
            .Select(kv => new IntercompanyEliminationRow(kv.Key, kv.Value.Debit, kv.Value.Credit))
            .OrderBy(r => r.AccountCode)
            .ToList();
    }
}
