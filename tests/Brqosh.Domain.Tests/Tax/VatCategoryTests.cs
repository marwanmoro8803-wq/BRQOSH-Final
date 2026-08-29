using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;

namespace Brqosh.Domain.Tests.Tax;

/// <summary>
/// (CEEF-207) الفئات الضريبية.
///
/// كانت النسبة 15% مسمّرة في كل المسارات، فيستحيل إصدار فاتورة فيها سطر صادرات صفريّ
/// أو دواء معفى — كل سطر يُضرَّب 15% إجبارياً. والتقريب صار على مستوى **الوعاء**
/// (الفئة) وهو ما تفرضه دلالة cac:TaxSubtotal لدى الهيئة.
/// </summary>
public class VatCategoryTests
{
    private static SalesInvoice Invoice() =>
        SalesInvoice.CreateDraft(1, 1, new DateOnly(2026, 7, 9));

    /// <summary>الحالة السائدة (كل الأسطر أساسية) تنتج نفس أرقام السلوك السابق حرفياً.</summary>
    [Fact]
    public void AllStandardLines_MatchPreviousBehaviour()
    {
        var invoice = Invoice();
        invoice.AddLine("صنف أ", 2m, 300m);
        invoice.AddLine("صنف ب", 1m, 400m);

        Assert.Equal(1000m, invoice.Subtotal);
        Assert.Equal(150m, invoice.VatAmount);          // 1000 × 15%

        var subtotals = LineDocumentTotalsCalculator.ComputeSubtotals(invoice.Lines);
        Assert.Single(subtotals);                       // وعاء واحد
        Assert.Equal(VatCategory.Standard, subtotals[0].Category);
    }

    /// <summary>السطر الصفريّ لا يُضرَّب 15% — وهو ما كان مستحيلاً قبلاً.</summary>
    [Fact]
    public void ZeroRatedLine_IsNotTaxed()
    {
        var invoice = Invoice();
        invoice.AddLine("تصدير", 1m, 1000m, vatCategory: VatCategory.ZeroRated, taxExemptionReasonCode: "VATEX-SA-32");

        Assert.Equal(1000m, invoice.Subtotal);
        Assert.Equal(0m, invoice.VatAmount);
    }

    [Theory]
    [InlineData(VatCategory.ZeroRated, "Z")]
    [InlineData(VatCategory.Exempt, "E")]
    [InlineData(VatCategory.OutOfScope, "O")]
    [InlineData(VatCategory.Standard, "S")]
    public void Category_MapsToUblCode_AndConsistentRate(VatCategory category, string expectedCode)
    {
        Assert.Equal(expectedCode, category.UblCode());

        var rate = category.RateFor(SalesInvoice.SaudiVatRate);
        // النسبة المعلنة تطابق المبلغ المحسوب — لا "O" بنسبة 15.00 كما كان.
        Assert.Equal(category == VatCategory.Standard ? 0.15m : 0m, rate);
    }

    /// <summary>فاتورة مختلطة: وعاء لكل فئة، وإجمالي الضريبة مجموع الأوعية.</summary>
    [Fact]
    public void MixedCategories_ProduceOneSubtotalPerCategory()
    {
        var invoice = Invoice();
        invoice.AddLine("بيع محلي", 1m, 1000m);                                        // أساسي
        invoice.AddLine("تصدير", 1m, 500m, vatCategory: VatCategory.ZeroRated, taxExemptionReasonCode: "VATEX-SA-32");        // صفري
        invoice.AddLine("إيجار سكني", 1m, 200m, vatCategory: VatCategory.Exempt, taxExemptionReasonCode: "VATEX-SA-30");      // معفى

        var subtotals = LineDocumentTotalsCalculator.ComputeSubtotals(invoice.Lines);
        Assert.Equal(3, subtotals.Count);

        var standard = subtotals.Single(s => s.Category == VatCategory.Standard);
        Assert.Equal(1000m, standard.TaxableAmount);
        Assert.Equal(150m, standard.VatAmount);

        Assert.Equal(0m, subtotals.Single(s => s.Category == VatCategory.ZeroRated).VatAmount);
        Assert.Equal(0m, subtotals.Single(s => s.Category == VatCategory.Exempt).VatAmount);

        Assert.Equal(1700m, invoice.Subtotal);          // الوعاء الكلي يشمل كل الفئات
        Assert.Equal(150m, invoice.VatAmount);          // الضريبة من الأساسي وحده

        // اتساق حسابيّ يفحصه مدقّق الهيئة: إجمالي المستند = مجموع الأوعية.
        Assert.Equal(invoice.VatAmount, subtotals.Sum(s => s.VatAmount));
        Assert.Equal(invoice.Subtotal, subtotals.Sum(s => s.TaxableAmount));
    }

    /// <summary>التقريب على مستوى الوعاء لا السطر — فئتان لا تتداخل كسورهما.</summary>
    [Fact]
    public void RoundingIsPerCategory_NotPerLine()
    {
        var invoice = Invoice();
        invoice.AddLine("أ", 3m, 0.10m);                                               // 0.30 أساسي
        invoice.AddLine("ب", 3m, 0.10m, vatCategory: VatCategory.ZeroRated, taxExemptionReasonCode: "VATEX-SA-32");           // 0.30 صفري

        var subtotals = LineDocumentTotalsCalculator.ComputeSubtotals(invoice.Lines);

        // 0.30 × 15% = 0.045 → تقريب الوعاء 0.05 (لا 0.02×3 = 0.06 بتقريب السطر)
        Assert.Equal(0.05m, subtotals.Single(s => s.Category == VatCategory.Standard).VatAmount);
        Assert.Equal(0.05m, invoice.VatAmount);
    }
}
