using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>PDF — sales / purchase invoices and credit-debit notes.</summary>
public static class SalesInvoicePdfGenerator
{
    static SalesInvoicePdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public record Line(string Description, decimal Qty, decimal Price, decimal Total);

    public record InvoicePrintOptions(
        PrintDocumentKind Kind = PrintDocumentKind.SimplifiedSalesInvoice,
        string? PartyLabel = null,
        string? PartyName = null,
        string? QrTlvBase64 = null,
        int? Icv = null,
        string? ComplianceStatus = null);

    public static byte[] Generate(
        string companyName, string vatNumber, string invoiceNumber, DateOnly date,
        IReadOnlyList<Line> lines, decimal subtotal, decimal vat, decimal total,
        string? qrBase64 = null) =>
        Generate(
            PrintBranding.From(companyName, vatNumber),
            invoiceNumber,
            date,
            lines,
            subtotal,
            vat,
            total,
            new InvoicePrintOptions(QrTlvBase64: qrBase64));

    public static byte[] Generate(
        PrintBranding branding,
        string documentNumber,
        DateOnly date,
        IReadOnlyList<Line> lines,
        decimal subtotal,
        decimal vat,
        decimal total,
        InvoicePrintOptions? options = null)
    {
        options ??= new InvoicePrintOptions();
        var title = TitleFor(options.Kind);
        var partyLabel = options.PartyLabel ?? PartyLabelFor(options.Kind);
        var qrPng = ZatcaQrImageHelper.TryCreatePng(options.QrTlvBase64);
        var compliance = BuildComplianceLine(options);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(24);
                page.DefaultTextStyle(x => branding.PageTextStyle( 11));

                page.Header().Element(h => branding.RenderHeader(h, title));

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"رقم: {PrintText.Code(documentNumber)}");
                        row.RelativeItem().AlignLeft().Text($"التاريخ: {PrintText.Date(date)}");
                    });
                    if (!string.IsNullOrWhiteSpace(options.PartyName))
                        col.Item().PaddingTop(4).Text($"{partyLabel}: {options.PartyName}");

                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("الصنف").Bold();
                            h.Cell().AlignCenter().Text("الكمية").Bold();
                            h.Cell().AlignCenter().Text("السعر").Bold();
                            h.Cell().AlignCenter().Text("الإجمالي").Bold();
                        });
                        foreach (var line in lines)
                        {
                            table.Cell().Text(line.Description);
                            table.Cell().AlignCenter().Text($"{line.Qty:N2}");
                            table.Cell().AlignCenter().Text($"{line.Price:N2}");
                            table.Cell().AlignCenter().Text($"{line.Total:N2}");
                        }
                    });
                });

                page.Footer().Column(col =>
                {
                    col.Item().AlignLeft().Text($"قبل الضريبة: {subtotal:N2} ر.س");
                    col.Item().AlignLeft().Text($"ضريبة 15%: {vat:N2} ر.س");
                    col.Item().AlignLeft().Text($"الإجمالي: {total:N2} ر.س").Bold().FontSize(13);

                    if (qrPng is not null)
                    {
                        col.Item().PaddingTop(8).Row(row =>
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
                    else if (!string.IsNullOrWhiteSpace(options.QrTlvBase64))
                    {
                        col.Item().PaddingTop(6).Text("تعذّر توليد رمز QR — راجع بيانات ZATCA").FontSize(8);
                    }

                    col.Item().PaddingTop(6).Element(f => branding.RenderFooterMeta(f, compliance));
                });
            });
        }).GeneratePdf();
    }

    private static string TitleFor(PrintDocumentKind kind) => kind switch
    {
        PrintDocumentKind.StandardSalesInvoice => "فاتورة ضريبية",
        PrintDocumentKind.PurchaseInvoice => "فاتورة مشتريات",
        PrintDocumentKind.CreditNote => "إشعار دائن",
        PrintDocumentKind.PurchaseDebitNote => "إشعار مدين مشتريات",
        _ => "فاتورة ضريبية مبسّطة",
    };

    private static string PartyLabelFor(PrintDocumentKind kind) => kind switch
    {
        PrintDocumentKind.PurchaseInvoice or PrintDocumentKind.PurchaseDebitNote => "المورد",
        _ => "العميل",
    };

    private static string? BuildComplianceLine(InvoicePrintOptions options)
    {
        var parts = new List<string>();
        if (options.Icv is int icv)
            parts.Add($"ICV: {icv}");
        if (!string.IsNullOrWhiteSpace(options.ComplianceStatus))
            parts.Add(options.ComplianceStatus);
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
