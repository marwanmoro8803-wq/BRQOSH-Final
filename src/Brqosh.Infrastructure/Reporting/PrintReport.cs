using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>
/// هيكلُ التقرير المطبوع الموحّد: ترويسةٌ مشتركة ← جدولٌ ← شريطُ خلاصة ← تذييلٌ مشترك.
/// <b>كلُّ تقريرٍ جدوليٍّ</b> (ميزان المراجعة، قائمة الدخل، المركز المالي، الإقرار، الأستاذ، التدفقات النقدية…)
/// يمرّ من هنا فيتّفق الشكلُ والترتيبُ والتذييلُ ولا يُعاد رسمُ الجدول في كلّ مولِّد.
/// </summary>
internal static class PrintReport
{
    public static byte[] Table(
        PrintBranding branding, string title, string? subtitle,
        IReadOnlyList<PrintColumn> columns, IEnumerable<IReadOnlyList<string>> rows,
        IReadOnlyList<(string Label, string Value)> summary,
        bool landscape = false, float fontSize = 10)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(landscape ? 24 : 28);
                page.DefaultTextStyle(x => branding.PageTextStyle(fontSize));

                page.Header().Element(h => branding.RenderHeader(h, title, subtitle));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Element(c => PrintTheme.ReportTable(c, columns, rows));
                    if (summary.Count > 0)
                        col.Item().Element(c => PrintTheme.SummaryBand(c, summary));
                });

                page.Footer().Element(f => branding.RenderFooterMeta(f));
            });
        }).GeneratePdf();
    }
}
