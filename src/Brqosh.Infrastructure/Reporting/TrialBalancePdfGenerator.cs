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
        using var _lang = PrintLang.Begin(branding.English);
        var totalDebit = rows.Sum(r => r.TotalDebit);
        var totalCredit = rows.Sum(r => r.TotalCredit);

        PrintColumn[] columns =
        [
            new(PrintLang.T("الكود"), FixedWidth: 70),
            new(PrintLang.T("الحساب"), Weight: 3f),
            new(PrintLang.T("مدين"), Numeric: true),
            new(PrintLang.T("دائن"), Numeric: true),
            new(PrintLang.T("رصيد مدين"), Numeric: true),
            new(PrintLang.T("رصيد دائن"), Numeric: true),
        ];

        return PrintReport.Table(branding, PrintLang.T("ميزان المراجعة"),
            PrintLang.TF("من {0} إلى {1}", PrintText.Date(from), PrintText.Date(to)), columns,
            rows.Select(r => (IReadOnlyList<string>)
            [
                PrintText.Code(r.AccountCode), r.AccountNameAr,
                PrintText.Money(r.TotalDebit), PrintText.Money(r.TotalCredit),
                PrintText.Money(r.BalanceDebit), PrintText.Money(r.BalanceCredit),
            ]),
            [
                (PrintLang.T("إجمالي المدين"), PrintText.Money(totalDebit)),
                (PrintLang.T("إجمالي الدائن"), PrintText.Money(totalCredit)),
                (totalDebit == totalCredit ? PrintLang.T("الميزان متوازن") : PrintLang.T("الميزان غير متوازن"), PrintText.Money(totalDebit - totalCredit)),
            ]);
    }
}
