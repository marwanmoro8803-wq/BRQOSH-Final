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
        var rows = result.RevenueRows
            .Select(r => ("إيرادات", r.AccountCode, r.AccountNameAr, r.Amount))
            .Concat(result.ExpenseRows.Select(r => ("مصروفات", r.AccountCode, r.AccountNameAr, r.Amount)))
            .ToList();

        return BuildTableReport(
            branding, "قائمة الدخل",
            $"من {PrintText.Date(result.FromDate)} إلى {PrintText.Date(result.ToDate)}",
            rows,
            [
                ("إجمالي الإيرادات", PrintText.Money(result.TotalRevenue)),
                ("إجمالي المصروفات", PrintText.Money(result.TotalExpenses)),
                ("صافي الدخل", PrintText.Money(result.NetIncome)),
            ]);
    }

    public static byte[] BalanceSheet(string companyName, BalanceSheetResult result) =>
        BalanceSheet(PrintBranding.From(companyName, ""), result);

    public static byte[] BalanceSheet(PrintBranding branding, BalanceSheetResult result)
    {
        var rows = result.Assets
            .Select(r => ("أصول", r.AccountCode, r.AccountNameAr, r.Amount))
            .Concat(result.Liabilities.Select(r => ("التزامات", r.AccountCode, r.AccountNameAr, r.Amount)))
            .Concat(result.Equity.Select(r => ("حقوق ملكية", r.AccountCode, r.AccountNameAr, r.Amount)))
            .ToList();

        IReadOnlyList<(string Label, string Value)> summary =
        [
            ("الأصول", PrintText.Money(result.TotalAssets)),
            ("الالتزامات + حقوق الملكية", PrintText.Money(result.TotalLiabilitiesAndEquity)),
            (result.IsBalanced ? "المركز متوازن" : "المركز غير متوازن",
                PrintText.Money(result.TotalAssets - result.TotalLiabilitiesAndEquity)),
        ];

        return BuildTableReport(branding, "قائمة المركز المالي",
            $"حتى {PrintText.Date(result.AsOfDate)}", rows, summary);
    }

    public static byte[] VatReturn(string companyName, VatReturnResult result) =>
        VatReturn(PrintBranding.From(companyName, ""), result);

    public static byte[] VatReturn(PrintBranding branding, VatReturnResult result)
    {
        var rows = VatReturnRows(result);

        var label = result.NetVatPayable >= 0 ? "مستحقة للهيئة" : "مستردة/مرحّلة";
        return BuildTableReport(branding, "إقرار ضريبة القيمة المضافة",
            $"من {PrintText.Date(result.FromDate)} إلى {PrintText.Date(result.ToDate)}", rows,
            [($"صافي الضريبة ({label}) — {branding.CurrencyAr}", PrintText.Money(result.NetVatPayable))]);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٤٠) <b>سطورُ الإقرار المطبوع — دالّةٌ صافيةٌ تُقاس.</b>
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
            ("مبيعات", "—", "مبيعات خاضعة (وعاء)", result.SalesSubtotal),
            ("مبيعات", "—", "ضريبة المخرجات — مبيعات", result.OutputVat),
            ("مستخلصات", "—", "مستخلصات أعمال (وعاء)", result.ProgressBillingsSubtotal),
            ("مستخلصات", "—", "ضريبة المخرجات — مستخلصات", result.ProgressBillingsVat),
            ("مردودات", "—", "إشعارات دائن (وعاء)", result.CreditNotesSubtotal),
            ("مردودات", "—", "ضريبة مرتجعة", result.CreditNotesVat),
            ("تسويات", "—", "تسوية إلغاء مستندات فترات سابقة (تُطرح)", result.CancelledOutputVat),
            ("تسويات", "—", "ضريبة مخرجات مقيّدة بلا مستند (تُضاف)", result.JournalOutputVat),
            ("مبيعات", "—", "صافي ضريبة المخرجات", result.NetOutputVat),
        };
        AddBreakdown(rows, "مبيعات حسب الفئة", "صافي وعاء المبيعات (من المستندات)",
            result.SalesBaseNet, result.Sales, result.JournalOutputVat);

        rows.AddRange(
        [
            ("مشتريات", "—", "مشتريات (وعاء)", result.PurchasesSubtotal),
            ("مشتريات", "—", "ضريبة مدخلات قابلة للخصم", result.DeductibleInputVat),
            ("مردودات مشتريات", "—", "إشعارات مدينة (وعاء)", result.DebitNotesSubtotal),
            ("مردودات مشتريات", "—", "ضريبة مدخلات مُخفّضة", result.DebitNotesDeductibleVat),
            ("تسويات", "—", "تسوية إلغاء مشتريات فترات سابقة (تُطرح)", result.CancelledInputVat),
            ("تسويات", "—", "ضريبة مدخلات مقيّدة بلا مستند (تُضاف)", result.JournalInputVat),
            ("مشتريات", "—", "صافي ضريبة المدخلات", result.NetDeductibleInputVat),
        ]);
        AddBreakdown(rows, "مشتريات حسب الفئة", "صافي وعاء المشتريات القابلة للخصم (من المستندات)",
            result.PurchasesBaseNet, result.Purchases, result.JournalInputVat);

        return rows;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٤١ · tax-zatca#12) <b>والمطبوعُ يحمل الفئاتِ كالشاشة</b> — وكان
    /// بلا تفكيكٍ أصلاً، <b>وهو الوثيقةُ التي يُقدَّم منها الإقرار</b>. ضريبةُ كلّ فئةٍ تحت
    /// الصافي فوقها ومعها «بلا فئة» لِما قُيّد بلا مستند؛ ثمّ صافي الوعاء وأوعيتُه تحته.
    /// </summary>
    private static void AddBreakdown(
        List<(string Section, string Code, string Name, decimal Amount)> rows,
        string section, string baseLabel, decimal baseNet,
        IReadOnlyList<VatReturnBucket> buckets, decimal uncategorisedVat)
    {
        foreach (var b in buckets.Where(b => b.VatAmount != 0m))
            rows.Add((section, b.Category.UblCode(), $"منها ضريبة {b.Category.NameAr()}", b.VatAmount));
        if (uncategorisedVat != 0m)
            rows.Add((section, "—", "منها بلا فئة — مقيّدة بلا مستند", uncategorisedVat));

        rows.Add((section, "—", baseLabel, baseNet));
        foreach (var b in buckets)
            rows.Add((section, b.Category.UblCode(), $"منها وعاء {b.Category.NameAr()}", b.TaxableAmount));
    }

    public static byte[] VatReturn(string companyName, DateOnly from, DateOnly to,
        decimal outputVat, decimal inputVat, decimal netPayable) =>
        VatReturn(PrintBranding.From(companyName, ""), from, to, outputVat, inputVat, netPayable);

    public static byte[] VatReturn(PrintBranding branding, DateOnly from, DateOnly to,
        decimal outputVat, decimal inputVat, decimal netPayable)
    {
        var rows = new List<(string Section, string Code, string Name, decimal Amount)>
        {
            ("ضريبة مخرجات", "—", "ضريبة المبيعات (مخرجات)", outputVat),
            ("ضريبة مدخلات", "—", "ضريبة المشتريات (مدخلات)", inputVat),
        };

        return BuildTableReport(branding, "إقرار ضريبة القيمة المضافة",
            $"من {PrintText.Date(from)} إلى {PrintText.Date(to)}", rows,
            [
                ("ضريبة المخرجات", PrintText.Money(outputVat)),
                ("ضريبة المدخلات", PrintText.Money(inputVat)),
                ($"صافي الضريبة المستحقة — {branding.CurrencyAr}", PrintText.Money(netPayable)),
            ]);
    }

    public static byte[] CashFlowStatement(string companyName, CashFlowStatementResult result) =>
        CashFlowStatement(PrintBranding.From(companyName, ""), result);

    public static byte[] CashFlowStatement(PrintBranding branding, CashFlowStatementResult result)
    {
        var rows = new List<(string Section, string Label, decimal Amount)>();
        AppendSection(rows, result.Operating);
        AppendSection(rows, result.Investing);
        AppendSection(rows, result.Financing);

        PrintColumn[] columns =
        [
            new("القسم", Weight: 1f),
            new("البند", Weight: 2f),
            new("المبلغ", Numeric: true),
        ];

        return PrintReport.Table(branding, "قائمة التدفقات النقدية",
            $"من {PrintText.Date(result.FromDate)} إلى {PrintText.Date(result.ToDate)}", columns,
            rows.Select(r => (IReadOnlyList<string>)[r.Section, r.Label, PrintText.Money(r.Amount)]),
            [
                ("الرصيد الافتتاحي", PrintText.Money(result.OpeningCash)),
                ("صافي التغيّر", PrintText.Money(result.NetCashChange)),
                ("الرصيد الختامي", PrintText.Money(result.ClosingCash)),
            ]);
    }

    private static void AppendSection(
        List<(string Section, string Label, decimal Amount)> rows, CashFlowSection section)
    {
        foreach (var line in section.Lines)
            rows.Add((section.NameAr, line.Label, line.Amount));
        rows.Add((section.NameAr, $"إجمالي {section.NameAr}", section.Subtotal));
    }

    public static byte[] GeneralLedger(string companyName, DateOnly from, DateOnly to,
        IReadOnlyList<(DateOnly EntryDate, string AccountCode, string AccountNameAr,
            decimal Debit, decimal Credit, string Description)> rows) =>
        GeneralLedger(PrintBranding.From(companyName, ""), from, to, rows);

    public static byte[] GeneralLedger(PrintBranding branding, DateOnly from, DateOnly to,
        IReadOnlyList<(DateOnly EntryDate, string AccountCode, string AccountNameAr,
            decimal Debit, decimal Credit, string Description)> rows)
    {
        PrintColumn[] columns =
        [
            new("التاريخ", FixedWidth: 76),
            new("الكود", FixedWidth: 64),
            new("الحساب", Weight: 2f),
            new("مدين", Numeric: true),
            new("دائن", Numeric: true),
            new("البيان", Weight: 2.2f),
        ];

        return PrintReport.Table(branding, "دفتر الأستاذ العام",
            $"من {PrintText.Date(from)} إلى {PrintText.Date(to)}", columns,
            rows.Select(r => (IReadOnlyList<string>)
            [
                PrintText.Date(r.EntryDate), PrintText.Code(r.AccountCode), r.AccountNameAr,
                r.Debit > 0 ? PrintText.Money(r.Debit) : "", r.Credit > 0 ? PrintText.Money(r.Credit) : "",
                r.Description,
            ]),
            [
                ("إجمالي المدين", PrintText.Money(rows.Sum(r => r.Debit))),
                ("إجمالي الدائن", PrintText.Money(rows.Sum(r => r.Credit))),
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
            new("القسم", Weight: 1f),
            new("الكود", FixedWidth: 70),
            new("الحساب", Weight: 3f),
            new("المبلغ", Numeric: true),
        ];

        return PrintReport.Table(branding, title, subtitle, columns,
            rows.Select(r => (IReadOnlyList<string>)[r.Section, PrintText.Code(r.Code), r.Name, PrintText.Money(r.Amount)]),
            summary);
    }
}
