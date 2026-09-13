using Brqosh.Infrastructure.Retail;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>PDF — POS daily sales summary.</summary>
public static class PosReportPdfGenerator
{
    static PosReportPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] DailySummary(
        PrintBranding branding,
        IReadOnlyList<PosReportQuery.DailySaleRow> rows)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => branding.PageTextStyle( 10));

                page.Header().Element(h => branding.RenderHeader(h, "تقرير مبيعات نقاط البيع اليومي"));

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Text("التاريخ").Bold();
                        h.Cell().AlignCenter().Text("العمليات").Bold();
                        h.Cell().AlignCenter().Text("المبيعات").Bold();
                        h.Cell().AlignCenter().Text("الضريبة").Bold();
                    });
                    foreach (var row in rows)
                    {
                        table.Cell().Text(PrintText.Ltr(row.Date));
                        table.Cell().AlignCenter().Text(row.Transactions.ToString());
                        table.Cell().AlignCenter().Text($"{row.TotalSales:N2}");
                        table.Cell().AlignCenter().Text($"{row.VatAmount:N2}");
                    }
                });

                page.Footer().Text(
                    $"إجمالي المبيعات: {rows.Sum(r => r.TotalSales):N2} ر.س  |  الضريبة: {rows.Sum(r => r.VatAmount):N2} ر.س");
            });
        }).GeneratePdf();
    }
}
