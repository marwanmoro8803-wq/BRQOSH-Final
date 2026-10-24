using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

public static class TrialBalancePdfGenerator
{
    static TrialBalancePdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Generate(
        string companyName, DateOnly from, DateOnly to,
        IReadOnlyList<TrialBalanceRow> rows) =>
        Generate(PrintBranding.From(companyName, ""), from, to, rows);

    public static byte[] Generate(
        PrintBranding branding, DateOnly from, DateOnly to,
        IReadOnlyList<TrialBalanceRow> rows)
    {
        var totalDebit = rows.Sum(r => r.TotalDebit);
        var totalCredit = rows.Sum(r => r.TotalCredit);

        PrintColumn[] columns =
        [
            new("الكود", FixedWidth: 70),
            new("الحساب", Weight: 3f),
            new("مدين", Numeric: true),
            new("دائن", Numeric: true),
            new("رصيد مدين", Numeric: true),
            new("رصيد دائن", Numeric: true),
        ];

        return PrintReport.Table(branding, "ميزان المراجعة",
            $"من {PrintText.Date(from)} إلى {PrintText.Date(to)}", columns,
            rows.Select(r => (IReadOnlyList<string>)
            [
                PrintText.Code(r.AccountCode), r.AccountNameAr,
                PrintText.Money(r.TotalDebit), PrintText.Money(r.TotalCredit),
                PrintText.Money(r.BalanceDebit), PrintText.Money(r.BalanceCredit),
            ]),
            [
                ("إجمالي المدين", PrintText.Money(totalDebit)),
                ("إجمالي الدائن", PrintText.Money(totalCredit)),
                (totalDebit == totalCredit ? "الميزان متوازن" : "الميزان غير متوازن", PrintText.Money(totalDebit - totalCredit)),
            ]);
    }
}
