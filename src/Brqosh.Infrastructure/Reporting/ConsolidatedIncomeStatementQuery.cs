using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public record ConsolidatedIncomeStatementRow(
    string AccountCode,
    string AccountNameAr,
    AccountType Type,
    decimal Amount,
    int CompanyCount);

public record ConsolidatedIncomeStatementResult(
    long GroupId,
    string ReportingCurrencyCode,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<ConsolidatedIncomeStatementRow> RevenueRows,
    IReadOnlyList<ConsolidatedIncomeStatementRow> ExpenseRows)
{
    public decimal TotalRevenue => RevenueRows.Sum(r => r.Amount);
    public decimal TotalExpenses => ExpenseRows.Sum(r => r.Amount);
    public decimal NetIncome => TotalRevenue - TotalExpenses;
}

/// <summary>قائمة دخل موحّدة — الإيرادات/المصروفات بسعر متوسط الفترة.</summary>
public class ConsolidatedIncomeStatementQuery(BrqoshDbContext db, ExchangeRateService rateService)
{
    public async Task<ConsolidatedIncomeStatementResult> ExecuteAsync(
        long groupId,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyDictionary<long, decimal>? fxRatesFromFunctionalToReporting = null,
        CancellationToken ct = default)
    {
        Brqosh.Infrastructure.Licensing.LicenseGate.Ensure(Brqosh.Application.Licensing.LicensedCapability.Consolidation);
        var group = await db.CompanyGroups.SingleOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new AccountingException("مجموعة الشركات غير موجودة.");

        var companies = await db.Companies.Where(c => c.CompanyGroupId == groupId).ToListAsync(ct);
        var revenue = new Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)>(StringComparer.OrdinalIgnoreCase);
        var expenses = new Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)>(StringComparer.OrdinalIgnoreCase);

        foreach (var company in companies)
        {
            var rate = await ConsolidatedFxRateResolver.ResolveAsync(
                rateService, company, group.ReportingCurrencyCode, toDate, fromDate,
                FxRateKind.Average, fxRatesFromFunctionalToReporting, ct);

            var pnl = await new IncomeStatementQuery(db).ExecuteAsync(company.Id, fromDate, toDate);

            foreach (var row in pnl.RevenueRows)
            {
                var amount = FxConversion.Convert(row.Amount, rate);
                Accumulate(revenue, row.AccountCode, row.AccountNameAr, amount, company.Id);
            }

            foreach (var row in pnl.ExpenseRows)
            {
                var amount = FxConversion.Convert(row.Amount, rate);
                Accumulate(expenses, row.AccountCode, row.AccountNameAr, amount, company.Id);
            }
        }

        var eliminations = await IntercompanyEliminationHelper.ComputeByAccountAsync(
            db, rateService, group, companies, fromDate, toDate, FxRateKind.Average,
            fxRatesFromFunctionalToReporting, ct);
        IntercompanyEliminationHelper.SubtractFromAmounts(revenue, eliminations, treatAsRevenue: true);
        IntercompanyEliminationHelper.SubtractFromAmounts(expenses, eliminations, treatAsRevenue: false);

        return new ConsolidatedIncomeStatementResult(
            groupId,
            group.ReportingCurrencyCode,
            fromDate,
            toDate,
            ToRows(revenue, AccountType.Revenue),
            ToRows(expenses, AccountType.Expense));
    }

    private static void Accumulate(
        Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)> bucket,
        string code, string nameAr, decimal amount, long companyId)
    {
        if (!bucket.TryGetValue(code, out var row))
            row = (nameAr, 0m, []);
        row.Companies.Add(companyId);
        bucket[code] = (row.NameAr, row.Amount + amount, row.Companies);
    }

    private static IReadOnlyList<ConsolidatedIncomeStatementRow> ToRows(
        Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)> bucket,
        AccountType type) =>
        bucket
            .Where(kv => kv.Value.Amount != 0)
            .Select(kv => new ConsolidatedIncomeStatementRow(
                kv.Key, kv.Value.NameAr, type, kv.Value.Amount, kv.Value.Companies.Count))
            .OrderBy(r => r.AccountCode)
            .ToList();
}
