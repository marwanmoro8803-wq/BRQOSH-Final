using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>كشف حساب عميل أو مورد — PDF.</summary>
public static class StatementPdfGenerator
{
    static StatementPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] CustomerStatement(
        string companyName, string customerName, decimal closingBalance,
        IReadOnlyList<CustomerStatementLine> lines)
    {
        var rows = lines.Select(l => (
            l.Date, l.DocumentNumber, l.DocumentType, l.Debit, l.Credit, l.RunningBalance)).ToList();
        return Build(PrintBranding.From(companyName, ""), $"كشف حساب عميل — {customerName}",
            $"الرصيد المستحق: {closingBalance:N2} ر.س", rows);
    }

    public static byte[] CustomerStatement(
        PrintBranding branding, string customerName, decimal closingBalance,
        IReadOnlyList<CustomerStatementLine> lines)
    {
        var rows = lines.Select(l => (
            l.Date, l.DocumentNumber, l.DocumentType, l.Debit, l.Credit, l.RunningBalance)).ToList();
        return Build(branding, $"كشف حساب عميل — {customerName}",
            $"الرصيد المستحق: {closingBalance:N2} ر.س", rows);
    }

    public static byte[] SupplierStatement(
        string companyName, string supplierName, decimal closingBalance,
        IReadOnlyList<SupplierStatementLine> lines)
    {
        var rows = lines.Select(l => (
            l.Date, l.DocumentNumber, l.DocumentType, l.Debit, l.Credit, l.RunningBalance)).ToList();
        return Build(PrintBranding.From(companyName, ""), $"كشف حساب مورد — {supplierName}",
            $"الرصيد المستحق: {closingBalance:N2} ر.س", rows);
    }

    public static byte[] SupplierStatement(
        PrintBranding branding, string supplierName, decimal closingBalance,
        IReadOnlyList<SupplierStatementLine> lines)
    {
        var rows = lines.Select(l => (
            l.Date, l.DocumentNumber, l.DocumentType, l.Debit, l.Credit, l.RunningBalance)).ToList();
        return Build(branding, $"كشف حساب مورد — {supplierName}",
            $"الرصيد المستحق: {closingBalance:N2} ر.س", rows);
    }

    private static byte[] Build(
        PrintBranding branding, string title, string footer,
        IReadOnlyList<(DateOnly Date, string Number, string Type, decimal Debit, decimal Credit, decimal Balance)> rows)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => branding.PageTextStyle( 9));

                page.Header().Element(h => branding.RenderHeader(h, title));

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(70);
                        c.ConstantColumn(100);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Text("التاريخ").Bold();
                        h.Cell().Text("رقم المستند").Bold();
                        h.Cell().Text("النوع").Bold();
                        h.Cell().AlignCenter().Text("مدين").Bold();
                        h.Cell().AlignCenter().Text("دائن").Bold();
                        h.Cell().AlignCenter().Text("الرصيد").Bold();
                    });
                    foreach (var (date, number, type, debit, credit, balance) in rows)
                    {
                        table.Cell().Text(date.ToString("yyyy-MM-dd"));
                        table.Cell().Text(number);
                        table.Cell().Text(type);
                        table.Cell().AlignCenter().Text(debit > 0 ? $"{debit:N2}" : "");
                        table.Cell().AlignCenter().Text(credit > 0 ? $"{credit:N2}" : "");
                        table.Cell().AlignCenter().Text($"{balance:N2}");
                    }
                });

                page.Footer().Text(footer);
            });
        }).GeneratePdf();
    }
}
