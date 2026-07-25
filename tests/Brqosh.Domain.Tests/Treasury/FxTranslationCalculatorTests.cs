using Brqosh.Domain.Treasury;

namespace Brqosh.Domain.Tests.Treasury;

public class FxTranslationCalculatorTests
{
    [Fact]
    public void ComputePeriodCtaAdjustment_uses_rate_spread_on_net_income()
    {
        var cta = FxTranslationCalculator.ComputePeriodCtaAdjustment(100m, 3.70m, 3.75m);
        Assert.Equal(5m, cta);
    }

    [Fact]
    public void ComputePeriodCtaAdjustment_returns_zero_when_rates_match()
    {
        Assert.Equal(0m, FxTranslationCalculator.ComputePeriodCtaAdjustment(100m, 3.75m, 3.75m));
        Assert.Equal(0m, FxTranslationCalculator.ComputePeriodCtaAdjustment(0m, 3.70m, 3.75m));
    }
}
