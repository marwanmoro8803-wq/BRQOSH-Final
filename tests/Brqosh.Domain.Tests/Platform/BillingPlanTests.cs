using Brqosh.Domain.Accounting;
using Brqosh.Domain.Platform;

namespace Brqosh.Domain.Tests.Platform;

public class BillingPlanTests
{
    [Fact]
    public void Create_normalizes_code()
    {
        var plan = BillingPlan.Create("trial", "تجريبي", 1);
        Assert.Equal("TRIAL", plan.Code);
    }

    [Fact]
    public void Create_rejects_empty_code()
    {
        Assert.Throws<AccountingException>(() => BillingPlan.Create("", "x", 1));
    }
}
