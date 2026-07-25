using Brqosh.Domain.Accounting;
using Brqosh.Domain.Tax;

namespace Brqosh.Domain.Tests;

public class VatJurisdictionTests
{
    [Theory]
    [InlineData(TaxJurisdiction.SaudiArabia, 0.15)]
    [InlineData(TaxJurisdiction.UnitedArabEmirates, 0.05)]
    [InlineData(TaxJurisdiction.Bahrain, 0.10)]
    [InlineData(TaxJurisdiction.Oman, 0.05)]
    public void GetRate_matches_jurisdiction_matrix(TaxJurisdiction jurisdiction, decimal expected) =>
        Assert.Equal(expected, VatJurisdictionProfiles.Get(jurisdiction).StandardVatRate);

    [Fact]
    public void All_jurisdictions_G11_through_G15_present()
    {
        var codes = VatJurisdictionProfiles.All.Select(p => p.IsoCountryCode).ToList();
        Assert.Equal(["SA", "AE", "QA", "BH", "KW", "OM"], codes);
    }

    [Fact]
    public void FromTaxable_UAE_uses_five_percent()
    {
        var (_, vat, total) = VatCalculator.FromTaxable(1000m, TaxJurisdiction.UnitedArabEmirates);
        Assert.Equal(50m, vat);
        Assert.Equal(1050m, total);
    }
}
