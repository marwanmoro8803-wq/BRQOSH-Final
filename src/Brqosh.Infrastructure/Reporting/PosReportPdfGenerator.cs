using System.Globalization;
using Brqosh.Infrastructure.Retail;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>PDF — POS daily sales summary.</summary>
public static class PosReportPdfGenerator
{
    static PosReportPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] DailySummary(
        PrintBranding branding,
        IReadOnlyList<PosReportQuery.DailySaleRow> rows)
    {
        using var _lang = PrintLang.Begin(branding.English);
        PrintColumn[] columns =
        [
            new(PrintLang.T("التاريخ")),
            new(PrintLang.T("العمليات"), Numeric: true),
            new(PrintLang.T("المبيعات"), Numeric: true),
            new(PrintLang.T("الضريبة"), Numeric: true),
        ];

        return PrintReport.Table(branding, PrintLang.T("تقرير مبيعات نقاط البيع اليومي"), null, columns,
            rows.Select(r => (IReadOnlyList<string>)
            [
                PrintText.Ltr(r.Date), r.Transactions.ToString(CultureInfo.InvariantCulture),
                PrintText.Money(r.TotalSales), PrintText.Money(r.VatAmount),
            ]),
            [
                (PrintLang.T("إجمالي العمليات"), rows.Sum(r => r.Transactions).ToString(CultureInfo.InvariantCulture)),
                (PrintLang.TF("إجمالي الضريبة ({0})", branding.CurrencyAr), PrintText.Money(rows.Sum(r => r.VatAmount))),
                (PrintLang.TF("إجمالي المبيعات ({0})", branding.CurrencyAr), PrintText.Money(rows.Sum(r => r.TotalSales))),
            ]);
    }
}
