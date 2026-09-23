using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>PDF — progress billing (مستخلص أعمال).</summary>
public static class ProgressBillingPdfGenerator
{
    static ProgressBillingPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public record BillingPrintData(
        string BillingNumber,
        DateOnly BillingDate,
        string CustomerName,
        string ProjectName,
        string PeriodDescription,
        decimal CurrentWorkAmount,
        decimal RetentionPercent,
        decimal RetentionAmount,
        decimal NetBillableAmount,
        decimal VatAmount,
        decimal GrandTotal,
        // (جاهزيّةُ الإطلاق · ح-٥٦ · ui-ux-i18n-printing#1) ‏**المستخلصُ فاتورةٌ ضريبيّة** — نوعُها ونسبتُها
        // ‏وهويّةُ المشتري **إلزاميّةٌ بلا افتراضيّ**: يُسمّيها كلُّ مُنتِج، فلا تُطبع «15%» لأنّ أحداً نسيها.
        PrintDocumentKind Kind,
        decimal VatRate,
        string? PartyVatNumber,
        string? PartyAddress,
        string? QrTlvBase64 = null,
        int? Icv = null,
        string? ComplianceStatus = null,
        // (جاهزيّةُ الإطلاق · ح-٥٣) ‏المستخلصُ لعميلٍ مسجَّلٍ يُخلَّص — وما لم يُعتمد يقول ذلك.
        string? ClearanceNotice = null);

    public static byte[] Generate(PrintBranding branding, BillingPrintData data)
    {
        var qrPng = ZatcaQrImageHelper.TryCreatePng(data.QrTlvBase64);
        var compliance = BuildComplianceLine(data);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => branding.PageTextStyle( 11));

                page.Header().Element(h => branding.RenderHeader(h, Title(data)));

                page.Content().PaddingVertical(12).Column(col =>
                {
                    // ‏(ح-٥٣) **التنبيهُ أوّلَ الصفحة** — بالصندوق نفسِه الذي تحمله الفاتورة.
                    if (!string.IsNullOrWhiteSpace(data.ClearanceNotice))
                        col.Item().PaddingBottom(10).Element(c =>
                            SalesInvoicePdfGenerator.ClearanceNoticeBox(c, data.ClearanceNotice));

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"رقم: {PrintText.Code(data.BillingNumber)}");
                        row.RelativeItem().AlignLeft().Text($"التاريخ: {PrintText.Date(data.BillingDate)}");
                    });
                    // ‏(ح-٥٦) **المشتري بهويّته** — رقمُه الضريبيُّ وعنوانُه، كما على الفاتورة منذ CEEF-227.
                    col.Item().PaddingTop(6).Column(inner =>
                    {
                        foreach (var (label, value) in PartyRows(data))
                            inner.Item().Text($"{label}: {value}");
                    });

                    col.Item().PaddingTop(16).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                        });
                        void Row(string label, string value, bool bold = false)
                        {
                            var labelCell = table.Cell().Text(label);
                            if (bold) labelCell.Bold();
                            var valueCell = table.Cell().AlignLeft().Text(value);
                            if (bold) valueCell.Bold();
                        }

                        Row("قيمة الأعمال الحالية", $"{data.CurrentWorkAmount:N2} ر.س");
                        Row("نسبة المحتجز", $"{data.RetentionPercent:N2}%");
                        Row("مبلغ المحتجز", $"{data.RetentionAmount:N2} ر.س");
                        Row("صافي الذمة (قبل الضريبة)", $"{data.NetBillableAmount:N2} ر.س");
                        Row(VatLabel(data.VatRate), $"{data.VatAmount:N2} ر.س");
                        Row("الإجمالي المستحق", $"{data.GrandTotal:N2} ر.س", bold: true);
                    });

                    if (qrPng is not null)
                    {
                        col.Item().PaddingTop(16).Row(row =>
                        {
                            row.ConstantItem(96).Height(96).Image(qrPng);
                            row.RelativeItem().PaddingLeft(8).Column(inner =>
                            {
                                inner.Item().Text("رمز ZATCA — مسح للتحقق").Bold().FontSize(9);
                                if (!string.IsNullOrWhiteSpace(compliance))
                                    inner.Item().Text(compliance).FontSize(8);
                            });
                        });
                    }
                });

                page.Footer().Element(f => branding.RenderFooterMeta(f, compliance));
            });
        }).GeneratePdf();
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٥٦ · ui-ux-i18n-printing#1) <b>عنوانُ الورقة عنوانُ الفاتورة</b> — ثمّ «مستخلص
    /// أعمال» وصفاً. <b>وكان «مستخلص أعمال» وحدَه</b> ومستندُه عند الهيئة فاتورةٌ تُخلَّص أو تُبلَّغ، فيأخذ
    /// المشتري ورقةً لا تقول إنّها فاتورةٌ ضريبيّة. والعنوانُ من <c>TitleFor</c> الفاتورةِ لا نصٌّ ثانٍ.
    /// </summary>
    internal static string Title(BillingPrintData data) =>
        data.Kind is PrintDocumentKind.StandardSalesInvoice or PrintDocumentKind.SimplifiedSalesInvoice
            ? $"{SalesInvoicePdfGenerator.TitleFor(data.Kind)} — مستخلص أعمال"
            : throw new ArgumentOutOfRangeException(nameof(data), data.Kind,
                "المستخلصُ فاتورةٌ ضريبيّةٌ أو مبسّطةٌ لا غير.");

    /// <summary>(ح-٥٦) ‏المشتري وسياقُ المستخلص — <b>والرقمُ الضريبيُّ والعنوانُ «—» إن غابا</b>، كالفاتورة.</summary>
    internal static IReadOnlyList<(string Label, string Value)> PartyRows(BillingPrintData data) =>
    [
        ("العميل", data.CustomerName),
        ("الرقم الضريبي", string.IsNullOrWhiteSpace(data.PartyVatNumber) ? "—" : PrintText.Code(data.PartyVatNumber)),
        ("العنوان", string.IsNullOrWhiteSpace(data.PartyAddress) ? "—" : data.PartyAddress),
        ("المشروع", data.ProjectName),
        ("الفترة", data.PeriodDescription),
    ];

    /// <summary>
    /// (ح-٥٦) ‏<b>رأسُ الضريبة من النسبة التي حُسبت بها</b> — <c>ProgressBilling.SaudiVatRate</c> يمرّ في
    /// البيانات. <b>وكان «ضريبة 15%» نصّاً</b>: تتغيّر نسبةُ الكيان فتبقى الورقةُ على ما كانت.
    /// </summary>
    internal static string VatLabel(decimal rate) =>
        $"ضريبة {PrintText.Ltr((rate * 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%")}";

    // (جاهزيّةُ الإطلاق · ح-٥٤) ‏**وكان يطبع اسمَ الحالة البرمجيّ** — «SimulatedLocally» و«ReadyToClear»
    // ‏على ورقةٍ عربيّة، وقد أُصلح ذلك في الفاتورة منذ CEEF-229. ‏السطرُ الآن سطرُها نفسُه.
    private static string? BuildComplianceLine(BillingPrintData data) =>
        PrintedCompliance.Line(data.Icv, data.ComplianceStatus);
}
