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
        return Build(PrintBranding.From(companyName, ""), PrintLang.T("كشف حساب عميل"), PrintLang.T("العميل"), customerName,
            closingBalance, rows);
    }

    public static byte[] CustomerStatement(
        PrintBranding branding, string customerName, decimal closingBalance,
        IReadOnlyList<CustomerStatementLine> lines)
    {
        using var _lang = PrintLang.Begin(branding.English);
        var rows = lines.Select(l => (
            l.Date, l.DocumentNumber, l.DocumentType, l.Debit, l.Credit, l.RunningBalance)).ToList();
        return Build(branding, PrintLang.T("كشف حساب عميل"), PrintLang.T("العميل"), customerName,
            closingBalance, rows);
    }

    public static byte[] SupplierStatement(
        string companyName, string supplierName, decimal closingBalance,
        IReadOnlyList<SupplierStatementLine> lines)
    {
        var rows = lines.Select(l => (
            l.Date, l.DocumentNumber, l.DocumentType, l.Debit, l.Credit, l.RunningBalance)).ToList();
        return Build(PrintBranding.From(companyName, ""), PrintLang.T("كشف حساب مورد"), PrintLang.T("المورد"), supplierName,
            closingBalance, rows);
    }

    public static byte[] SupplierStatement(
        PrintBranding branding, string supplierName, decimal closingBalance,
        IReadOnlyList<SupplierStatementLine> lines)
    {
        using var _lang = PrintLang.Begin(branding.English);
        var rows = lines.Select(l => (
            l.Date, l.DocumentNumber, l.DocumentType, l.Debit, l.Credit, l.RunningBalance)).ToList();
        return Build(branding, PrintLang.T("كشف حساب مورد"), PrintLang.T("المورد"), supplierName,
            closingBalance, rows);
    }

    private static byte[] Build(
        PrintBranding branding, string title, string partyLabel, string partyName, decimal closingBalance,
        IReadOnlyList<(DateOnly Date, string Number, string Type, decimal Debit, decimal Credit, decimal Balance)> rows)
    {
        // بترتيب القراءة: التاريخ أقصى اليمين ثمّ الرصيدُ أقصى اليسار.
        PrintColumn[] columns =
        [
            new(PrintLang.T("التاريخ"), FixedWidth: 76),
            new(PrintLang.T("رقم المستند"), FixedWidth: 108),
            new(PrintLang.T("النوع"), Weight: 1.4f),
            new(PrintLang.T("مدين"), Numeric: true),
            new(PrintLang.T("دائن"), Numeric: true),
            new(PrintLang.T("الرصيد"), Numeric: true),
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
                        PrintLang.T(r.Type),
                        r.Debit > 0 ? PrintText.Money(r.Debit) : "",
                        r.Credit > 0 ? PrintText.Money(r.Credit) : "",
                        PrintText.Money(r.Balance),
                    ])));

                    col.Item().Element(c => PrintTheme.SummaryBand(c,
                    [
                        (PrintLang.T("إجمالي المدين"), PrintText.Money(rows.Sum(r => r.Debit))),
                        (PrintLang.T("إجمالي الدائن"), PrintText.Money(rows.Sum(r => r.Credit))),
                        (PrintLang.TF("الرصيد المستحق ({0})", branding.CurrencyAr), PrintText.Money(closingBalance)),
                    ]));

                    col.Item().Element(c => PrintTheme.AmountInWords(c, closingBalance, branding.CurrencyCode));
                });

                page.Footer().Element(f => branding.RenderFooterMeta(f, showAuditor: true));
            });
        }).GeneratePdf();
    }
}
