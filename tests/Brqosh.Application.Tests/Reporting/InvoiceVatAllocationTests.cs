using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-227) عمود الضريبة في جدول البنود يجمع إلى ضريبة الفاتورة **بالضبط**.
///
/// الفاتورة الجديدة تعرض ضريبة كل بند. والتقريب لكل سطر وحده يُنتج مجموعاً يخالف
/// ذيل الفاتورة بهللة أو هللتين — وعمودٌ لا يجمع إلى الإجمالي المطبوع أسفله يُسقط
/// ثقة المدقّق فوراً، ولو كان الفرق ضئيلاً.
/// </summary>
public class InvoiceVatAllocationTests
{
    private static SalesInvoicePdfGenerator.Line L(decimal total) =>
        new("بند", 1m, total, total);

    private static SalesInvoicePdfGenerator.Line L(decimal total, VatCategory category) =>
        new("بند", 1m, total, total, category);

    /// <summary>حالة مصمَّمة للانحراف: ثلث الوعاء لا يُقسَّم على هللتين.</summary>
    [Fact]
    public void ThirdsThatDoNotDivide_StillSumExactly()
    {
        var lines = new[] { L(33.33m), L(33.33m), L(33.34m) };
        const decimal subtotal = 100.00m;
        const decimal vat = 15.00m;

        var allocated = SalesInvoicePdfGenerator.AllocateLineVat(lines, subtotal, vat);

        Assert.Equal(vat, allocated.Sum());
    }

    [Theory]
    [InlineData(new[] { 1.00, 2.00, 7.00 }, 10.00, 1.50)]
    [InlineData(new[] { 0.01, 0.01, 0.01 }, 0.03, 0.00)]
    [InlineData(new[] { 999.99, 0.01 }, 1000.00, 150.00)]
    [InlineData(new[] { 12.34, 56.78, 90.12, 34.56 }, 193.80, 29.07)]
    public void AnyDistribution_SumsToInvoiceVat(double[] totals, double subtotal, double vat)
    {
        var lines = totals.Select(t => L((decimal)t)).ToArray();

        var allocated = SalesInvoicePdfGenerator.AllocateLineVat(lines, (decimal)subtotal, (decimal)vat);

        Assert.Equal((decimal)vat, allocated.Sum());
        Assert.Equal(lines.Length, allocated.Count);
    }

    /// <summary>الفرق يقع على أكبر بند — أقلّ أثر نسبيّ.</summary>
    [Fact]
    public void Residual_LandsOnTheLargestLine()
    {
        var lines = new[] { L(0.01m), L(0.01m), L(99.98m) };

        var allocated = SalesInvoicePdfGenerator.AllocateLineVat(lines, 100.00m, 15.00m);

        Assert.Equal(15.00m, allocated.Sum());
        Assert.True(allocated[2] > allocated[0] && allocated[2] > allocated[1]);
    }

    /// <summary>فاتورة معفاة أو صفرية: أصفار، بلا قسمة على صفر.</summary>
    [Fact]
    public void ZeroSubtotal_YieldsZerosWithoutDividing()
    {
        var allocated = SalesInvoicePdfGenerator.AllocateLineVat([L(0m), L(0m)], 0m, 0m);

        Assert.Equal(2, allocated.Count);
        Assert.All(allocated, v => Assert.Equal(0m, v));
    }

    // ── (CEEF-228 · A-14) الفئة الضريبية تحكم التوزيع ───────────────────────
    //
    // العطل: التوزيع كان تناسبياً على **كل** السطور بحصّتها من الوعاء، بلا نظر إلى
    // الفئة. فبند صادرات صفريّ داخل فاتورة مختلطة كان يُطبع وعليه مبلغ ضريبة لم
    // يُحصَّل قط — على المستند المسلَّم للعميل، لا في شاشة داخلية.

    /// <summary>بند صفريّ داخل فاتورة مختلطة: ضريبته صفر بالضبط، والعمود يجمع للذيل.</summary>
    [Fact]
    public void ZeroRatedLine_InMixedInvoice_CarriesNoVat()
    {
        // 1,000 أساسيّ + 500 صفريّ ⟵ ضريبة المستند 150.00 (على الوعاء الخاضع وحده)
        var lines = new[] { L(1000.00m, VatCategory.Standard), L(500.00m, VatCategory.ZeroRated) };

        var allocated = SalesInvoicePdfGenerator.AllocateLineVat(lines, 1500.00m, 150.00m);

        Assert.Equal(0.00m, allocated[1]);      // كان 50.00 قبل الإصلاح
        Assert.Equal(150.00m, allocated[0]);
        Assert.Equal(150.00m, allocated.Sum()); // العمود ما زال يجمع إلى ذيل الفاتورة
    }

    /// <summary>المعفى وخارج النطاق مثل الصفريّ — لا تحمّل ضريبة بحال.</summary>
    [Theory]
    [InlineData(VatCategory.Exempt)]
    [InlineData(VatCategory.OutOfScope)]
    [InlineData(VatCategory.ZeroRated)]
    public void NonStandardCategories_NeverCarryVat(VatCategory category)
    {
        var lines = new[]
        {
            L(700.00m, VatCategory.Standard),
            L(300.00m, category),
            L(300.00m, VatCategory.Standard),
        };

        var allocated = SalesInvoicePdfGenerator.AllocateLineVat(lines, 1300.00m, 150.00m);

        Assert.Equal(0.00m, allocated[1]);
        Assert.Equal(150.00m, allocated.Sum());
    }

    /// <summary>
    /// الفرق يقع على أكبر بند **خاضع** لا على أكبر بند مطلقاً — وإلا عاد الكسر إلى
    /// سطرٍ معفى فانتقض الإصلاح من حيث أُريد له أن يُحكم.
    /// </summary>
    [Fact]
    public void Residual_LandsOnLargestStandardLine_NotLargestExemptLine()
    {
        // أكبر بند في الفاتورة معفى؛ والوعاء الخاضع ثلثٌ لا يُقسَّم على هللتين.
        var lines = new[]
        {
            L(33.33m, VatCategory.Standard),
            L(33.33m, VatCategory.Standard),
            L(33.34m, VatCategory.Standard),
            L(5000.00m, VatCategory.Exempt),
        };

        var allocated = SalesInvoicePdfGenerator.AllocateLineVat(lines, 5100.00m, 15.00m);

        Assert.Equal(0.00m, allocated[3]);
        Assert.Equal(15.00m, allocated.Sum());
    }

    /// <summary>
    /// حارسٌ للحارس: فاتورةٌ كلّها أساسية يجب أن تُوزَّع كما كانت تماماً — وإلا كان
    /// الإصلاح انزياحاً في أرقام قائمة لا تصحيحاً لعطل.
    /// </summary>
    [Fact]
    public void AllStandardInvoice_BehavesExactlyAsBefore()
    {
        var lines = new[] { L(33.33m), L(33.33m), L(33.34m) };

        var allocated = SalesInvoicePdfGenerator.AllocateLineVat(lines, 100.00m, 15.00m);

        Assert.Equal(15.00m, allocated.Sum());
        Assert.All(allocated, v => Assert.True(v > 0m, "كل بند خاضع يحمل نصيبه"));
    }

    /// <summary>
    /// احتياطٌ لا يُسقِط ريالاً: ضريبةٌ موجبة بلا بند خاضع واحد تعني تعارضاً بين
    /// البيانات والفئات — يُوزَّع حينئذٍ على الجميع حتى يبقى العمود مطابقاً للذيل،
    /// فعمودٌ لا يجمع إلى الإجمالي أسوأ من عمودٍ موزَّع بتحفّظ.
    /// </summary>
    [Fact]
    public void VatWithNoStandardLine_StillSumsToInvoiceVat()
    {
        var lines = new[] { L(600.00m, VatCategory.Exempt), L(400.00m, VatCategory.ZeroRated) };

        var allocated = SalesInvoicePdfGenerator.AllocateLineVat(lines, 1000.00m, 150.00m);

        Assert.Equal(150.00m, allocated.Sum());
    }
}
