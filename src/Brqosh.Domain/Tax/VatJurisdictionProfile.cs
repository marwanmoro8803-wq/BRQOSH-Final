namespace Brqosh.Domain.Tax;

/// <summary>ملف ضريبة القيمة المضافة لكل سلطة — stub in-repo لـ G-11…G-15.</summary>
public sealed record VatJurisdictionProfile(
    TaxJurisdiction Jurisdiction,
    string IsoCountryCode,
    string CurrencyCode,
    decimal StandardVatRate,
    bool EInvoicingInRepo,
    string EInvoicingAuthority);

public static class VatJurisdictionProfiles
{
    public static IReadOnlyList<VatJurisdictionProfile> All { get; } =
    [
        new(TaxJurisdiction.SaudiArabia, "SA", "SAR", 0.15m, true, "ZATCA"),
        new(TaxJurisdiction.UnitedArabEmirates, "AE", "AED", 0.05m, false, "FTA"),
        new(TaxJurisdiction.Qatar, "QA", "QAR", 0.00m, false, "GTA"),
        new(TaxJurisdiction.Bahrain, "BH", "BHD", 0.10m, false, "NBR"),
        new(TaxJurisdiction.Kuwait, "KW", "KWD", 0.00m, false, "KTA"),
        new(TaxJurisdiction.Oman, "OM", "OMR", 0.05m, false, "Oman Tax Authority"),
    ];

    public static VatJurisdictionProfile Get(TaxJurisdiction jurisdiction) =>
        All.Single(p => p.Jurisdiction == jurisdiction);
}
