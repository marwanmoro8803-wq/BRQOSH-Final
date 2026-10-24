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
        using var _lang = PrintLang.Begin(branding.English);
        PrintColumn[] columns =
        [
            new(PrintLang.T("الحساب"), FixedWidth: 80),
            new(PrintLang.T("البيان"), Weight: 3f),
            new(PrintLang.T("مدين"), Numeric: true),
            new(PrintLang.T("دائن"), Numeric: true),
        ];

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => branding.PageTextStyle(10));

                page.Header().Element(h => branding.RenderHeader(h, PrintLang.T("قيد يومية"), PrintLang.TF("رقم {0}", PrintText.Code(entryNumber))));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Card().Column(info =>
                    {
                        info.Item().Element(c => PrintTheme.LabelValue(c, PrintLang.T("التاريخ"), PrintText.Date(entryDate)));
                        info.Item().Element(c => PrintTheme.LabelValue(c, PrintLang.T("الحالة"), PrintLang.T(statusAr)));
                        if (!string.IsNullOrWhiteSpace(headerDescription))
                            info.Item().Element(c => PrintTheme.LabelValue(c, PrintLang.T("البيان"), headerDescription));
                        if (!string.IsNullOrWhiteSpace(sourceDocument))
                            info.Item().Element(c => PrintTheme.LabelValue(c, PrintLang.T("المستند المصدر"), PrintText.Code(sourceDocument)));
                    });

                    col.Item().Element(c => PrintTheme.ReportTable(c, columns, lines.Select(l => (IReadOnlyList<string>)
                    [
                        PrintText.Code(l.AccountCode), l.Description,
                        l.Debit > 0 ? PrintText.Money(l.Debit) : "", l.Credit > 0 ? PrintText.Money(l.Credit) : "",
                    ])));

                    col.Item().Element(c => PrintTheme.SummaryBand(c,
                    [
                        (PrintLang.T("مجموع المدين"), PrintText.Money(totalDebit)),
                        (PrintLang.T("مجموع الدائن"), PrintText.Money(totalCredit)),
                    ]));

                    col.Item().PaddingTop(6).Row(sign =>
                    {
                        sign.RelativeItem().Element(c => PrintTheme.SignatureLine(c, PrintLang.T("المحاسب")));
                        sign.RelativeItem().Element(c => PrintTheme.SignatureLine(c, PrintLang.T("المدير المالي")));
                    });
                });

                page.Footer().Element(f => branding.RenderFooterMeta(f));
            });
        }).GeneratePdf();
    }
}
