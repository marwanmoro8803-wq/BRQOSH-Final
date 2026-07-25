using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Tests.Accounting;

public class VatCalculatorTests
{
    [Fact]
    public void FromTaxable_zero_returns_zeros()
    {
        var (taxable, vat, total) = VatCalculator.FromTaxable(0m);
        Assert.Equal(0m, taxable);
        Assert.Equal(0m, vat);
        Assert.Equal(0m, total);
    }

    [Fact]
    public void FromTaxable_1000_applies_15_percent()
    {
        var (_, vat, total) = VatCalculator.FromTaxable(1000m);
        Assert.Equal(150m, vat);
        Assert.Equal(1150m, total);
    }
}
