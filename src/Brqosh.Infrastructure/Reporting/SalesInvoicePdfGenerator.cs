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
        string? ComplianceStatus = null,
        // (CEEF-227) الفاتورة الضريبية تلزمها بيانات المشتري لا اسمه وحده.
        string? PartyVatNumber = null,
        string? PartyAddress = null,
        string? NotesAr = null);

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
        // النسبة محسوبة من الأرقام لا مثبّتة بـ«15%»: فاتورة صفرية أو معفاة أو بنسبة
        // خليجية مختلفة كانت تُطبع بنسبة كاذبة.
        var vatRateLabel = PrintText.Ltr(subtotal == 0m
            ? "%"
            : (Math.Round(vat / subtotal * 100m, 2, MidpointRounding.AwayFromZero)).ToString("0.##") + "%");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                // A4 لا A5: الفاتورة الضريبية تحمل جدول بنود بستّة أعمدة وكتلتَي
                // بائع/مشتري — والمقاس الأصغر كان يضغطها ويترك ثلثَ الورقة فارغاً.
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => branding.PageTextStyle(10));

                page.Header().Element(h => branding.RenderHeader(h, title));

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);

                    // ── كتلتان متجاورتان: بيانات المستند · بيانات الطرف ──
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Element(c => LabelBox(c, "بيانات المستند",
                        [
                            ("رقم المستند", PrintText.Code(documentNumber)),
                            ("التاريخ", PrintText.Date(date)),
                            ("نوع المستند", title),
                        ]));

                        row.ConstantItem(12);

                        row.RelativeItem().Element(c => LabelBox(c, $"بيانات {partyLabel}",
                        [
                            ("الاسم", string.IsNullOrWhiteSpace(options.PartyName) ? "—" : options.PartyName),
                            ("الرقم الضريبي", string.IsNullOrWhiteSpace(options.PartyVatNumber)
                                ? "—" : PrintText.Code(options.PartyVatNumber)),
                            ("العنوان", string.IsNullOrWhiteSpace(options.PartyAddress) ? "—" : options.PartyAddress),
                        ]));
                    });

                    // ── جدول البنود: حدود كاملة، رأس مظلَّل، تناوب في الصفوف ──
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(26);      // #
                            c.RelativeColumn(4);       // الصنف
                            c.RelativeColumn(1.1f);    // الكمية
                            c.RelativeColumn(1.4f);    // السعر
                            c.RelativeColumn(1.6f);    // الخاضع للضريبة
                            c.RelativeColumn(1.4f);    // الضريبة
                            c.RelativeColumn(1.6f);    // الإجمالي
                        });

                        table.Header(h =>
                        {
                            HeaderCell(h.Cell(), "#");
                            HeaderCell(h.Cell(), "الصنف");
                            HeaderCell(h.Cell(), "الكمية");
                            HeaderCell(h.Cell(), "السعر");
                            HeaderCell(h.Cell(), "الخاضع للضريبة");
                            HeaderCell(h.Cell(), $"ضريبة {vatRateLabel}");
                            HeaderCell(h.Cell(), "الإجمالي");
                        });

                        var lineVats = AllocateLineVat(lines, subtotal, vat);
                        var index = 0;
                        foreach (var line in lines)
                        {
                            var lineVat = lineVats[index];
                            index++;
                            var shade = index % 2 == 0;

                            BodyCell(table.Cell(), index.ToString(), shade, center: true);
                            BodyCell(table.Cell(), line.Description, shade);
                            BodyCell(table.Cell(), $"{line.Qty:N2}", shade, center: true);
                            BodyCell(table.Cell(), $"{line.Price:N2}", shade, center: true);
                            BodyCell(table.Cell(), $"{line.Total:N2}", shade, center: true);
                            BodyCell(table.Cell(), $"{lineVat:N2}", shade, center: true);
                            BodyCell(table.Cell(), $"{line.Total + lineVat:N2}", shade, center: true);
                        }
                    });

                    // ── الإجماليات في صندوق محاذٍ لليسار + رمز التحقق مقابله ──
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
                            else if (!string.IsNullOrWhiteSpace(options.QrTlvBase64))
                            {
                                inner.Item().Text("تعذّر توليد رمز QR — راجع بيانات ZATCA").FontSize(8);
                            }
                        });

                        row.ConstantItem(12);

                        row.ConstantItem(220).AlignTop().Element(c => TotalsBox(c, subtotal, vat, total, vatRateLabel));
                    });

                    if (!string.IsNullOrWhiteSpace(options.NotesAr))
                        col.Item().Element(c => LabelBox(c, "ملاحظات", [("", options.NotesAr!)]));
                });

                page.Footer().Column(f =>
                {
                    f.Item().Element(x => branding.RenderFooterMeta(x, null));
                    f.Item().AlignCenter().Text(t =>
                    {
                        t.Span("صفحة ").FontSize(7);
                        t.CurrentPageNumber().FontSize(7);
                        t.Span(" من ").FontSize(7);
                        t.TotalPages().FontSize(7);
                    });
                });
            });
        }).GeneratePdf();
    }



    /// <summary>
    /// (CEEF-227) توزيع ضريبة الفاتورة على البنود بحصّة كلّ بند من الوعاء.
    ///
    /// **التقريب لكل سطر وحده لا يكفي:** ثلاثة بنود متساوية من 100 بضريبة 15.00
    /// تُعطي 5.00×3 = 15.00، لكن بنوداً بكسور تُعطي مجموعاً يخالف ذيل الفاتورة
    /// بهللة — وعمودٌ لا يجمع إلى الإجمالي المطبوع أسفله يُسقط ثقة المدقّق فوراً.
    /// لذلك يُحمَّل الفرق المتبقّي على **أكبر بند**: أقلّ أثر نسبيّ، ومجموعٌ مطابق دائماً.
    /// </summary>
    internal static IReadOnlyList<decimal> AllocateLineVat(
        IReadOnlyList<Line> lines, decimal subtotal, decimal vat)
    {
        var allocated = new decimal[lines.Count];
        if (lines.Count == 0 || subtotal == 0m)
            return allocated;

        for (var i = 0; i < lines.Count; i++)
            allocated[i] = Math.Round(vat * (lines[i].Total / subtotal), 2, MidpointRounding.AwayFromZero);

        var residual = vat - allocated.Sum();
        if (residual != 0m)
        {
            var largest = 0;
            for (var i = 1; i < lines.Count; i++)
                if (Math.Abs(lines[i].Total) > Math.Abs(lines[largest].Total))
                    largest = i;
            allocated[largest] += residual;
        }

        return allocated;
    }

    // ── لبنات التخطيط ──────────────────────────────────────────────────────

    private const string BorderColour = "#94A3B8";
    private const string HeaderFill = "#E2E8F0";
    private const string RowFill = "#F5F7FA";
    private const string BoxTitleFill = "#EDF1F6";

    /// <summary>صندوق معنون بصفوف «تسمية: قيمة» — لبيانات المستند والطرف.</summary>
    private static void LabelBox(IContainer container, string title, IReadOnlyList<(string Label, string Value)> rows)
    {
        container.Border(0.8f).BorderColor(BorderColour).Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                col.Item().Background(BoxTitleFill).PaddingVertical(4).PaddingHorizontal(8)
                    .Text(title).Bold().FontSize(9.5f);
            }

            foreach (var (label, value) in rows)
            {
                col.Item().PaddingVertical(3).PaddingHorizontal(8).Row(row =>
                {
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        row.ConstantItem(84).Text(label).FontSize(9).FontColor("#475569");
                        row.RelativeItem().Text(value).FontSize(9.5f);
                    }
                    else
                    {
                        row.RelativeItem().Text(value).FontSize(9.5f);
                    }
                });
            }
        });
    }

    private static void HeaderCell(IContainer cell, string text) =>
        cell.Background(HeaderFill).Border(0.8f).BorderColor(BorderColour)
            .PaddingVertical(5).PaddingHorizontal(4)
            .AlignCenter().Text(text).Bold().FontSize(9);

    private static void BodyCell(IContainer cell, string text, bool shaded, bool center = false)
    {
        var box = cell.Border(0.5f).BorderColor(BorderColour).PaddingVertical(4).PaddingHorizontal(4);
        if (shaded)
            box = box.Background(RowFill);
        var slot = center ? box.AlignCenter() : box;
        slot.Text(text).FontSize(9);
    }

    /// <summary>صندوق الإجماليات — السطر الأخير مميّز لأنه المبلغ المستحق.</summary>
    private static void TotalsBox(IContainer container, decimal subtotal, decimal vat, decimal total, string vatRateLabel)
    {
        container.Border(0.8f).BorderColor(BorderColour).Column(col =>
        {
            TotalRow(col, "الإجمالي قبل الضريبة", subtotal, false, first: true);
            TotalRow(col, $"ضريبة القيمة المضافة {vatRateLabel}", vat, false);
            TotalRow(col, "الإجمالي شامل الضريبة", total, true);
        });
    }

    private static void TotalRow(ColumnDescriptor col, string label, decimal value, bool emphasis, bool first = false)
    {
        var item = col.Item();
        if (emphasis)
            item = item.Background(HeaderFill);
        if (!first)
            item = item.BorderTop(0.5f).BorderColor(BorderColour);

        item.PaddingVertical(5).PaddingHorizontal(8).Row(row =>
        {
            var labelSpan = row.RelativeItem().Text(label).FontSize(emphasis ? 10 : 9);
            var valueSpan = row.ConstantItem(96).AlignLeft()
                .Text($"{value:N2} ر.س").FontSize(emphasis ? 11 : 9.5f);
            if (emphasis)
            {
                labelSpan.Bold();
                valueSpan.Bold();
            }
        });
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
