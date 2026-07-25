using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

public static class TrialBalancePdfGenerator
{
    static TrialBalancePdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Generate(
        string companyName, DateOnly from, DateOnly to,
        IReadOnlyList<TrialBalanceRow> rows) =>
        Generate(PrintBranding.From(companyName, ""), from, to, rows);

    public static byte[] Generate(
        PrintBranding branding, DateOnly from, DateOnly to,
        IReadOnlyList<TrialBalanceRow> rows)
    {
        var totalDebit = rows.Sum(r => r.TotalDebit);
        var totalCredit = rows.Sum(r => r.TotalCredit);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => branding.PageTextStyle( 10));

                page.Header().Element(h => branding.RenderHeader(
                    h, "ميزان المراجعة", $"من {PrintText.Date(from)} إلى {PrintText.Date(to)}"));

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(70);
                        c.RelativeColumn(3);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Text("الكود").Bold();
                        h.Cell().Text("الحساب").Bold();
                        h.Cell().AlignCenter().Text("مدين").Bold();
                        h.Cell().AlignCenter().Text("دائن").Bold();
                        h.Cell().AlignCenter().Text("ر.مدين").Bold();
                        h.Cell().AlignCenter().Text("ر.دائن").Bold();
                    });
                    foreach (var row in rows)
                    {
                        table.Cell().Text(row.AccountCode);
                        table.Cell().Text(row.AccountNameAr);
                        table.Cell().AlignCenter().Text($"{row.TotalDebit:N2}");
                        table.Cell().AlignCenter().Text($"{row.TotalCredit:N2}");
                        table.Cell().AlignCenter().Text($"{row.BalanceDebit:N2}");
                        table.Cell().AlignCenter().Text($"{row.BalanceCredit:N2}");
                    }
                });

                page.Footer().Text(
                    $"إجمالي المدين: {totalDebit:N2}  |  إجمالي الدائن: {totalCredit:N2}  |  " +
                    (totalDebit == totalCredit ? "متوازن ✓" : "غير متوازن ✗"));
            });
        }).GeneratePdf();
    }
}
