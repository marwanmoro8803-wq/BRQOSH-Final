using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

public record ConsolidatedBalanceSheetRow(
    string AccountCode,
    string AccountNameAr,
    AccountType Type,
    decimal Amount,
    int CompanyCount);

public record ConsolidatedBalanceSheetResult(
    long GroupId,
    string ReportingCurrencyCode,
    DateOnly AsOfDate,
    IReadOnlyList<ConsolidatedBalanceSheetRow> Assets,
    IReadOnlyList<ConsolidatedBalanceSheetRow> Liabilities,
    IReadOnlyList<ConsolidatedBalanceSheetRow> Equity)
{
    public decimal TotalAssets => Assets.Sum(a => a.Amount);
    public decimal TotalLiabilities => Liabilities.Sum(l => l.Amount);
    public decimal TotalEquity => Equity.Sum(e => e.Amount);
    public decimal TotalLiabilitiesAndEquity => TotalLiabilities + TotalEquity;
    public bool IsBalanced => TotalAssets == TotalLiabilitiesAndEquity;
}

/// <summary>قائمة مركز مالي موحّدة — الأرصدة بسعر إقفال.</summary>
public class ConsolidatedBalanceSheetQuery(BrqoshDbContext db, ExchangeRateService rateService)
{
    public async Task<ConsolidatedBalanceSheetResult> ExecuteAsync(
        long groupId,
        DateOnly asOfDate,
        IReadOnlyDictionary<long, decimal>? fxRatesFromFunctionalToReporting = null,
        CancellationToken ct = default)
    {
        var group = await db.CompanyGroups.SingleOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new AccountingException("مجموعة الشركات غير موجودة.");

        var companies = await db.Companies.Where(c => c.CompanyGroupId == groupId).ToListAsync(ct);
        var assets = new Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)>(StringComparer.OrdinalIgnoreCase);
        var liabilities = new Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)>(StringComparer.OrdinalIgnoreCase);
        var equity = new Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)>(StringComparer.OrdinalIgnoreCase);

        decimal consolidatedCurrentYearEarnings = 0m;

        foreach (var company in companies)
        {
            var closingRate = await ConsolidatedFxRateResolver.ResolveAsync(
                rateService, company, group.ReportingCurrencyCode, asOfDate, null,
                FxRateKind.Closing, fxRatesFromFunctionalToReporting, ct);

            var bs = await new BalanceSheetQuery(db).ExecuteAsync(company.Id, asOfDate);

            foreach (var row in bs.Assets)
                Accumulate(assets, row, closingRate, company.Id);
            foreach (var row in bs.Liabilities)
                Accumulate(liabilities, row, closingRate, company.Id);
            foreach (var row in bs.Equity.Where(e => e.AccountCode != "3999"))
                Accumulate(equity, row, closingRate, company.Id);

            var yearStart = new DateOnly(asOfDate.Year, 1, 1);
            var pnl = await new IncomeStatementQuery(db).ExecuteAsync(company.Id, yearStart, asOfDate);
            var avgRate = await ConsolidatedFxRateResolver.ResolveAsync(
                rateService, company, group.ReportingCurrencyCode, asOfDate, yearStart,
                FxRateKind.Average, fxRatesFromFunctionalToReporting, ct);
            consolidatedCurrentYearEarnings += FxConversion.Convert(pnl.NetIncome, avgRate);
        }

        var eliminations = await IntercompanyEliminationHelper.ComputeByAccountAsync(
            db, rateService, group, companies, new DateOnly(1900, 1, 1), asOfDate, FxRateKind.Closing,
            fxRatesFromFunctionalToReporting, ct);
        ApplyBalanceSheetEliminations(assets, eliminations, AccountType.Asset);
        ApplyBalanceSheetEliminations(liabilities, eliminations, AccountType.Liability);
        ApplyBalanceSheetEliminations(equity, eliminations, AccountType.Equity);

        if (consolidatedCurrentYearEarnings != 0)
        {
            if (!equity.TryGetValue("3999", out var cy))
                cy = ("أرباح العام الجاري (موحّدة)", 0m, []);
            cy.Companies.UnionWith(companies.Select(c => c.Id));
            equity["3999"] = (cy.NameAr, cy.Amount + consolidatedCurrentYearEarnings, cy.Companies);
        }

        var assetTotal = assets.Values.Sum(v => v.Amount);
        var liabilityTotal = liabilities.Values.Sum(v => v.Amount);
        var equityTotal = equity.Values.Sum(v => v.Amount);
        var ctaPlug = assetTotal - liabilityTotal - equityTotal;
        if (ctaPlug != 0)
        {
            if (!equity.TryGetValue(SaudiChartOfAccounts.SystemCodes.TranslationReserve, out var cta))
                cta = ("احتياطي ترجمة العملات (موحّد)", 0m, []);
            cta.Companies.UnionWith(companies.Select(c => c.Id));
            equity[SaudiChartOfAccounts.SystemCodes.TranslationReserve] =
                (cta.NameAr, cta.Amount + ctaPlug, cta.Companies);
        }

        return new ConsolidatedBalanceSheetResult(
            groupId,
            group.ReportingCurrencyCode,
            asOfDate,
            ToRows(assets, AccountType.Asset),
            ToRows(liabilities, AccountType.Liability),
            ToRows(equity, AccountType.Equity));
    }

    private static void ApplyBalanceSheetEliminations(
        Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)> bucket,
        IReadOnlyDictionary<string, (decimal Debit, decimal Credit)> eliminations,
        AccountType type)
    {
        foreach (var (code, (elimDebit, elimCredit)) in eliminations)
        {
            if (!bucket.TryGetValue(code, out var row))
                continue;

            var elimAmount = type switch
            {
                AccountType.Asset => elimDebit - elimCredit,
                AccountType.Liability or AccountType.Equity => elimCredit - elimDebit,
                _ => 0m,
            };
            bucket[code] = (row.NameAr, row.Amount - elimAmount, row.Companies);
        }
    }

    private static void Accumulate(
        Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)> bucket,
        BalanceSheetRow row,
        decimal rate,
        long companyId)
    {
        var amount = FxConversion.Convert(row.Amount, rate);
        if (!bucket.TryGetValue(row.AccountCode, out var existing))
            existing = (row.AccountNameAr, 0m, []);
        existing.Companies.Add(companyId);
        bucket[row.AccountCode] = (existing.NameAr, existing.Amount + amount, existing.Companies);
    }

    private static IReadOnlyList<ConsolidatedBalanceSheetRow> ToRows(
        Dictionary<string, (string NameAr, decimal Amount, HashSet<long> Companies)> bucket,
        AccountType type) =>
        bucket
            .Where(kv => kv.Value.Amount != 0)
            .Select(kv => new ConsolidatedBalanceSheetRow(
                kv.Key, kv.Value.NameAr, type, kv.Value.Amount, kv.Value.Companies.Count))
            .OrderBy(r => r.AccountCode)
            .ToList();
}
