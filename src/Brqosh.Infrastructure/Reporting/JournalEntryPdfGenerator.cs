using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>PDF — journal entry (قيد يومية).</summary>
public static class JournalEntryPdfGenerator
{
    static JournalEntryPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public record LineRow(string AccountCode, string Description, decimal Debit, decimal Credit);

    public static byte[] Generate(
        PrintBranding branding,
        string entryNumber,
        DateOnly entryDate,
        string statusAr,
        string? headerDescription,
        string? sourceDocument,
        IReadOnlyList<LineRow> lines,
        decimal totalDebit,
        decimal totalCredit)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => branding.PageTextStyle( 10));

                page.Header().Element(h => branding.RenderHeader(h, "قيد يومية", $"رقم {entryNumber}"));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Text($"التاريخ: {PrintText.Date(entryDate)}  |  الحالة: {statusAr}");
                    if (!string.IsNullOrWhiteSpace(headerDescription))
                        col.Item().Text($"البيان: {headerDescription}");
                    if (!string.IsNullOrWhiteSpace(sourceDocument))
                        col.Item().Text($"المستند المصدر: {sourceDocument}");

                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(80);
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("الحساب").Bold();
                            h.Cell().Text("البيان").Bold();
                            h.Cell().AlignCenter().Text("مدين").Bold();
                            h.Cell().AlignCenter().Text("دائن").Bold();
                        });
                        foreach (var line in lines)
                        {
                            table.Cell().Text(line.AccountCode);
                            table.Cell().Text(line.Description);
                            table.Cell().AlignCenter().Text(line.Debit > 0 ? $"{line.Debit:N2}" : "");
                            table.Cell().AlignCenter().Text(line.Credit > 0 ? $"{line.Credit:N2}" : "");
                        }
                    });

                    col.Item().PaddingTop(10).Text(
                        $"مجموع المدين: {totalDebit:N2}  |  مجموع الدائن: {totalCredit:N2}").Bold();
                });

                page.Footer().Element(f => branding.RenderFooterMeta(f));
            });
        }).GeneratePdf();
    }
}
