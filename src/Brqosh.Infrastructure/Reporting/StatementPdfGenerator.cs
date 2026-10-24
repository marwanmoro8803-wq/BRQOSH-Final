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
        return Build(PrintBranding.From(companyName, ""), "كشف حساب عميل", "العميل", customerName,
            closingBalance, rows);
    }

    public static byte[] CustomerStatement(
        PrintBranding branding, string customerName, decimal closingBalance,
        IReadOnlyList<CustomerStatementLine> lines)
    {
        var rows = lines.Select(l => (
            l.Date, l.DocumentNumber, l.DocumentType, l.Debit, l.Credit, l.RunningBalance)).ToList();
        return Build(branding, "كشف حساب عميل", "العميل", customerName,
            closingBalance, rows);
    }

    public static byte[] SupplierStatement(
        string companyName, string supplierName, decimal closingBalance,
        IReadOnlyList<SupplierStatementLine> lines)
    {
        var rows = lines.Select(l => (
            l.Date, l.DocumentNumber, l.DocumentType, l.Debit, l.Credit, l.RunningBalance)).ToList();
        return Build(PrintBranding.From(companyName, ""), "كشف حساب مورد", "المورد", supplierName,
            closingBalance, rows);
    }

    public static byte[] SupplierStatement(
        PrintBranding branding, string supplierName, decimal closingBalance,
        IReadOnlyList<SupplierStatementLine> lines)
    {
        var rows = lines.Select(l => (
            l.Date, l.DocumentNumber, l.DocumentType, l.Debit, l.Credit, l.RunningBalance)).ToList();
        return Build(branding, "كشف حساب مورد", "المورد", supplierName,
            closingBalance, rows);
    }

    private static byte[] Build(
        PrintBranding branding, string title, string partyLabel, string partyName, decimal closingBalance,
        IReadOnlyList<(DateOnly Date, string Number, string Type, decimal Debit, decimal Credit, decimal Balance)> rows)
    {
        // بترتيب القراءة: التاريخ أقصى اليمين ثمّ الرصيدُ أقصى اليسار.
        PrintColumn[] columns =
        [
            new("التاريخ", FixedWidth: 76),
            new("رقم المستند", FixedWidth: 108),
            new("النوع", Weight: 1.4f),
            new("مدين", Numeric: true),
            new("دائن", Numeric: true),
            new("الرصيد", Numeric: true),
        ];

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => branding.PageTextStyle(9));

                page.Header().Element(h => branding.RenderHeader(h, title));

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Card().Element(c => PrintTheme.LabelValue(c, partyLabel, partyName, emphasizeValue: true));
                    col.Item().Element(c => PrintTheme.ReportTable(c, columns, rows.Select(r => (IReadOnlyList<string>)
                    [
                        PrintText.Date(r.Date),
                        PrintText.Code(r.Number),
                        r.Type,
                        r.Debit > 0 ? PrintText.Money(r.Debit) : "",
                        r.Credit > 0 ? PrintText.Money(r.Credit) : "",
                        PrintText.Money(r.Balance),
                    ])));

                    col.Item().Element(c => PrintTheme.SummaryBand(c,
                    [
                        ("إجمالي المدين", PrintText.Money(rows.Sum(r => r.Debit))),
                        ("إجمالي الدائن", PrintText.Money(rows.Sum(r => r.Credit))),
                        ("الرصيد المستحق (ر.س)", PrintText.Money(closingBalance)),
                    ]));

                    col.Item().Element(c => PrintTheme.AmountInWords(c, closingBalance));
                });

                page.Footer().Element(f => branding.RenderFooterMeta(f));
            });
        }).GeneratePdf();
    }
}
