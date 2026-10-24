using System.Globalization;
using Brqosh.Domain.Companies;
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
        string? ClearanceNotice = null,
        // ‏(أسئلةُ المالك 2026-10-24 · العملة) ‏عملةُ دفاتر المنشأة — يملؤها المولِّدُ من الترويسة.
        string CurrencyCode = "SAR");

    public static byte[] Generate(PrintBranding branding, BillingPrintData data)
    {
        // المستخلصُ فاتورةٌ ضريبيّة — عربيٌّ دائماً.
        using var _lang = PrintLang.Begin(false);
        branding = branding with { English = false };
        data = data with { CurrencyCode = branding.CurrencyCode };
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

                page.Content().PaddingVertical(12).ContentFromRightToLeft().Column(col =>
                {
                    col.Spacing(10);

                    // ‏(ح-٥٣) **التنبيهُ أوّلَ الصفحة** — بالصندوق نفسِه الذي تحمله الفاتورة.
                    if (!string.IsNullOrWhiteSpace(data.ClearanceNotice))
                        col.Item().Element(c =>
                            SalesInvoicePdfGenerator.ClearanceNoticeBox(c, data.ClearanceNotice));

                    // ‏(ح-٥٦) **المشتري بهويّته** — رقمُه الضريبيُّ وعنوانُه، كما على الفاتورة منذ CEEF-227.
                    // ‏والصفوفُ من `PartyRows` وحدَها؛ تُقسَم هنا بطاقتين: المستخلص (مشروعٌ وفترة) ثمّ الطرف.
                    var party = PartyRows(data);
                    var documentRows = new List<(string Label, string Value)>
                    {
                        ("رقم المستخلص", PrintText.Code(data.BillingNumber)),
                        ("التاريخ", PrintText.Date(data.BillingDate)),
                    };
                    var partyRows = new List<(string Label, string Value)>();
                    foreach (var row in party)
                        (row.Label is "المشروع" or "الفترة" ? documentRows : partyRows).Add(row);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Element(c => InfoCard(c, "بيانات المستخلص", documentRows));
                        row.ConstantItem(12);
                        row.RelativeItem().Element(c => InfoCard(c, "بيانات العميل", partyRows));
                    });

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(inner =>
                        {
                            if (qrPng is not null)
                            {
                                inner.Item().Row(qr =>
                                {
                                    qr.ConstantItem(104).Height(104).Image(qrPng);
                                    qr.RelativeItem().PaddingRight(8).Column(t =>
                                    {
                                        t.Item().Text("رمز التحقق (ZATCA)").Bold().FontSize(9);
                                        t.Item().Text("امسح الرمز للتحقق من صحة الفاتورة").FontSize(8);
                                        if (!string.IsNullOrWhiteSpace(compliance))
                                            t.Item().PaddingTop(2).Text(compliance).FontSize(8);
                                    });
                                });
                            }
                        });
                        row.ConstantItem(12);
                        row.ConstantItem(250).AlignTop().Element(c => TotalsCard(c, data, VatLabel(data.VatRate)));
                    });

                    col.Item().Element(c => PrintTheme.AmountInWords(c, data.GrandTotal, branding.CurrencyCode));

                    col.Item().PaddingTop(6).Row(sign =>
                    {
                        sign.RelativeItem().Element(c => PrintTheme.SignatureLine(c, "مهندس الموقع"));
                        sign.RelativeItem().Element(c => PrintTheme.SignatureLine(c, "مدير المشروع"));
                        sign.RelativeItem().Element(c => PrintTheme.SignatureLine(c, "اعتماد العميل"));
                    });
                });

                page.Footer().Element(f => branding.RenderFooterMeta(f, compliance));
            });
        }).GeneratePdf();
    }

    private static void InfoCard(IContainer container, string title, IReadOnlyList<(string Label, string Value)> rows)
    {
        container.Card().Column(col =>
        {
            col.Item().Element(c => PrintTheme.CardTitle(c, title));
            foreach (var (label, value) in rows)
                col.Item().Element(c => PrintTheme.LabelValue(c, label, value));
        });
    }

    /// <summary>الإجماليّاتُ بالترتيب المحاسبيّ: الأعمال ← المحتجز ← الصافي ← الضريبة ← المستحق (الأخيرُ على كحليّ).</summary>
    private static void TotalsCard(IContainer container, BillingPrintData data, string vatLabel)
    {
        var currency = CurrencyCatalog.Short(data.CurrencyCode, english: false);
        string Money(decimal value) => PrintText.Ltr(value.ToString("N2", CultureInfo.InvariantCulture)) + " " + currency;

        container.Card().Column(col =>
        {
            col.Item().Element(c => PrintTheme.LabelValue(c, "قيمة الأعمال الحالية", Money(data.CurrentWorkAmount), labelWidth: 128));
            col.Item().BorderTop(0.5f).BorderColor(PrintTheme.Line)
                .Element(c => PrintTheme.LabelValue(c, $"المحتجز ({data.RetentionPercent:0.##}%)", Money(-data.RetentionAmount), labelWidth: 128));
            col.Item().BorderTop(0.5f).BorderColor(PrintTheme.Line)
                .Element(c => PrintTheme.LabelValue(c, "صافي الذمة (قبل الضريبة)", Money(data.NetBillableAmount), labelWidth: 128));
            col.Item().BorderTop(0.5f).BorderColor(PrintTheme.Line)
                .Element(c => PrintTheme.LabelValue(c, vatLabel, Money(data.VatAmount), labelWidth: 128));
            col.Item().Background(PrintTheme.Navy).PaddingVertical(6).PaddingHorizontal(10).Row(row =>
            {
                row.RelativeItem().AlignLeft().Text(Money(data.GrandTotal)).Bold().FontSize(11).FontColor("#FFFFFF");
                row.ConstantItem(10);
                row.ConstantItem(92).AlignRight().Text("الإجمالي المستحق").Bold().FontSize(10).FontColor("#FFFFFF");
            });
        });
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
