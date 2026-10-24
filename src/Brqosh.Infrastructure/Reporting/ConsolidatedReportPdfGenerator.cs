using System.Globalization;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>PDF — consolidated group reports.</summary>
public static class ConsolidatedReportPdfGenerator
{
    static ConsolidatedReportPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] TrialBalance(
        string groupName,
        string reportingCurrency,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<ConsolidatedTrialBalanceRow> rows)
    {
        using var _lang = PrintLang.Begin(PrintLang.Preferred);
        PrintColumn[] columns =
        [
            new(PrintLang.T("الكود"), FixedWidth: 70),
            new(PrintLang.T("الحساب"), Weight: 3f),
            new(PrintLang.T("شركات"), Numeric: true, FixedWidth: 50),
            new(PrintLang.T("مدين"), Numeric: true),
            new(PrintLang.T("دائن"), Numeric: true),
            new(PrintLang.T("رصيد مدين"), Numeric: true),
            new(PrintLang.T("رصيد دائن"), Numeric: true),
        ];

        var totalDebit = rows.Sum(r => r.TotalDebit);
        var totalCredit = rows.Sum(r => r.TotalCredit);

        return PrintReport.Table(PrintBranding.From(groupName, ""), PrintLang.T("ميزان مراجعة موحّد"),
            PrintLang.TF("من {0} إلى {1} · {2}", PrintText.Date(from), PrintText.Date(to), reportingCurrency), columns,
            rows.Select(r => (IReadOnlyList<string>)
            [
                PrintText.Code(r.AccountCode), r.AccountNameAr, r.CompanyCount.ToString(CultureInfo.InvariantCulture),
                PrintText.Money(r.TotalDebit), PrintText.Money(r.TotalCredit),
                r.BalanceDebit > 0 ? PrintText.Money(r.BalanceDebit) : "",
                r.BalanceCredit > 0 ? PrintText.Money(r.BalanceCredit) : "",
            ]),
            [
                (PrintLang.T("إجمالي المدين"), PrintText.Money(totalDebit)),
                (PrintLang.T("إجمالي الدائن"), PrintText.Money(totalCredit)),
            ],
            fontSize: 9);
    }

    public static byte[] IncomeStatement(
        string groupName,
        ConsolidatedIncomeStatementResult result)
    {
        using var _lang = PrintLang.Begin(PrintLang.Preferred);
        var rows = result.RevenueRows.Select(r => (PrintLang.T("إيرادات"), r.AccountCode, r.AccountNameAr, r.Amount, r.CompanyCount))
            .Concat(result.ExpenseRows.Select(r => (PrintLang.T("مصروفات"), r.AccountCode, r.AccountNameAr, r.Amount, r.CompanyCount)))
            .ToList();

        return BuildSectionReport(
            groupName,
            PrintLang.T("قائمة دخل موحّدة"),
            PrintLang.TF("من {0} إلى {1} · {2}", PrintText.Date(result.FromDate), PrintText.Date(result.ToDate), result.ReportingCurrencyCode),
            rows,
            [
                (PrintLang.T("الإيرادات"), PrintText.Money(result.TotalRevenue)),
                (PrintLang.T("المصروفات"), PrintText.Money(result.TotalExpenses)),
                (PrintLang.T("صافي الدخل"), PrintText.Money(result.NetIncome)),
            ]);
    }

    public static byte[] BalanceSheet(
        string groupName,
        ConsolidatedBalanceSheetResult result)
    {
        using var _lang = PrintLang.Begin(PrintLang.Preferred);
        var rows = result.Assets.Select(r => (PrintLang.T("أصول"), r.AccountCode, r.AccountNameAr, r.Amount, r.CompanyCount))
            .Concat(result.Liabilities.Select(r => (PrintLang.T("التزامات"), r.AccountCode, r.AccountNameAr, r.Amount, r.CompanyCount)))
            .Concat(result.Equity.Select(r => (PrintLang.T("حقوق ملكية"), r.AccountCode, r.AccountNameAr, r.Amount, r.CompanyCount)))
            .ToList();

        return BuildSectionReport(
            groupName,
            PrintLang.T("مركز مالي موحّد"),
            PrintLang.TF("كما في {0} · {1}", PrintText.Date(result.AsOfDate), result.ReportingCurrencyCode),
            rows,
            [
                (PrintLang.T("الأصول"), PrintText.Money(result.TotalAssets)),
                (PrintLang.T("الالتزامات + الملكية"), PrintText.Money(result.TotalLiabilities + result.TotalEquity)),
            ]);
    }

    private static byte[] BuildSectionReport(
        string groupName,
        string title,
        string subtitle,
        IReadOnlyList<(string Section, string Code, string Name, decimal Amount, int CompanyCount)> rows,
        IReadOnlyList<(string Label, string Value)> summary)
    {
        PrintColumn[] columns =
        [
            new(PrintLang.T("القسم"), Weight: 1f),
            new(PrintLang.T("الكود"), FixedWidth: 70),
            new(PrintLang.T("الحساب"), Weight: 3f),
            new(PrintLang.T("شركات"), Numeric: true, FixedWidth: 50),
            new(PrintLang.T("المبلغ"), Numeric: true),
        ];

        return PrintReport.Table(PrintBranding.From(groupName, ""), title, subtitle, columns,
            rows.Select(r => (IReadOnlyList<string>)
            [
                r.Section, PrintText.Code(r.Code), r.Name,
                r.CompanyCount.ToString(CultureInfo.InvariantCulture), PrintText.Money(r.Amount),
            ]),
            summary);
    }
}
