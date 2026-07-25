using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public record ConsolidatedTrialBalanceRow(
    string AccountCode,
    string AccountNameAr,
    AccountType Type,
    decimal TotalDebit,
    decimal TotalCredit,
    int CompanyCount)
{
    public decimal BalanceDebit => TotalDebit > TotalCredit ? TotalDebit - TotalCredit : 0m;
    public decimal BalanceCredit => TotalCredit > TotalDebit ? TotalCredit - TotalDebit : 0m;
}

/// <summary>
/// ميزان مراجعة موحّد لمجموعة شركات — يحوّل من العملة الوظيفية لكل شركة إلى عملة التقرير.
/// </summary>
public class ConsolidatedTrialBalanceQuery(BrqoshDbContext db, Treasury.ExchangeRateService rateService)
{
    public async Task<IReadOnlyList<ConsolidatedTrialBalanceRow>> ExecuteAsync(
        long groupId,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyDictionary<long, decimal>? fxRatesFromFunctionalToReporting = null,
        CancellationToken ct = default)
    {
        var group = await db.CompanyGroups.SingleOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new AccountingException("مجموعة الشركات غير موجودة.");

        var companies = await db.Companies
            .Where(c => c.CompanyGroupId == groupId)
            .ToListAsync(ct);

        if (companies.Count == 0)
            return [];

        var aggregated = new Dictionary<string, (string NameAr, AccountType Type, decimal Debit, decimal Credit, HashSet<long> Companies)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var company in companies)
        {
            var rate = await ConsolidatedFxRateResolver.ResolveAsync(
                rateService, company, group.ReportingCurrencyCode, toDate, fromDate,
                FxRateKind.Closing, fxRatesFromFunctionalToReporting, ct);
            var rows = await new TrialBalanceQuery(db).ExecuteAsync(company.Id, fromDate, toDate);

            foreach (var row in rows)
            {
                var debit = FxConversion.Convert(row.TotalDebit, rate);
                var credit = FxConversion.Convert(row.TotalCredit, rate);

                if (!aggregated.TryGetValue(row.AccountCode, out var bucket))
                {
                    bucket = (row.AccountNameAr, row.Type, 0m, 0m, []);
                    aggregated[row.AccountCode] = bucket;
                }

                bucket.Companies.Add(company.Id);
                aggregated[row.AccountCode] = (
                    bucket.NameAr,
                    bucket.Type,
                    bucket.Debit + debit,
                    bucket.Credit + credit,
                    bucket.Companies);
            }
        }

        var eliminations = await IntercompanyEliminationHelper.ComputeByAccountAsync(
            db, rateService, group, companies, fromDate, toDate, FxRateKind.Closing,
            fxRatesFromFunctionalToReporting, ct);
        IntercompanyEliminationHelper.SubtractFromTrialBalance(aggregated, eliminations);

        return aggregated
            .Select(kv => new ConsolidatedTrialBalanceRow(
                kv.Key, kv.Value.NameAr, kv.Value.Type,
                kv.Value.Debit, kv.Value.Credit, kv.Value.Companies.Count))
            .OrderBy(r => r.AccountCode)
            .ToList();
    }
}
