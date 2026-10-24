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
        PrintColumn[] columns =
        [
            new("الكود", FixedWidth: 70),
            new("الحساب", Weight: 3f),
            new("شركات", Numeric: true, FixedWidth: 50),
            new("مدين", Numeric: true),
            new("دائن", Numeric: true),
            new("رصيد مدين", Numeric: true),
            new("رصيد دائن", Numeric: true),
        ];

        var totalDebit = rows.Sum(r => r.TotalDebit);
        var totalCredit = rows.Sum(r => r.TotalCredit);

        return PrintReport.Table(PrintBranding.From(groupName, ""), "ميزان مراجعة موحّد",
            $"من {PrintText.Date(from)} إلى {PrintText.Date(to)} · {reportingCurrency}", columns,
            rows.Select(r => (IReadOnlyList<string>)
            [
                PrintText.Code(r.AccountCode), r.AccountNameAr, r.CompanyCount.ToString(CultureInfo.InvariantCulture),
                PrintText.Money(r.TotalDebit), PrintText.Money(r.TotalCredit),
                r.BalanceDebit > 0 ? PrintText.Money(r.BalanceDebit) : "",
                r.BalanceCredit > 0 ? PrintText.Money(r.BalanceCredit) : "",
            ]),
            [
                ("إجمالي المدين", PrintText.Money(totalDebit)),
                ("إجمالي الدائن", PrintText.Money(totalCredit)),
            ],
            fontSize: 9);
    }

    public static byte[] IncomeStatement(
        string groupName,
        ConsolidatedIncomeStatementResult result)
    {
        var rows = result.RevenueRows.Select(r => ("إيرادات", r.AccountCode, r.AccountNameAr, r.Amount, r.CompanyCount))
            .Concat(result.ExpenseRows.Select(r => ("مصروفات", r.AccountCode, r.AccountNameAr, r.Amount, r.CompanyCount)))
            .ToList();

        return BuildSectionReport(
            groupName,
            "قائمة دخل موحّدة",
            $"من {PrintText.Date(result.FromDate)} إلى {PrintText.Date(result.ToDate)} · {result.ReportingCurrencyCode}",
            rows,
            [
                ("الإيرادات", PrintText.Money(result.TotalRevenue)),
                ("المصروفات", PrintText.Money(result.TotalExpenses)),
                ("صافي الدخل", PrintText.Money(result.NetIncome)),
            ]);
    }

    public static byte[] BalanceSheet(
        string groupName,
        ConsolidatedBalanceSheetResult result)
    {
        var rows = result.Assets.Select(r => ("أصول", r.AccountCode, r.AccountNameAr, r.Amount, r.CompanyCount))
            .Concat(result.Liabilities.Select(r => ("التزامات", r.AccountCode, r.AccountNameAr, r.Amount, r.CompanyCount)))
            .Concat(result.Equity.Select(r => ("حقوق ملكية", r.AccountCode, r.AccountNameAr, r.Amount, r.CompanyCount)))
            .ToList();

        return BuildSectionReport(
            groupName,
            "مركز مالي موحّد",
            $"كما في {PrintText.Date(result.AsOfDate)} · {result.ReportingCurrencyCode}",
            rows,
            [
                ("الأصول", PrintText.Money(result.TotalAssets)),
                ("الالتزامات + الملكية", PrintText.Money(result.TotalLiabilities + result.TotalEquity)),
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
            new("القسم", Weight: 1f),
            new("الكود", FixedWidth: 70),
            new("الحساب", Weight: 3f),
            new("شركات", Numeric: true, FixedWidth: 50),
            new("المبلغ", Numeric: true),
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
