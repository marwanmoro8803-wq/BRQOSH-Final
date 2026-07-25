using QuestPDF.Fluent;
using QuestPDF.Helpers;
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
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(groupName).Bold().FontSize(16);
                    col.Item().AlignCenter().Text("ميزان مراجعة موحّد").FontSize(14);
                    col.Item().AlignCenter().Text($"من {from:yyyy-MM-dd} إلى {to:yyyy-MM-dd} · {reportingCurrency}").FontSize(10);
                });

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(70);
                        c.RelativeColumn(3);
                        c.ConstantColumn(50);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Text("الكود").Bold();
                        h.Cell().Text("الحساب").Bold();
                        h.Cell().AlignCenter().Text("شركات").Bold();
                        h.Cell().AlignCenter().Text("مدين").Bold();
                        h.Cell().AlignCenter().Text("دائن").Bold();
                        h.Cell().AlignCenter().Text("رصيد مدين").Bold();
                        h.Cell().AlignCenter().Text("رصيد دائن").Bold();
                    });
                    foreach (var row in rows)
                    {
                        table.Cell().Text(row.AccountCode);
                        table.Cell().Text(row.AccountNameAr);
                        table.Cell().AlignCenter().Text(row.CompanyCount.ToString());
                        table.Cell().AlignCenter().Text($"{row.TotalDebit:N2}");
                        table.Cell().AlignCenter().Text($"{row.TotalCredit:N2}");
                        table.Cell().AlignCenter().Text(row.BalanceDebit > 0 ? $"{row.BalanceDebit:N2}" : "");
                        table.Cell().AlignCenter().Text(row.BalanceCredit > 0 ? $"{row.BalanceCredit:N2}" : "");
                    }
                });

                var totalDebit = rows.Sum(r => r.TotalDebit);
                var totalCredit = rows.Sum(r => r.TotalCredit);
                page.Footer().Text($"إجمالي المدين: {totalDebit:N2} | إجمالي الدائن: {totalCredit:N2}");
            });
        }).GeneratePdf();
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
            $"من {result.FromDate:yyyy-MM-dd} إلى {result.ToDate:yyyy-MM-dd} · {result.ReportingCurrencyCode}",
            rows,
            $"الإيرادات: {result.TotalRevenue:N2} | المصروفات: {result.TotalExpenses:N2} | صافي الدخل: {result.NetIncome:N2}");
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
            $"كما في {result.AsOfDate:yyyy-MM-dd} · {result.ReportingCurrencyCode}",
            rows,
            $"الأصول: {result.TotalAssets:N2} | الالتزامات+الملكية: {result.TotalLiabilities + result.TotalEquity:N2}");
    }

    private static byte[] BuildSectionReport(
        string groupName,
        string title,
        string subtitle,
        IReadOnlyList<(string Section, string Code, string Name, decimal Amount, int CompanyCount)> rows,
        string footer)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(groupName).Bold().FontSize(16);
                    col.Item().AlignCenter().Text(title).FontSize(14);
                    col.Item().AlignCenter().Text(subtitle).FontSize(10);
                });

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1);
                        c.ConstantColumn(70);
                        c.RelativeColumn(3);
                        c.ConstantColumn(50);
                        c.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Text("القسم").Bold();
                        h.Cell().Text("الكود").Bold();
                        h.Cell().Text("الحساب").Bold();
                        h.Cell().AlignCenter().Text("شركات").Bold();
                        h.Cell().AlignCenter().Text("المبلغ").Bold();
                    });
                    foreach (var (section, code, name, amount, companyCount) in rows)
                    {
                        table.Cell().Text(section);
                        table.Cell().Text(code);
                        table.Cell().Text(name);
                        table.Cell().AlignCenter().Text(companyCount.ToString());
                        table.Cell().AlignCenter().Text($"{amount:N2}");
                    }
                });

                page.Footer().Text(footer);
            });
        }).GeneratePdf();
    }
}
