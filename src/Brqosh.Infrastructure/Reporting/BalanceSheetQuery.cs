using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Infrastructure.Reporting;

public record BalanceSheetRow(string AccountCode, string AccountNameAr, AccountType Type, decimal Amount);

public record BalanceSheetResult(
    DateOnly AsOfDate,
    IReadOnlyList<BalanceSheetRow> Assets,
    IReadOnlyList<BalanceSheetRow> Liabilities,
    IReadOnlyList<BalanceSheetRow> Equity)
{
    public decimal TotalAssets => Assets.Sum(a => a.Amount);
    public decimal TotalLiabilities => Liabilities.Sum(l => l.Amount);
    public decimal TotalEquity => Equity.Sum(e => e.Amount);
    public decimal TotalLiabilitiesAndEquity => TotalLiabilities + TotalEquity;
    public bool IsBalanced => TotalAssets == TotalLiabilitiesAndEquity;
}

/// <summary>قائمة المركز المالي — أرصدة الأصول والالتزامات وحقوق الملكية حتى تاريخ محدد.</summary>
public class BalanceSheetQuery(BrqoshDbContext db)
{
    public async Task<BalanceSheetResult> ExecuteAsync(long companyId, DateOnly asOfDate)
    {
        var from = new DateOnly(1900, 1, 1);
        var rows = await new TrialBalanceQuery(db).ExecuteAsync(companyId, from, asOfDate);

        var assets = rows
            .Where(r => r.Type == AccountType.Asset)
            .Select(r => new BalanceSheetRow(r.AccountCode, r.AccountNameAr, r.Type, r.BalanceDebit - r.BalanceCredit))
            .Where(r => r.Amount != 0)
            .OrderBy(r => r.AccountCode)
            .ToList();

        var liabilities = rows
            .Where(r => r.Type == AccountType.Liability)
            .Select(r => new BalanceSheetRow(r.AccountCode, r.AccountNameAr, r.Type, r.BalanceCredit - r.BalanceDebit))
            .Where(r => r.Amount != 0)
            .OrderBy(r => r.AccountCode)
            .ToList();

        var equity = rows
            .Where(r => r.Type == AccountType.Equity)
            .Select(r => new BalanceSheetRow(r.AccountCode, r.AccountNameAr, r.Type, r.BalanceCredit - r.BalanceDebit))
            .Where(r => r.Amount != 0)
            .OrderBy(r => r.AccountCode)
            .ToList();

        // أرباح العام الجاري غير المقفلة — من قائمة الدخل حتى تاريخ التقرير
        var yearStart = new DateOnly(asOfDate.Year, 1, 1);
        var pnl = await new IncomeStatementQuery(db).ExecuteAsync(companyId, yearStart, asOfDate);
        if (pnl.NetIncome != 0)
        {
            equity.Add(new BalanceSheetRow(
                "3999",
                "أرباح العام الجاري (غير مقفلة)",
                AccountType.Equity,
                pnl.NetIncome));
            equity = equity.OrderBy(e => e.AccountCode).ToList();
        }

        return new BalanceSheetResult(asOfDate, assets, liabilities, equity);
    }
}
