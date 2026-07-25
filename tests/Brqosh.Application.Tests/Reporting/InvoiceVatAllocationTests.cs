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
}
