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
        string? QrTlvBase64 = null,
        int? Icv = null,
        string? ComplianceStatus = null);

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

                page.Header().Element(h => branding.RenderHeader(h, "مستخلص أعمال"));

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"رقم: {data.BillingNumber}");
                        row.RelativeItem().AlignLeft().Text($"التاريخ: {data.BillingDate:yyyy-MM-dd}");
                    });
                    col.Item().PaddingTop(6).Text($"العميل: {data.CustomerName}");
                    col.Item().Text($"المشروع: {data.ProjectName}");
                    col.Item().Text($"الفترة: {data.PeriodDescription}");

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
                        Row("ضريبة 15%", $"{data.VatAmount:N2} ر.س");
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

    private static string? BuildComplianceLine(BillingPrintData data)
    {
        var parts = new List<string>();
        if (data.Icv is int icv)
            parts.Add($"ICV: {icv}");
        if (!string.IsNullOrWhiteSpace(data.ComplianceStatus))
            parts.Add(data.ComplianceStatus);
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
