using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Infrastructure.Reporting;

public record CashFlowLine(string Label, decimal Amount);

public record CashFlowSection(string NameAr, IReadOnlyList<CashFlowLine> Lines)
{
    public decimal Subtotal => Lines.Sum(l => l.Amount);
}

public record CashFlowStatementResult(
    DateOnly FromDate,
    DateOnly ToDate,
    CashFlowSection Operating,
    CashFlowSection Investing,
    CashFlowSection Financing,
    decimal NetCashChange,
    decimal OpeningCash,
    decimal ClosingCash);

/// <summary>قائمة التدفقات النقدية — طريقة غير مباشرة مبسطة من أرصدة الحسابات.</summary>
public class CashFlowStatementService(BrqoshDbContext db)
{
    private static readonly string[] CashCodes = ["1101", "1102", "1103"];

    public async Task<CashFlowStatementResult> ExecuteAsync(long companyId, DateOnly fromDate, DateOnly toDate)
    {
        var dayBefore = fromDate.AddDays(-1);
        var startBalances = await BuildBalancesAsync(companyId, dayBefore);
        var endBalances = await BuildBalancesAsync(companyId, toDate);

        var pnl = await new IncomeStatementQuery(db).ExecuteAsync(companyId, fromDate, toDate);
        var operating = new List<CashFlowLine>
        {
            new("صافي الدخل", pnl.NetIncome),
        };

        var depreciation = DeltaForCodes(startBalances, endBalances, "1207", AccountType.Asset, invert: true);
        if (depreciation != 0m)
            operating.Add(new("إهلاك الأصول (تعديل غير نقدي)", depreciation));

        foreach (var (code, label) in new (string, string)[]
        {
            ("1104", "تغيّر ذمم العملاء"),
            ("1107", "تغيّر المخزون"),
            ("1109", "تغيّر مصروفات مدفوعة مقدماً"),
            ("2101", "تغيّر ذمم الموردين"),
            ("2104", "تغيّر مصروفات مستحقة"),
            ("2105", "تغيّر دفعات مقدمة من العملاء"),
        })
        {
            var delta = DeltaForCodes(startBalances, endBalances, code, null);
            if (delta != 0m)
                operating.Add(new(label, delta));
        }

        var investing = new List<CashFlowLine>();
        var investingDelta = DeltaForPrefix(startBalances, endBalances, "12", AccountType.Asset, exclude: ["1207"]);
        if (investingDelta != 0m)
            investing.Add(new("شراء/بيع أصول ثابتة (صافي)", -investingDelta));

        var financing = new List<CashFlowLine>();
        var loanDelta = DeltaForPrefix(startBalances, endBalances, "22", AccountType.Liability);
        if (loanDelta != 0m)
            financing.Add(new("قروض طويلة الأجل (صافي)", loanDelta));

        var equityDelta = DeltaForPrefix(startBalances, endBalances, "31", AccountType.Equity, exclude: ["3104"]);
        if (equityDelta != 0m)
            financing.Add(new("حقوق ملكية / رأس مال (صافي)", equityDelta));

        var openingCash = SumCash(startBalances);
        var closingCash = SumCash(endBalances);
        var netCashChange = closingCash - openingCash;

        return new CashFlowStatementResult(
            fromDate,
            toDate,
            new CashFlowSection("التشغيل", operating),
            new CashFlowSection("الاستثمار", investing),
            new CashFlowSection("التمويل", financing),
            netCashChange,
            openingCash,
            closingCash);
    }

    private async Task<Dictionary<string, (AccountType Type, decimal SignedBalance)>> BuildBalancesAsync(
        long companyId, DateOnly asOfDate)
    {
        var from = new DateOnly(1900, 1, 1);
        var rows = await new TrialBalanceQuery(db).ExecuteAsync(companyId, from, asOfDate);
        return rows.ToDictionary(
            r => r.AccountCode,
            r => (r.Type, SignedBalance(r)));
    }

    private static decimal SignedBalance(TrialBalanceRow row) => row.Type switch
    {
        AccountType.Asset => row.BalanceDebit - row.BalanceCredit,
        AccountType.Liability or AccountType.Equity => row.BalanceCredit - row.BalanceDebit,
        AccountType.Revenue => row.BalanceCredit - row.BalanceDebit,
        AccountType.Expense => row.BalanceDebit - row.BalanceCredit,
        _ => row.BalanceDebit - row.BalanceCredit,
    };

    private static decimal DeltaForCodes(
        Dictionary<string, (AccountType Type, decimal SignedBalance)> start,
        Dictionary<string, (AccountType Type, decimal SignedBalance)> end,
        string code,
        AccountType? type,
        bool invert = false)
    {
        var startBal = start.GetValueOrDefault(code).SignedBalance;
        var endBal = end.GetValueOrDefault(code).SignedBalance;
        var delta = endBal - startBal;
        var accountType = type ?? start.GetValueOrDefault(code).Type;
        var cashEffect = accountType switch
        {
            AccountType.Asset => -delta,
            AccountType.Liability or AccountType.Equity => delta,
            _ => -delta,
        };
        return invert ? -cashEffect : cashEffect;
    }

    private static decimal DeltaForPrefix(
        Dictionary<string, (AccountType Type, decimal SignedBalance)> start,
        Dictionary<string, (AccountType Type, decimal SignedBalance)> end,
        string prefix,
        AccountType type,
        string[]? exclude = null)
    {
        exclude ??= [];
        var codes = start.Keys.Concat(end.Keys)
            .Where(c => c.StartsWith(prefix, StringComparison.Ordinal) && !exclude.Contains(c))
            .Distinct();
        return codes.Sum(c =>
        {
            var startBal = start.GetValueOrDefault(c).SignedBalance;
            var endBal = end.GetValueOrDefault(c).SignedBalance;
            return endBal - startBal;
        });
    }

    private static decimal SumCash(Dictionary<string, (AccountType Type, decimal SignedBalance)> balances) =>
        CashCodes.Sum(c => balances.GetValueOrDefault(c).SignedBalance);
}
