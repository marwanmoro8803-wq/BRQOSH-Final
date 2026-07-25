using Brqosh.Domain.Modules;

namespace Brqosh.Application.Setup;

/// <summary>قالب دولة — ضريبة، عملة، امتثال، ZATCA.</summary>
public record CountryTemplate(
    string Id,
    string NameAr,
    string NameEn,
    string IsoCountryCode,
    CurrencyTemplate Currency,
    VatTemplate Vat,
    IReadOnlyList<string> Compliance,
    string ChartOfAccounts,
    ZatcaCountryTemplate Zatca);

public record CurrencyTemplate(string Base, string Functional);

public record VatTemplate(decimal StandardRate, int RegistrationDigits);

public record ZatcaCountryTemplate(bool Enabled, string DefaultEnvironment, string ApiBaseUrl);

/// <summary>قالب قطاع — يرتبط بـ <see cref="BusinessProfile"/> ويحدد الوحدات والميزات.</summary>
public record IndustryTemplate(
    string Id,
    BusinessProfile BusinessProfile,
    string NameAr,
    string NameEn,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Features,
    IndustryDefaults Defaults);

public record IndustryDefaults(string CostingMethod, decimal DefaultRetentionPercent);

/// <summary>إعداد مدمج من قالب دولة + قطاع — يُطبَّق عند إنشاء الشركة.</summary>
public record ResolvedCompanyConfiguration(
    CountryTemplate Country,
    IndustryTemplate Industry);

/// <summary>محرك القوالب W3 — قراءة JSON من <c>config/templates</c>.</summary>
public interface IConfigurationTemplateLoader
{
    /// <summary>معرّف الدولة الافتراضية — السعودية في v1.</summary>
    string DefaultCountryId { get; }

    CountryTemplate LoadCountry(string countryId);

    IndustryTemplate LoadIndustry(BusinessProfile profile);

    IndustryTemplate LoadIndustryById(string industryId);

    ResolvedCompanyConfiguration Resolve(string countryId, BusinessProfile profile);

    IReadOnlyList<CountryTemplateSummary> ListCountries();

    IReadOnlyList<IndustryTemplateSummary> ListIndustries();
}

public record CountryTemplateSummary(string Id, string NameAr, string NameEn);

public record IndustryTemplateSummary(
    string Id,
    BusinessProfile BusinessProfile,
    string NameAr,
    string NameEn);
