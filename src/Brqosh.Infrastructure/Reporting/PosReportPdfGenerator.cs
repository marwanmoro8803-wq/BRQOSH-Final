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
        PrintColumn[] columns =
        [
            new("التاريخ"),
            new("العمليات", Numeric: true),
            new("المبيعات", Numeric: true),
            new("الضريبة", Numeric: true),
        ];

        return PrintReport.Table(branding, "تقرير مبيعات نقاط البيع اليومي", null, columns,
            rows.Select(r => (IReadOnlyList<string>)
            [
                PrintText.Ltr(r.Date), r.Transactions.ToString(CultureInfo.InvariantCulture),
                PrintText.Money(r.TotalSales), PrintText.Money(r.VatAmount),
            ]),
            [
                ("إجمالي العمليات", rows.Sum(r => r.Transactions).ToString(CultureInfo.InvariantCulture)),
                ($"إجمالي الضريبة ({branding.CurrencyAr})", PrintText.Money(rows.Sum(r => r.VatAmount))),
                ($"إجمالي المبيعات ({branding.CurrencyAr})", PrintText.Money(rows.Sum(r => r.TotalSales))),
            ]);
    }
}
