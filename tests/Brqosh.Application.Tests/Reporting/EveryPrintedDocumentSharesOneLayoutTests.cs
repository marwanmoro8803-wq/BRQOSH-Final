using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Sales;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// كلُّ مطبوعٍ يأخذ <b>الترتيبَ والشكلَ والتذييلَ نفسَه</b>: ترويسةٌ مشتركة (<see cref="PrintBranding.RenderHeader"/>) وتذييلٌ
/// مشترك (<see cref="PrintBranding.RenderFooterMeta"/>) وجداولُ <see cref="PrintTheme"/> — لا تذييلُ نصٍّ مبتورٌ هنا
/// وترويسةٌ مرسومةٌ يدويّاً هناك. والقياسُ ثنائيّ: (١) الأصولُ — مولِّدٌ جديدٌ يخرج عن القالب يسقط؛ (٢) كلُّ مولِّدٍ يُنتج PDF
/// فعلاً من بيانات نموذجيّة (QuestPDF يرمي على أيّ تخطيطٍ متعارض).
/// </summary>
public class EveryPrintedDocumentSharesOneLayoutTests
{
    private static readonly PrintBranding Branding = new(
        "شركة برقوش للتجارة والمقاولات", "300123456700003", "Brqosh Trading & Contracting Co.", "الرياض، حي العليا");

    private static string ReportingDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("جذر المستودع غير موجود."),
            "src", "Brqosh.Infrastructure", "Reporting");
    }

    [Fact]
    public void EveryGeneratorUsesTheSharedHeaderAndFooter()
    {
        var generators = Directory.GetFiles(ReportingDirectory(), "*PdfGenerator.cs");
        Assert.True(generators.Length >= 10, "قائمةُ المولِّدات أقلّ ممّا يُتوقَّع — القياسُ أعمى.");

        foreach (var file in generators)
        {
            var source = File.ReadAllText(file);
            var name = Path.GetFileName(file);

            // ‏المولِّداتُ الجدوليّةُ تمرّ من PrintReport الذي يستعمل الترويسةَ والتذييلَ المشتركَين بدورها.
            var viaSharedBuilder = source.Contains("PrintReport.Table(", StringComparison.Ordinal);
            if (!viaSharedBuilder)
            {
                Assert.True(source.Contains("branding.RenderHeader(", StringComparison.Ordinal),
                    $"{name}: ترويسةٌ غيرُ مشتركة.");
                Assert.True(source.Contains("branding.RenderFooterMeta(", StringComparison.Ordinal),
                    $"{name}: تذييلٌ غيرُ مشترك.");
            }

            Assert.DoesNotContain("page.Footer().Text(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("page.Footer().AlignCenter().Text(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("FontFamily(\"Arial\")", source, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// (أسئلةُ المالك 2026-10-24 · العملة) <b>لا مولِّدَ يكتب «ر.س» في شيفرته</b> — رمزُ العملة من <c>branding.CurrencyAr</c> (عملةِ دفاتر
    /// المنشأة) أو من عملةِ المستند. واستثناءٌ واحد: فاتورةُ المبيعات تطبع مكافئَ الريال لفاتورةٍ بعملةٍ أجنبية (متطلّبُ الهيئة).
    /// </summary>
    [Fact]
    public void NoGeneratorHardcodesTheRiyalSymbol()
    {
        foreach (var file in Directory.GetFiles(ReportingDirectory(), "*PdfGenerator.cs"))
        {
            var name = Path.GetFileName(file);
            if (name == "SalesInvoicePdfGenerator.cs")
                continue;

            Assert.False(File.ReadAllText(file).Contains("ر.س", StringComparison.Ordinal),
                $"{name}: «ر.س» مكتوبةٌ في المولِّد — استعمل branding.CurrencyAr.");
        }
    }

    [Theory]
    [InlineData("SAR", true)]
    [InlineData("sar", true)]
    [InlineData("USD", false)]
    [InlineData("KWD", false)]
    public void TheAmountInWordsIsPrintedForTheRiyalOnly(string currency, bool printed)
    {
        var words = PrintTheme.AmountInWordsText(1250.5m, currency);

        Assert.Equal(printed, words is not null);
        if (printed)
            Assert.Contains("ريالاً", words!, StringComparison.Ordinal);
    }

    [Fact]
    public void AVoucherInAForeignCurrencyRendersWithItsCurrencyAndTheBookEquivalent()
    {
        var pdf = VoucherPdfGenerator.ReceiptVoucher(Branding, "RV-2026-000312", new DateOnly(2026, 10, 20),
            "Acme Inc.", 1000m, "تحويل بنكي", "TRX-1", "سداد", currencyCode: "USD", exchangeRate: 3.75m);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 1500);
    }

    [Fact]
    public void TheBrandingCarriesTheCompanysCurrencyLabel()
    {
        Assert.Equal("ر.س", Branding.CurrencyAr);
        Assert.Equal("$", (Branding with { CurrencyCode = "USD" }).CurrencyAr);
        Assert.Equal("د.إ", (Branding with { CurrencyCode = "AED" }).CurrencyAr);
    }

    [Fact]
    public void EveryDocumentRendersToARealPdf()
    {
        var statementLines = new List<CustomerStatementLine>
        {
            new(new DateOnly(2026, 9, 1), "INV-2026-000101", "فاتورة ضريبية", 11500m, 0m, 11500m),
            new(new DateOnly(2026, 9, 12), "RV-2026-000250", "سند قبض", 0m, 4000m, 7500m),
        };
        var trial = new List<TrialBalanceRow>
        {
            new("1101", "الصندوق", AccountType.Asset, 52000m, 12000m),
            new("4101", "إيرادات المبيعات", AccountType.Revenue, 0m, 40000m),
        };
        var income = new IncomeStatementResult(new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 31),
            [new("4101", "إيرادات المبيعات", AccountType.Revenue, 121000m)],
            [new("5101", "تكلفة المبيعات", AccountType.Expense, 64000m)]);

        var documents = new Dictionary<string, byte[]>
        {
            ["سند قبض"] = VoucherPdfGenerator.ReceiptVoucher(Branding, "RV-2026-000311", new DateOnly(2026, 10, 20),
                "مؤسسة النخبة", 4370m, "تحويل بنكي", "TRX-889120", "سداد الفاتورة"),
            ["سند صرف"] = VoucherPdfGenerator.PaymentVoucher(Branding, "PV-2026-000045", new DateOnly(2026, 10, 21),
                "شركة الأفق", 258750.5m, "شيك", "", ""),
            ["كشف حساب"] = StatementPdfGenerator.CustomerStatement(Branding, "مؤسسة النخبة", 7500m, statementLines),
            ["ميزان مراجعة"] = TrialBalancePdfGenerator.Generate(Branding, new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 31), trial),
            ["قائمة دخل"] = FinancialReportPdfGenerator.IncomeStatement(Branding, income),
            ["دفتر أستاذ"] = FinancialReportPdfGenerator.GeneralLedger(Branding, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31),
                [(new DateOnly(2026, 10, 1), "1201", "العملاء", 11500m, 0m, "فاتورة")]),
            ["قيد يومية"] = JournalEntryPdfGenerator.Generate(Branding, "JE-2026-000412", new DateOnly(2026, 10, 20), "مرحَّل",
                "إثبات فاتورة", "INV-2026-000123",
                [new("1201", "العملاء", 4370m, 0m), new("4101", "إيرادات", 0m, 4370m)], 4370m, 4370m),
            ["قسيمة راتب"] = PayslipPdfGenerator.Build(Branding, new PayslipData("2026-09", new DateOnly(2026, 9, 28),
                "خالد العتيبي", "E-0042", "1098765432", "محاسب", null, 7000m, 1750m, 700m, 300m, 787.5m)),
            ["تقرير نقاط البيع"] = PosReportPdfGenerator.DailySummary(Branding,
                [new PosReportQuery.DailySaleRow("2026-10-19", 42, 18250.5m, 2380.5m)]),
            ["ميزان موحّد"] = ConsolidatedReportPdfGenerator.TrialBalance("مجموعة برقوش", "SAR",
                new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 31),
                [new ConsolidatedTrialBalanceRow("1101", "الصندوق", AccountType.Asset, 52000m, 12000m, 2)]),
        };

        foreach (var (name, pdf) in documents)
        {
            Assert.True(pdf.Length > 1500, $"{name}: ملفٌّ أصغرُ من أن يكون مستنداً.");
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        }
    }
}
