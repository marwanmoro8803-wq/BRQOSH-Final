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
            $"إجمالي الإيرادات: {result.TotalRevenue:N2}  |  إجمالي المصروفات: {result.TotalExpenses:N2}  |  صافي الدخل: {result.NetIncome:N2}");
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

        var footer = $"الأصول: {result.TotalAssets:N2}  |  الالتزامات + حقوق الملكية: {result.TotalLiabilitiesAndEquity:N2}  |  " +
                     (result.IsBalanced ? "متوازنة ✓" : "غير متوازنة ✗");

        return BuildTableReport(branding, "قائمة المركز المالي",
            $"حتى {PrintText.Date(result.AsOfDate)}", rows, footer);
    }

    public static byte[] VatReturn(string companyName, VatReturnResult result) =>
        VatReturn(PrintBranding.From(companyName, ""), result);

    public static byte[] VatReturn(PrintBranding branding, VatReturnResult result)
    {
        var rows = VatReturnRows(result);

        var label = result.NetVatPayable >= 0 ? "مستحقة للهيئة" : "مستردة/مرحّلة";
        return BuildTableReport(branding, "إقرار ضريبة القيمة المضافة",
            $"من {PrintText.Date(result.FromDate)} إلى {PrintText.Date(result.ToDate)}", rows,
            $"صافي الضريبة ({label}): {result.NetVatPayable:N2} ر.س");
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
        VatReturnResult result) =>
    [
        ("مبيعات", "—", "مبيعات خاضعة (وعاء)", result.SalesSubtotal),
        ("مبيعات", "—", "ضريبة المخرجات — مبيعات", result.OutputVat),
        ("مستخلصات", "—", "مستخلصات أعمال (وعاء)", result.ProgressBillingsSubtotal),
        ("مستخلصات", "—", "ضريبة المخرجات — مستخلصات", result.ProgressBillingsVat),
        ("مردودات", "—", "إشعارات دائن (وعاء)", result.CreditNotesSubtotal),
        ("مردودات", "—", "ضريبة مرتجعة", result.CreditNotesVat),
        ("تسويات", "—", "تسوية إلغاء مستندات فترات سابقة (تُطرح)", result.CancelledOutputVat),
        ("تسويات", "—", "ضريبة مخرجات مقيّدة بلا مستند (تُضاف)", result.JournalOutputVat),
        ("مبيعات", "—", "صافي ضريبة المخرجات", result.NetOutputVat),
        ("مشتريات", "—", "مشتريات (وعاء)", result.PurchasesSubtotal),
        ("مشتريات", "—", "ضريبة مدخلات قابلة للخصم", result.DeductibleInputVat),
        ("مردودات مشتريات", "—", "إشعارات مدينة (وعاء)", result.DebitNotesSubtotal),
        ("مردودات مشتريات", "—", "ضريبة مدخلات مُخفّضة", result.DebitNotesDeductibleVat),
        ("تسويات", "—", "تسوية إلغاء مشتريات فترات سابقة (تُطرح)", result.CancelledInputVat),
        ("تسويات", "—", "ضريبة مدخلات مقيّدة بلا مستند (تُضاف)", result.JournalInputVat),
        ("مشتريات", "—", "صافي ضريبة المدخلات", result.NetDeductibleInputVat),
    ];

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
            $"صافي الضريبة المستحقة: {netPayable:N2} ر.س");
    }

    public static byte[] CashFlowStatement(string companyName, CashFlowStatementResult result) =>
        CashFlowStatement(PrintBranding.From(companyName, ""), result);

    public static byte[] CashFlowStatement(PrintBranding branding, CashFlowStatementResult result)
    {
        var rows = new List<(string Section, string Label, decimal Amount)>();
        AppendSection(rows, result.Operating);
        AppendSection(rows, result.Investing);
        AppendSection(rows, result.Financing);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => branding.PageTextStyle( 10));

                page.Header().Element(h => branding.RenderHeader(
                    h, "قائمة التدفقات النقدية",
                    $"من {PrintText.Date(result.FromDate)} إلى {PrintText.Date(result.ToDate)}"));

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                        c.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Text("القسم").Bold();
                        h.Cell().Text("البند").Bold();
                        h.Cell().AlignCenter().Text("المبلغ").Bold();
                    });
                    foreach (var (section, label, amount) in rows)
                    {
                        table.Cell().Text(section);
                        table.Cell().Text(label);
                        table.Cell().AlignCenter().Text($"{amount:N2}");
                    }
                });

                page.Footer().Text(
                    $"صافي التغيّر: {result.NetCashChange:N2} · افتتاحي: {result.OpeningCash:N2} · إغلاق: {result.ClosingCash:N2}");
            });
        }).GeneratePdf();
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
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => branding.PageTextStyle( 9));

                page.Header().Element(h => branding.RenderHeader(
                    h, "دفتر الأستاذ العام", $"من {PrintText.Date(from)} إلى {PrintText.Date(to)}"));

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(70);
                        c.ConstantColumn(60);
                        c.RelativeColumn(2);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Text("التاريخ").Bold();
                        h.Cell().Text("الكود").Bold();
                        h.Cell().Text("الحساب").Bold();
                        h.Cell().AlignCenter().Text("مدين").Bold();
                        h.Cell().AlignCenter().Text("دائن").Bold();
                        h.Cell().Text("البيان").Bold();
                    });
                    foreach (var (date, code, name, debit, credit, desc) in rows)
                    {
                        table.Cell().Text(PrintText.Date(date));
                        table.Cell().Text(code);
                        table.Cell().Text(name);
                        table.Cell().AlignCenter().Text(debit > 0 ? $"{debit:N2}" : "");
                        table.Cell().AlignCenter().Text(credit > 0 ? $"{credit:N2}" : "");
                        table.Cell().Text(desc);
                    }
                });

                page.Footer().AlignCenter().Text(
                    $"إجمالي المدين: {rows.Sum(r => r.Debit):N2}  |  إجمالي الدائن: {rows.Sum(r => r.Credit):N2}");
            });
        }).GeneratePdf();
    }

    private static byte[] BuildTableReport(
        PrintBranding branding, string title, string subtitle,
        IReadOnlyList<(string Section, string Code, string Name, decimal Amount)> rows,
        string footer)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => branding.PageTextStyle( 10));

                page.Header().Element(h => branding.RenderHeader(h, title, subtitle));

                page.Content().PaddingVertical(10).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1);
                        c.ConstantColumn(70);
                        c.RelativeColumn(3);
                        c.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Text("القسم").Bold();
                        h.Cell().Text("الكود").Bold();
                        h.Cell().Text("الحساب").Bold();
                        h.Cell().AlignCenter().Text("المبلغ").Bold();
                    });
                    foreach (var (section, code, name, amount) in rows)
                    {
                        table.Cell().Text(section);
                        table.Cell().Text(code);
                        table.Cell().Text(name);
                        table.Cell().AlignCenter().Text($"{amount:N2}");
                    }
                });

                page.Footer().Text(footer);
            });
        }).GeneratePdf();
    }
}
