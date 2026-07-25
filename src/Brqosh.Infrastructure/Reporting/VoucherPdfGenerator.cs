using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>طباعة PDF — سند قبض أو سند صرف.</summary>
public static class VoucherPdfGenerator
{
    static VoucherPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] ReceiptVoucher(
        PrintBranding branding, string voucherNumber, DateOnly date,
        string partyName, decimal amount, string method, string reference, string description) =>
        Build(branding, "سند قبض", voucherNumber, date, "العميل", partyName, amount, method, reference, description);

    public static byte[] PaymentVoucher(
        PrintBranding branding, string voucherNumber, DateOnly date,
        string partyName, decimal amount, string method, string reference, string description) =>
        Build(branding, "سند صرف", voucherNumber, date, "المورد", partyName, amount, method, reference, description);

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
        string reference, string description)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(28);
                page.DefaultTextStyle(x => branding.PageTextStyle( 11));

                page.Header().Element(h => branding.RenderHeader(h, title));

                page.Content().Column(col =>
                {
                    col.Item().Text($"رقم السند: {voucherNumber}");
                    col.Item().Text($"التاريخ: {date:yyyy-MM-dd}");
                    col.Item().PaddingTop(8).Text($"{partyLabel}: {partyName}");
                    col.Item().Text($"طريقة الدفع: {method}");
                    if (!string.IsNullOrWhiteSpace(reference))
                        col.Item().Text($"المرجع: {reference}");
                    col.Item().PaddingTop(12).Text($"المبلغ: {amount:N2} ر.س").Bold().FontSize(14);
                    if (!string.IsNullOrWhiteSpace(description))
                        col.Item().PaddingTop(8).Text($"البيان: {description}");
                });

                page.Footer().Column(col =>
                {
                    col.Item().PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Text("المحاسب").FontSize(10);
                        row.RelativeItem().AlignCenter().Text("المدير المالي").FontSize(10);
                        row.RelativeItem().AlignCenter().Text("المستلم").FontSize(10);
                    });
                    col.Item().Element(f => branding.RenderFooterMeta(f));
                });
            });
        }).GeneratePdf();
    }
}
