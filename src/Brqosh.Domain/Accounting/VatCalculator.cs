namespace Brqosh.Domain.Accounting;

using Brqosh.Domain.Tax;

/// <summary>حاسبة ضريبة القيمة المضافة — السعودية + مصفوفة خليجية (G-11…G-15 stub).</summary>
public static class VatCalculator
{
    public const decimal SaudiVatRate = 0.15m;

    public static decimal GetRate(TaxJurisdiction jurisdiction) =>
        VatJurisdictionProfiles.Get(jurisdiction).StandardVatRate;

    public static (decimal TaxableAmount, decimal VatAmount, decimal TotalAmount) FromTaxable(
        decimal taxableAmount,
        TaxJurisdiction jurisdiction = TaxJurisdiction.SaudiArabia)
    {
        var rate = GetRate(jurisdiction);
        return taxableAmount <= 0
            ? (0m, 0m, 0m)
            : (
                Math.Round(taxableAmount, 2, MidpointRounding.AwayFromZero),
                Math.Round(taxableAmount * rate, 2, MidpointRounding.AwayFromZero),
                Math.Round(taxableAmount * (1m + rate), 2, MidpointRounding.AwayFromZero));
    }
}
