using System.Globalization;
using Brqosh.Domain.Companies;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>طباعة PDF — سند قبض أو سند صرف.</summary>
public static class VoucherPdfGenerator
{
    static VoucherPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    // ‏(أسئلةُ المالك 2026-10-24 · العملة) ‏`currencyCode`/`exchangeRate` عملةُ السند وسعرُ صرفه: كان المبلغُ يُطبع موسوماً بالريال أياً كانت
    // ‏عملةُ السند، فسندُ ١٬٠٠٠ دولار يُقرأ ألفَ ريال. وغيابُها = عملةُ دفاتر المنشأة.
    public static byte[] ReceiptVoucher(
        PrintBranding branding, string voucherNumber, DateOnly date,
        string partyName, decimal amount, string method, string reference, string description,
        string? currencyCode = null, decimal exchangeRate = 1m) =>
        Build(branding, "سند قبض", voucherNumber, date, "العميل", partyName, amount, method, reference, description,
            currencyCode ?? branding.CurrencyCode, exchangeRate);

    public static byte[] PaymentVoucher(
        PrintBranding branding, string voucherNumber, DateOnly date,
        string partyName, decimal amount, string method, string reference, string description,
        string? currencyCode = null, decimal exchangeRate = 1m) =>
        Build(branding, "سند صرف", voucherNumber, date, "المورد", partyName, amount, method, reference, description,
            currencyCode ?? branding.CurrencyCode, exchangeRate);

    public static byte[] ReceiptVoucher(
        string companyName, string vatNumber, string voucherNumber, DateOnly date,
        string partyName, decimal amount, string method, string reference, string description) =>
        ReceiptVoucher(PrintBranding.From(companyName, vatNumber), voucherNumber, date,
            partyName, amount, method, reference, description);

    public static byte[] PaymentVoucher(
        string companyName, string vatNumber, string voucherNumber, DateOnly date,
        string partyName, decimal amount, string method, string reference, string description) =>
        PaymentVoucher(PrintBranding.From(companyName, vatNumber), voucherNumber, date,
            partyName, amount, method, reference, description);

    private static byte[] Build(
        PrintBranding branding, string title, string voucherNumber, DateOnly date,
        string partyLabel, string partyName, decimal amount, string method,
        string reference, string description, string currencyCode, decimal exchangeRate)
    {
        var isReceipt = title == "سند قبض";
        var partyCardTitle = isReceipt ? $"استلمنا من ({partyLabel})" : $"صرفنا إلى ({partyLabel})";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                // A5 أفقيّ: السندُ ورقةٌ قصيرة العرض — الاتجاه الأفقيّ يُسكن البطاقاتِ جنباً إلى جنب
                // فلا يبقى ثلثُ الورقة فارغاً تحت التواقيع.
                page.Size(PageSizes.A5.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => branding.PageTextStyle(11));

                page.Header().Element(h => branding.RenderHeader(h, title));

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Row(row =>
                    {
                        row.ConstantItem(170).Element(c => AmountCard(c, amount, currencyCode));
                        row.ConstantItem(10);
                        row.RelativeItem().Element(c => InfoCard(c, voucherNumber, date, method, reference,
                            BookEquivalent(branding, currencyCode, amount, exchangeRate)));
                    });

                    col.Item().Element(c => PartyCard(c, partyCardTitle, partyName, description));
                    col.Item().Element(c => PrintTheme.AmountInWords(c, amount, currencyCode));
                });

                page.Footer().Column(col =>
                {
                    col.Item().PaddingBottom(8).Row(row =>
                    {
                        // من اليسار: المستلم · المدير المالي · المحاسب — فيقع المحاسبُ أقصى اليمين كما تُقرأ الورقة.
                        row.RelativeItem().Element(c => PrintTheme.SignatureLine(c, isReceipt ? "المستلم" : "المستفيد"));
                        row.RelativeItem().Element(c => PrintTheme.SignatureLine(c, "المدير المالي"));
                        row.RelativeItem().Element(c => PrintTheme.SignatureLine(c, "المحاسب"));
                    });
                    col.Item().Element(f => branding.RenderFooterMeta(f));
                });
            });
        }).GeneratePdf();
    }

    /// <summary>سطرُ «ما يعادل بعملة الدفاتر» لسندٍ بعملةٍ أجنبية — وغيرُ ذلك لا سطرَ له.</summary>
    private static string? BookEquivalent(PrintBranding branding, string currencyCode, decimal amount, decimal rate)
    {
        if (string.Equals(currencyCode, branding.CurrencyCode, StringComparison.OrdinalIgnoreCase) || rate <= 0m)
            return null;
        var inBooks = Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
        return $"{PrintText.Ltr(inBooks.ToString("N2", CultureInfo.InvariantCulture))} {branding.CurrencyAr}" +
               $" (سعر الصرف {PrintText.Ltr(rate.ToString("0.######", CultureInfo.InvariantCulture))})";
    }

    private static void AmountCard(IContainer container, decimal amount, string currencyCode)
    {
        container.Card().Column(col =>
        {
            col.Item().Background(PrintTheme.Navy).PaddingVertical(5).AlignCenter()
                .Text("المبلغ").Bold().FontSize(10).FontColor("#FFFFFF");
            col.Item().PaddingVertical(14).AlignCenter().Text(text =>
            {
                text.Span(PrintText.Ltr(amount.ToString("N2", CultureInfo.InvariantCulture)))
                    .Bold().FontSize(21).FontColor(PrintTheme.Navy);
                text.Span("  " + CurrencyCatalog.Short(currencyCode, english: false)).Bold().FontSize(13).FontColor(PrintTheme.Navy);
            });
        });
    }

    private static void InfoCard(IContainer container, string voucherNumber, DateOnly date, string method, string reference,
        string? bookEquivalent)
    {
        container.Card().Column(col =>
        {
            col.Item().Element(c => PrintTheme.LabelValue(c, "رقم السند", PrintText.Code(voucherNumber), emphasizeValue: true));
            col.Item().Element(c => PrintTheme.LabelValue(c, "التاريخ", PrintText.Date(date)));
            col.Item().Element(c => PrintTheme.LabelValue(c, "طريقة الدفع", method));
            if (!string.IsNullOrWhiteSpace(reference))
                col.Item().Element(c => PrintTheme.LabelValue(c, "المرجع", PrintText.Code(reference)));
            if (bookEquivalent is not null)
                col.Item().Element(c => PrintTheme.LabelValue(c, "يعادل بالدفاتر", bookEquivalent));
        });
    }

    private static void PartyCard(IContainer container, string cardTitle, string partyName, string description)
    {
        container.Card().Column(col =>
        {
            col.Item().Element(c => PrintTheme.CardTitle(c, cardTitle));
            col.Item().Element(c => PrintTheme.LabelValue(c, "الاسم", partyName, emphasizeValue: true));
            if (!string.IsNullOrWhiteSpace(description))
                col.Item().Element(c => PrintTheme.LabelValue(c, "وذلك عن", description));
        });
    }
}
