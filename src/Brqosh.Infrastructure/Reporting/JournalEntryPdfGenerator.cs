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
        PrintColumn[] columns =
        [
            new("الحساب", FixedWidth: 80),
            new("البيان", Weight: 3f),
            new("مدين", Numeric: true),
            new("دائن", Numeric: true),
        ];

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => branding.PageTextStyle(10));

                page.Header().Element(h => branding.RenderHeader(h, "قيد يومية", $"رقم {PrintText.Code(entryNumber)}"));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Card().Column(info =>
                    {
                        info.Item().Element(c => PrintTheme.LabelValue(c, "التاريخ", PrintText.Date(entryDate)));
                        info.Item().Element(c => PrintTheme.LabelValue(c, "الحالة", statusAr));
                        if (!string.IsNullOrWhiteSpace(headerDescription))
                            info.Item().Element(c => PrintTheme.LabelValue(c, "البيان", headerDescription));
                        if (!string.IsNullOrWhiteSpace(sourceDocument))
                            info.Item().Element(c => PrintTheme.LabelValue(c, "المستند المصدر", PrintText.Code(sourceDocument)));
                    });

                    col.Item().Element(c => PrintTheme.ReportTable(c, columns, lines.Select(l => (IReadOnlyList<string>)
                    [
                        PrintText.Code(l.AccountCode), l.Description,
                        l.Debit > 0 ? PrintText.Money(l.Debit) : "", l.Credit > 0 ? PrintText.Money(l.Credit) : "",
                    ])));

                    col.Item().Element(c => PrintTheme.SummaryBand(c,
                    [
                        ("مجموع المدين", PrintText.Money(totalDebit)),
                        ("مجموع الدائن", PrintText.Money(totalCredit)),
                    ]));

                    col.Item().PaddingTop(6).Row(sign =>
                    {
                        sign.RelativeItem().Element(c => PrintTheme.SignatureLine(c, "المحاسب"));
                        sign.RelativeItem().Element(c => PrintTheme.SignatureLine(c, "المدير المالي"));
                    });
                });

                page.Footer().Element(f => branding.RenderFooterMeta(f));
            });
        }).GeneratePdf();
    }
}
