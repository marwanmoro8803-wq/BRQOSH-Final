using Brqosh.Domain.Tax;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>تصدير PDF للتقارير المالية — قائمة الدخل، المركز المالي، دفتر الأستاذ، إقرار VAT.</summary>
public static class FinancialReportPdfGenerator
{
    static FinancialReportPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] IncomeStatement(string companyName, IncomeStatementResult result) =>
        IncomeStatement(PrintBranding.From(companyName, ""), result);

    public static byte[] IncomeStatement(PrintBranding branding, IncomeStatementResult result)
    {
        using var _lang = PrintLang.Begin(branding.English);
        var rows = result.RevenueRows
            .Select(r => (PrintLang.T("إيرادات"), r.AccountCode, r.AccountNameAr, r.Amount))
            .Concat(result.ExpenseRows.Select(r => (PrintLang.T("مصروفات"), r.AccountCode, r.AccountNameAr, r.Amount)))
            .ToList();

        return BuildTableReport(
            branding, PrintLang.T("قائمة الدخل"),
            PrintLang.TF("من {0} إلى {1}", PrintText.Date(result.FromDate), PrintText.Date(result.ToDate)),
            rows,
            [
                (PrintLang.T("إجمالي الإيرادات"), PrintText.Money(result.TotalRevenue)),
                (PrintLang.T("إجمالي المصروفات"), PrintText.Money(result.TotalExpenses)),
                (PrintLang.T("صافي الدخل"), PrintText.Money(result.NetIncome)),
            ]);
    }

    public static byte[] BalanceSheet(string companyName, BalanceSheetResult result) =>
        BalanceSheet(PrintBranding.From(companyName, ""), result);

    public static byte[] BalanceSheet(PrintBranding branding, BalanceSheetResult result)
    {
        using var _lang = PrintLang.Begin(branding.English);
        var rows = result.Assets
            .Select(r => (PrintLang.T("أصول"), r.AccountCode, r.AccountNameAr, r.Amount))
            .Concat(result.Liabilities.Select(r => (PrintLang.T("التزامات"), r.AccountCode, r.AccountNameAr, r.Amount)))
            .Concat(result.Equity.Select(r => (PrintLang.T("حقوق ملكية"), r.AccountCode, r.AccountNameAr, r.Amount)))
            .ToList();

        IReadOnlyList<(string Label, string Value)> summary =
        [
            (PrintLang.T("الأصول"), PrintText.Money(result.TotalAssets)),
            (PrintLang.T("الالتزامات + حقوق الملكية"), PrintText.Money(result.TotalLiabilitiesAndEquity)),
            (result.IsBalanced ? PrintLang.T("المركز متوازن") : PrintLang.T("المركز غير متوازن"),
                PrintText.Money(result.TotalAssets - result.TotalLiabilitiesAndEquity)),
        ];

        return BuildTableReport(branding, PrintLang.T("قائمة المركز المالي"),
            PrintLang.TF("حتى {0}", PrintText.Date(result.AsOfDate)), rows, summary);
    }

    public static byte[] VatReturn(string companyName, VatReturnResult result) =>
        VatReturn(PrintBranding.From(companyName, ""), result);

    public static byte[] VatReturn(PrintBranding branding, VatReturnResult result)
    {
        using var _lang = PrintLang.Begin(branding.English);
        var rows = VatReturnRows(result);

        var label = result.NetVatPayable >= 0 ? PrintLang.T("مستحقة للهيئة") : PrintLang.T("مستردة/مرحّلة");
        return BuildTableReport(branding, PrintLang.T("إقرار ضريبة القيمة المضافة"),
            PrintLang.TF("من {0} إلى {1}", PrintText.Date(result.FromDate), PrintText.Date(result.ToDate)), rows,
            [(PrintLang.TF("صافي الضريبة ({0}) — {1}", label, branding.CurrencyAr), PrintText.Money(result.NetVatPayable))]);
    }

    /// <summary>
    /// <b>سطورُ الإقرار المطبوع — دالّةٌ صافيةٌ تُقاس.</b>
    ///
    /// <para><b>وكان صافيه صحيحاً وسطورُه ناقصة</b>: يطبع صافيَ المخرجات بتسوية الإلغاء
    /// والضريبةِ المقيّدة بلا مستند، <b>ولا يطبع سطراً لأيٍّ منهما</b> — فلا تجمع السطورُ
    /// إلى الصافي تحتها في الوثيقة التي يُقدَّم منها الإقرار. <b>وأُخرجت إلى دالّةٍ لأنّ
    /// محتوى PDF لا يُقرأ في الاختبار</b>، وهو عينُ ما فُعل بصفوف الشاشة في م1-١٠.</para>
    /// </summary>
    public static IReadOnlyList<(string Section, string Code, string Name, decimal Amount)> VatReturnRows(
        VatReturnResult result)
    {
        var rows = new List<(string Section, string Code, string Name, decimal Amount)>
        {
            (PrintLang.T("مبيعات"), "—", PrintLang.T("مبيعات خاضعة (وعاء)"), result.SalesSubtotal),
            (PrintLang.T("مبيعات"), "—", PrintLang.T("ضريبة المخرجات — مبيعات"), result.OutputVat),
            (PrintLang.T("مستخلصات"), "—", PrintLang.T("مستخلصات أعمال (وعاء)"), result.ProgressBillingsSubtotal),
            (PrintLang.T("مستخلصات"), "—", PrintLang.T("ضريبة المخرجات — مستخلصات"), result.ProgressBillingsVat),
            (PrintLang.T("مردودات"), "—", PrintLang.T("إشعارات دائن (وعاء)"), result.CreditNotesSubtotal),
            (PrintLang.T("مردودات"), "—", PrintLang.T("ضريبة مرتجعة"), result.CreditNotesVat),
            (PrintLang.T("تسويات"), "—", PrintLang.T("تسوية إلغاء مستندات فترات سابقة (تُطرح)"), result.CancelledOutputVat),
            (PrintLang.T("تسويات"), "—", PrintLang.T("ضريبة مخرجات مقيّدة بلا مستند (تُضاف)"), result.JournalOutputVat),
            (PrintLang.T("مبيعات"), "—", PrintLang.T("صافي ضريبة المخرجات"), result.NetOutputVat),
        };
        AddBreakdown(rows, PrintLang.T("مبيعات حسب الفئة"), PrintLang.T("صافي وعاء المبيعات (من المستندات)"),
            result.SalesBaseNet, result.Sales, result.JournalOutputVat);

        rows.AddRange(
        [
            (PrintLang.T("مشتريات"), "—", PrintLang.T("مشتريات (وعاء)"), result.PurchasesSubtotal),
            (PrintLang.T("مشتريات"), "—", PrintLang.T("ضريبة مدخلات قابلة للخصم"), result.DeductibleInputVat),
            (PrintLang.T("مردودات مشتريات"), "—", PrintLang.T("إشعارات مدينة (وعاء)"), result.DebitNotesSubtotal),
            (PrintLang.T("مردودات مشتريات"), "—", PrintLang.T("ضريبة مدخلات مُخفّضة"), result.DebitNotesDeductibleVat),
            (PrintLang.T("تسويات"), "—", PrintLang.T("تسوية إلغاء مشتريات فترات سابقة (تُطرح)"), result.CancelledInputVat),
            (PrintLang.T("تسويات"), "—", PrintLang.T("ضريبة مدخلات مقيّدة بلا مستند (تُضاف)"), result.JournalInputVat),
            (PrintLang.T("مشتريات"), "—", PrintLang.T("صافي ضريبة المدخلات"), result.NetDeductibleInputVat),
        ]);
        AddBreakdown(rows, PrintLang.T("مشتريات حسب الفئة"), PrintLang.T("صافي وعاء المشتريات القابلة للخصم (من المستندات)"),
            result.PurchasesBaseNet, result.Purchases, result.JournalInputVat);

        return rows;
    }

    /// <summary>
    /// <b>والمطبوعُ يحمل الفئاتِ كالشاشة</b> — وكان
    /// بلا تفكيكٍ أصلاً، <b>وهو الوثيقةُ التي يُقدَّم منها الإقرار</b>. ضريبةُ كلّ فئةٍ تحت
    /// الصافي فوقها ومعها «بلا فئة» لِما قُيّد بلا مستند؛ ثمّ صافي الوعاء وأوعيتُه تحته.
    /// </summary>
    private static void AddBreakdown(
        List<(string Section, string Code, string Name, decimal Amount)> rows,
        string section, string baseLabel, decimal baseNet,
        IReadOnlyList<VatReturnBucket> buckets, decimal uncategorisedVat)
    {
        foreach (var b in buckets.Where(b => b.VatAmount != 0m))
            rows.Add((section, b.Category.UblCode(), PrintLang.TF("منها ضريبة {0}", PrintLang.T(b.Category.NameAr())), b.VatAmount));
        if (uncategorisedVat != 0m)
            rows.Add((section, "—", PrintLang.T("منها بلا فئة — مقيّدة بلا مستند"), uncategorisedVat));

        rows.Add((section, "—", baseLabel, baseNet));
        foreach (var b in buckets)
            rows.Add((section, b.Category.UblCode(), PrintLang.TF("منها وعاء {0}", PrintLang.T(b.Category.NameAr())), b.TaxableAmount));
    }

    public static byte[] VatReturn(string companyName, DateOnly from, DateOnly to,
        decimal outputVat, decimal inputVat, decimal netPayable) =>
        VatReturn(PrintBranding.From(companyName, ""), from, to, outputVat, inputVat, netPayable);

    public static byte[] VatReturn(PrintBranding branding, DateOnly from, DateOnly to,
        decimal outputVat, decimal inputVat, decimal netPayable)
    {
        using var _lang = PrintLang.Begin(branding.English);
        var rows = new List<(string Section, string Code, string Name, decimal Amount)>
        {
            (PrintLang.T("ضريبة مخرجات"), "—", PrintLang.T("ضريبة المبيعات (مخرجات)"), outputVat),
            (PrintLang.T("ضريبة مدخلات"), "—", PrintLang.T("ضريبة المشتريات (مدخلات)"), inputVat),
        };

        return BuildTableReport(branding, PrintLang.T("إقرار ضريبة القيمة المضافة"),
            PrintLang.TF("من {0} إلى {1}", PrintText.Date(from), PrintText.Date(to)), rows,
            [
                (PrintLang.T("ضريبة المخرجات"), PrintText.Money(outputVat)),
                (PrintLang.T("ضريبة المدخلات"), PrintText.Money(inputVat)),
                (PrintLang.TF("صافي الضريبة المستحقة — {0}", branding.CurrencyAr), PrintText.Money(netPayable)),
            ]);
    }

    public static byte[] CashFlowStatement(string companyName, CashFlowStatementResult result) =>
        CashFlowStatement(PrintBranding.From(companyName, ""), result);

    public static byte[] CashFlowStatement(PrintBranding branding, CashFlowStatementResult result)
    {
        using var _lang = PrintLang.Begin(branding.English);
        var rows = new List<(string Section, string Label, decimal Amount)>();
        AppendSection(rows, result.Operating);
        AppendSection(rows, result.Investing);
        AppendSection(rows, result.Financing);

        PrintColumn[] columns =
        [
            new(PrintLang.T("القسم"), Weight: 1f),
            new(PrintLang.T("البند"), Weight: 2f),
            new(PrintLang.T("المبلغ"), Numeric: true),
        ];

        return PrintReport.Table(branding, PrintLang.T("قائمة التدفقات النقدية"),
            PrintLang.TF("من {0} إلى {1}", PrintText.Date(result.FromDate), PrintText.Date(result.ToDate)), columns,
            rows.Select(r => (IReadOnlyList<string>)[r.Section, r.Label, PrintText.Money(r.Amount)]),
            [
                (PrintLang.T("الرصيد الافتتاحي"), PrintText.Money(result.OpeningCash)),
                (PrintLang.T("صافي التغيّر"), PrintText.Money(result.NetCashChange)),
                (PrintLang.T("الرصيد الختامي"), PrintText.Money(result.ClosingCash)),
            ]);
    }

    private static void AppendSection(
        List<(string Section, string Label, decimal Amount)> rows, CashFlowSection section)
    {
        foreach (var line in section.Lines)
            rows.Add((PrintLang.T(section.NameAr), PrintLang.T(line.Label), line.Amount));
        rows.Add((PrintLang.T(section.NameAr), PrintLang.TF("إجمالي {0}", PrintLang.T(section.NameAr)), section.Subtotal));
    }

    public static byte[] GeneralLedger(string companyName, DateOnly from, DateOnly to,
        IReadOnlyList<(DateOnly EntryDate, string AccountCode, string AccountNameAr,
            decimal Debit, decimal Credit, string Description)> rows) =>
        GeneralLedger(PrintBranding.From(companyName, ""), from, to, rows);

    public static byte[] GeneralLedger(PrintBranding branding, DateOnly from, DateOnly to,
        IReadOnlyList<(DateOnly EntryDate, string AccountCode, string AccountNameAr,
            decimal Debit, decimal Credit, string Description)> rows)
    {
        using var _lang = PrintLang.Begin(branding.English);
        PrintColumn[] columns =
        [
            new(PrintLang.T("التاريخ"), FixedWidth: 76),
            new(PrintLang.T("الكود"), FixedWidth: 64),
            new(PrintLang.T("الحساب"), Weight: 2f),
            new(PrintLang.T("مدين"), Numeric: true),
            new(PrintLang.T("دائن"), Numeric: true),
            new(PrintLang.T("البيان"), Weight: 2.2f),
        ];

        return PrintReport.Table(branding, PrintLang.T("دفتر الأستاذ العام"),
            PrintLang.TF("من {0} إلى {1}", PrintText.Date(from), PrintText.Date(to)), columns,
            rows.Select(r => (IReadOnlyList<string>)
            [
                PrintText.Date(r.EntryDate), PrintText.Code(r.AccountCode), r.AccountNameAr,
                r.Debit > 0 ? PrintText.Money(r.Debit) : "", r.Credit > 0 ? PrintText.Money(r.Credit) : "",
                r.Description,
            ]),
            [
                (PrintLang.T("إجمالي المدين"), PrintText.Money(rows.Sum(r => r.Debit))),
                (PrintLang.T("إجمالي الدائن"), PrintText.Money(rows.Sum(r => r.Credit))),
            ],
            landscape: true, fontSize: 9);
    }

    private static byte[] BuildTableReport(
        PrintBranding branding, string title, string subtitle,
        IReadOnlyList<(string Section, string Code, string Name, decimal Amount)> rows,
        IReadOnlyList<(string Label, string Value)> summary)
    {
        PrintColumn[] columns =
        [
            new(PrintLang.T("القسم"), Weight: 1f),
            new(PrintLang.T("الكود"), FixedWidth: 70),
            new(PrintLang.T("الحساب"), Weight: 3f),
            new(PrintLang.T("المبلغ"), Numeric: true),
        ];

        return PrintReport.Table(branding, title, subtitle, columns,
            rows.Select(r => (IReadOnlyList<string>)[r.Section, PrintText.Code(r.Code), r.Name, PrintText.Money(r.Amount)]),
            summary);
    }
}
