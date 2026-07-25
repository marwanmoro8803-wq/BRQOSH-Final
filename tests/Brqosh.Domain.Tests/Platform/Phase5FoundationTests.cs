using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Treasury;

namespace Brqosh.Domain.Tests.Platform;

public class Phase5FoundationTests
{
    [Fact]
    public void Company_defaults_to_SAR_currencies()
    {
        var company = Company.Create("منشأة", "310123456700003", BusinessProfile.Retail);
        Assert.Equal("SAR", company.BaseCurrencyCode);
        Assert.Equal("SAR", company.FunctionalCurrencyCode);

        company.SetCurrencies("usd", "EUR");
        Assert.Equal("USD", company.BaseCurrencyCode);
        Assert.Equal("EUR", company.FunctionalCurrencyCode);
    }

    [Fact]
    public void Branch_requires_code_and_name()
    {
        var branch = Branch.Create(1, "hq", "المركز الرئيسي", isHeadquarters: true);
        Assert.Equal("HQ", branch.Code);
        Assert.True(branch.IsHeadquarters);
    }

    [Fact]
    public void Branch_headquarters_can_be_cleared_and_reassigned()
    {
        var hq = Branch.Create(1, "HQ", "المركز الرئيسي", isHeadquarters: true);
        var branch = Branch.Create(1, "BR2", "فرع جدة");

        hq.ClearHeadquarters();
        branch.MarkAsHeadquarters();

        Assert.False(hq.IsHeadquarters);
        Assert.True(branch.IsHeadquarters);
    }

    [Fact]
    public void Branch_deactivate_rejects_headquarters()
    {
        var hq = Branch.Create(1, "HQ", "المركز الرئيسي", isHeadquarters: true);
        Assert.Throws<Domain.Accounting.AccountingException>(() => hq.Deactivate());
    }

    [Fact]
    public void FxConversion_computes_gain_loss()
    {
        Assert.Equal(3750m, FxConversion.Convert(1000m, 3.75m));
        Assert.Equal(50m, FxConversion.GainOrLoss(1050m, 1000m));
        Assert.Throws<Domain.Accounting.AccountingException>(() => FxConversion.Convert(1m, 0m));
    }
}
