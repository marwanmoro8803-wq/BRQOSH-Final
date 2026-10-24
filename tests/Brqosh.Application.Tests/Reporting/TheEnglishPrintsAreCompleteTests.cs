using System.Text.RegularExpressions;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Sales;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · «هل الترجمةُ والاتجاهُ تشمل الفواتيرَ والجداولَ المخرَجة؟») <b>المطبوعاتُ تخرج بالإنجليزيّة (LTR) كاملةً</b> متى طُلب ذلك،
/// والفاتورةُ الضريبيّةُ وحدَها تبقى عربيّةً (الهيئة). ثلاثةُ حرّاس:
/// <list type="number">
/// <item>كلُّ تسميةٍ عربيّةٍ في مولِّدٍ تمرّ بـ<c>PrintLang.T/TF</c> ولها مقابلٌ في القاموس (قراءةُ المصدر — مولِّدٌ جديدٌ بنصٍّ حرفيٍّ يسقط).</item>
/// <item>كلُّ مولِّدٍ يُنتج PDF إنجليزيّاً فعلاً <b>ولا يمرّ به نصٌّ عربيٌّ بلا مقابل</b> (<c>PrintLang.Misses</c>).</item>
/// <item>المبلغُ بالحروف الإنجليزيّة بقيمٍ مرجعيّةٍ مستقلّة.</item>
/// </list>
/// </summary>
public class TheEnglishPrintsAreCompleteTests
{
    private static readonly Regex Arabic = new(@"[\u0600-\u06FF]", RegexOptions.Compiled);
    private static readonly Regex Wrapped = new(@"PrintLang\.TF?\(\s*""([^""\n]+)""", RegexOptions.Compiled);

    private static PrintBranding English => new(
        "شركة برقوش للتجارة والمقاولات", "300123456700003", "Brqosh Trading & Contracting Co.", "الرياض، حي العليا",
        English: true);

    private static string ReportingDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("جذر المستودع غير موجود."),
            "src", "Brqosh.Infrastructure", "Reporting");
    }

    private static string[] TranslatedGenerators() =>
        ["VoucherPdfGenerator", "StatementPdfGenerator", "TrialBalancePdfGenerator", "FinancialReportPdfGenerator",
         "ConsolidatedReportPdfGenerator", "JournalEntryPdfGenerator", "PayslipPdfGenerator", "PosReportPdfGenerator",
         "PrintBranding", "PrintTheme"];

    [Fact]
    public void EveryWrappedLabelHasAnEnglishEntry()
    {
        var missing = new List<string>();
        var checkedCount = 0;
        foreach (var name in TranslatedGenerators())
        {
            var source = File.ReadAllText(Path.Combine(ReportingDirectory(), name + ".cs"));
            foreach (Match m in Wrapped.Matches(source))
            {
                checkedCount++;
                if (!PrintLangDictionary.Map.ContainsKey(m.Groups[1].Value))
                    missing.Add($"{name}: «{m.Groups[1].Value}»");
            }
        }

        Assert.True(checkedCount > 150, $"القارئُ وجد {checkedCount} تسميةً فقط — أعمى.");
        Assert.True(missing.Count == 0, "تسمياتٌ بلا مقابل إنجليزيّ:\n  " + string.Join("\n  ", missing));
    }

    [Fact]
    public void NoGeneratorWritesABareArabicLabel()
    {
        var offenders = new List<string>();
        foreach (var name in TranslatedGenerators().Where(n => n.EndsWith("PdfGenerator", StringComparison.Ordinal)))
        {
            var lines = File.ReadAllLines(Path.Combine(ReportingDirectory(), name + ".cs"));
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var code = line.TrimStart();
                if (code.StartsWith("//", StringComparison.Ordinal) || code.StartsWith("///", StringComparison.Ordinal) || code.StartsWith('*'))
                    continue;
                if (line.Contains("throw ", StringComparison.Ordinal))
                    continue;

                // ‏نزعُ ما غُلِّف بـT/TF ثم البحثُ عن حرفيٍّ عربيٍّ باقٍ.
                var stripped = Regex.Replace(line, @"PrintLang\.TF?\(\s*""[^""\n]*""", "");
                if (Regex.IsMatch(stripped, @"""[^""\n]*[\u0600-\u06FF][^""\n]*"""))
                    offenders.Add($"{name}:{i + 1}: {code}");
            }
        }

        Assert.True(offenders.Count == 0, "نصوصٌ عربيّةٌ حرفيّةٌ لا تمرّ بـPrintLang.T:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void TheTaxInvoicesStayArabicWhateverThePreference()
    {
        foreach (var name in new[] { "SalesInvoicePdfGenerator", "ProgressBillingPdfGenerator" })
        {
            var source = File.ReadAllText(Path.Combine(ReportingDirectory(), name + ".cs"));
            Assert.Contains("PrintLang.Begin(false)", source, StringComparison.Ordinal);
            Assert.Contains("English = false", source, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(0, "Saudi Riyals Zero Only")]
    [InlineData(1, "Saudi Riyals One Only")]
    [InlineData(1250.5, "Saudi Riyals One Thousand Two Hundred Fifty and Halalas Fifty Only")]
    [InlineData(19.99, "Saudi Riyals Nineteen and Halalas Ninety Nine Only")]
    [InlineData(100, "Saudi Riyals One Hundred Only")]
    [InlineData(2_000_000.01, "Saudi Riyals Two Million and Halalas One Only")]
    [InlineData(1_234_567.89, "Saudi Riyals One Million Two Hundred Thirty Four Thousand Five Hundred Sixty Seven and Halalas Eighty Nine Only")]
    public void TheEnglishAmountInWordsMatchesReferenceValues(double amount, string expected) =>
        Assert.Equal(expected, EnglishAmountWords.ForRiyals((decimal)amount));

    [Fact]
    public void EveryDocumentRendersInEnglishWithoutAnUntranslatedLabel()
    {
        PrintLang.Misses.Clear();
        var branding = English;
        var statementLines = new List<CustomerStatementLine>
        {
            new(new DateOnly(2026, 9, 1), "INV-2026-000101", "فاتورة ضريبية", 11500m, 0m, 11500m),
            new(new DateOnly(2026, 9, 12), "RV-2026-000250", "سند قبض", 0m, 4000m, 7500m),
        };
        var trial = new List<TrialBalanceRow>
        {
            new("1101", "Cash", AccountType.Asset, 52000m, 12000m),
            new("4101", "Sales revenue", AccountType.Revenue, 0m, 40000m),
        };
        var income = new IncomeStatementResult(new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 31),
            [new("4101", "Sales revenue", AccountType.Revenue, 121000m)],
            [new("5101", "Cost of sales", AccountType.Expense, 64000m)]);
        var balance = new BalanceSheetResult(new DateOnly(2026, 10, 31),
            [new("1101", "Cash", AccountType.Asset, 52000m)],
            [new("2101", "Suppliers", AccountType.Liability, 12000m)],
            [new("3101", "Capital", AccountType.Equity, 40000m)]);
        var cashFlow = new CashFlowStatementResult(new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 31),
            new CashFlowSection("التشغيل", [new("صافي الدخل", 57000m), new("تغيّر ذمم العملاء", -11500m)]),
            new CashFlowSection("الاستثمار", [new("شراء/بيع أصول ثابتة (صافي)", -9000m)]),
            new CashFlowSection("التمويل", [new("قروض طويلة الأجل (صافي)", 5000m)]),
            41500m, 10500m, 52000m);

        var english = new Dictionary<string, byte[]>
        {
            ["receipt"] = VoucherPdfGenerator.ReceiptVoucher(branding, "RV-2026-000311", new DateOnly(2026, 10, 20),
                "Al-Nokhba Est.", 4370m, "تحويل بنكي", "TRX-889120", "Invoice settlement"),
            ["payment"] = VoucherPdfGenerator.PaymentVoucher(branding, "PV-2026-000045", new DateOnly(2026, 10, 21),
                "Al-Ufuq Co.", 258750.5m, "شيك", "", ""),
            ["foreign voucher"] = VoucherPdfGenerator.ReceiptVoucher(branding, "RV-2026-000312", new DateOnly(2026, 10, 20),
                "Acme Inc.", 1000m, "نقداً", "", "", currencyCode: "USD", exchangeRate: 3.75m),
            ["statement"] = StatementPdfGenerator.CustomerStatement(branding, "Al-Nokhba Est.", 7500m, statementLines),
            ["trial balance"] = TrialBalancePdfGenerator.Generate(branding, new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 31), trial),
            ["income"] = FinancialReportPdfGenerator.IncomeStatement(branding, income),
            ["balance sheet"] = FinancialReportPdfGenerator.BalanceSheet(branding, balance),
            ["cash flow"] = FinancialReportPdfGenerator.CashFlowStatement(branding, cashFlow),
            ["ledger"] = FinancialReportPdfGenerator.GeneralLedger(branding, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31),
                [(new DateOnly(2026, 10, 1), "1201", "Customers", 11500m, 0m, "Invoice")]),
            ["journal"] = JournalEntryPdfGenerator.Generate(branding, "JE-2026-000412", new DateOnly(2026, 10, 20), "مرحَّل",
                "Invoice recognition", "INV-2026-000123",
                [new("1201", "Customers", 4370m, 0m), new("4101", "Revenue", 0m, 4370m)], 4370m, 4370m),
            ["payslip"] = PayslipPdfGenerator.Build(branding, new PayslipData("2026-09", new DateOnly(2026, 9, 28),
                "Khalid Al-Otaibi", "E-0042", "1098765432", "Accountant", null, 7000m, 1750m, 700m, 300m, 787.5m)),
            ["pos"] = PosReportPdfGenerator.DailySummary(branding,
                [new PosReportQuery.DailySaleRow("2026-10-19", 42, 18250.5m, 2380.5m)]),
            ["consolidated"] = ConsolidatedReportPdfGenerator.TrialBalance("Brqosh Group", "SAR",
                new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 31),
                [new ConsolidatedTrialBalanceRow("1101", "Cash", AccountType.Asset, 52000m, 12000m, 2)]),
        };

        foreach (var (name, pdf) in english)
        {
            Assert.True(pdf.Length > 1500, $"{name}: ملفٌّ أصغرُ من أن يكون مستنداً.");
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
            // لفحصٍ بصريٍّ: BRQOSH_PRINT_OUT=<مجلد> يحفظ الملفّات.
            var outDir = Environment.GetEnvironmentVariable("BRQOSH_PRINT_OUT");
            if (!string.IsNullOrWhiteSpace(outDir))
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllBytes(Path.Combine(outDir, name.Replace(' ', '_') + ".pdf"), pdf);
            }
        }

        var misses = PrintLang.Misses.Keys.Where(k => Arabic.IsMatch(k)).OrderBy(k => k).ToList();
        Assert.True(misses.Count == 0, "نصوصٌ عربيّةٌ مرّت بـT في مطبوعٍ إنجليزيٍّ بلا مقابل:\n  " + string.Join("\n  ", misses));
    }

    [Fact]
    public void ArabicAndEnglishPrintsOfTheSameDocumentDiffer()
    {
        var arabic = VoucherPdfGenerator.ReceiptVoucher(English with { English = false }, "RV-1", new DateOnly(2026, 10, 20),
            "Acme", 100m, "نقداً", "", "");
        var english = VoucherPdfGenerator.ReceiptVoucher(English, "RV-1", new DateOnly(2026, 10, 20),
            "Acme", 100m, "نقداً", "", "");

        Assert.NotEqual(arabic.Length, english.Length);
    }

    [Fact]
    public void AnEnglishPrint_CarriesTheEnglishAddress_AndTheArabicOneStaysArabic()
    {
        var company = Brqosh.Domain.Companies.Company.Create("منشأة", "310123456700003", Brqosh.Domain.Modules.BusinessProfile.General);
        company.UpdateBranding(null, "الرياض", "Tahoma");
        company.SetAddressEn("Riyadh");

        Assert.Equal("Riyadh", PrintBranding.FromCompany(company, null, english: true).AddressEn);
        Assert.Equal("الرياض", PrintBranding.FromCompany(company, null, english: false).AddressAr);
    }

    [Fact]
    public void TheScopeRestoresTheDirectionAfterwards()
    {
        Assert.False(PrintLang.English);
        using (PrintLang.Begin(true))
            Assert.True(PrintLang.English);
        Assert.False(PrintLang.English);
        Assert.Equal("Receipt Voucher", WithEnglish(() => PrintLang.T("سند قبض")));
        Assert.Equal("سند قبض", PrintLang.T("سند قبض"));
    }

    private static string WithEnglish(Func<string> read)
    {
        using var _ = PrintLang.Begin(true);
        return read();
    }
}
