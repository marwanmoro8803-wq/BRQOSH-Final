using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Tests.Companies;

/// <summary>(أسئلةُ المالك 2026-10-24 · العملة) كتالوجُ العملات وما يُكتب بجوار المبلغ.</summary>
public class CurrencyCatalogTests
{
    [Theory]
    [InlineData("SAR", true)]
    [InlineData("usd", true)]
    [InlineData(" kwd ", true)]
    [InlineData("ABC", false)]
    [InlineData("XXX", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void KnownCurrencies_AreRecognised(string? code, bool expected) =>
        Assert.Equal(expected, CurrencyCatalog.IsKnown(code));

    [Theory]
    [InlineData("SAR", false, "ر.س")]
    [InlineData("SAR", true, "SAR")]
    [InlineData("AED", false, "د.إ")]
    [InlineData("USD", false, "$")]
    [InlineData("USD", true, "USD")]
    [InlineData("CHF", false, "CHF")]
    [InlineData(null, false, "ر.س")]
    [InlineData("", true, "SAR")]
    public void TheLabelNextToAnAmount_FollowsTheCurrencyAndLanguage(string? code, bool english, string expected) =>
        Assert.Equal(expected, CurrencyCatalog.Short(code, english));

    [Fact]
    public void TheChoiceList_StartsWithTheCommonOnes_AndHasNoDuplicates()
    {
        Assert.Equal("SAR", CurrencyCatalog.Codes[0]);
        Assert.Equal(CurrencyCatalog.Codes.Count, CurrencyCatalog.Codes.Distinct().Count());
        Assert.All(CurrencyCatalog.Codes, c => Assert.True(CurrencyCatalog.IsKnown(c)));
    }
}
