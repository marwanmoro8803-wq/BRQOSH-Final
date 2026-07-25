using System.Text.Json;
using System.Text.Json.Serialization;
using Brqosh.Application.Setup;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;

namespace Brqosh.Infrastructure.Setup;

/// <summary>قراءة قوالب JSON من <c>config/templates</c> مع بدائل مدمجة للقطاعات غير الملفّية بعد.</summary>
public sealed class ConfigurationTemplateLoader : IConfigurationTemplateLoader
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    Converters = { new JsonStringEnumConverter() },
  };

  private readonly string _templatesRoot;

  public ConfigurationTemplateLoader(string? templatesRoot = null)
  {
    _templatesRoot = templatesRoot ?? ResolveTemplatesRoot();
  }

  public string DefaultCountryId => "saudi-arabia";

  public CountryTemplate LoadCountry(string countryId)
  {
    var id = NormalizeId(countryId);
    var path = Path.Combine(_templatesRoot, "countries", $"{id}.json");
    if (!File.Exists(path))
      throw new FileNotFoundException($"قالب الدولة غير موجود: {id}", path);

    var dto = Deserialize<CountryTemplateDto>(path);
    return MapCountry(dto);
  }

  public IndustryTemplate LoadIndustry(BusinessProfile profile) =>
    LoadIndustryById(ResolveIndustryId(profile));

  public IndustryTemplate LoadIndustryById(string industryId)
  {
    var id = NormalizeId(industryId);
    var path = Path.Combine(_templatesRoot, "industries", $"{id}.json");
    if (File.Exists(path))
    {
      var dto = Deserialize<IndustryTemplateDto>(path);
      return MapIndustry(dto);
    }

    return BuildDefaultIndustry(ResolveProfileFromIndustryId(id));
  }

  public ResolvedCompanyConfiguration Resolve(string countryId, BusinessProfile profile)
  {
    var country = LoadCountry(string.IsNullOrWhiteSpace(countryId) ? DefaultCountryId : countryId);
    var industry = LoadIndustry(profile);
    return new ResolvedCompanyConfiguration(country, industry);
  }

  public IReadOnlyList<CountryTemplateSummary> ListCountries()
  {
    var dir = Path.Combine(_templatesRoot, "countries");
    if (!Directory.Exists(dir))
      return [new CountryTemplateSummary(DefaultCountryId, "المملكة العربية السعودية", "Saudi Arabia")];

    return Directory.GetFiles(dir, "*.json")
      .Select(path =>
      {
        var dto = Deserialize<CountryTemplateDto>(path);
        return new CountryTemplateSummary(dto.Id, dto.NameAr, dto.NameEn);
      })
      .OrderBy(c => c.NameEn)
      .ToList();
  }

  public IReadOnlyList<IndustryTemplateSummary> ListIndustries() =>
    Enum.GetValues<BusinessProfile>()
      .Select(profile =>
      {
        var template = LoadIndustry(profile);
        return new IndustryTemplateSummary(template.Id, template.BusinessProfile, template.NameAr, template.NameEn);
      })
      .ToList();

  private T Deserialize<T>(string path)
  {
    var json = File.ReadAllText(path);
    var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
    if (value is null)
      throw new InvalidOperationException($"تعذر قراءة القالب: {path}");
    return value;
  }

  private static CountryTemplate MapCountry(CountryTemplateDto dto) =>
    new(
      dto.Id,
      dto.NameAr,
      dto.NameEn,
      dto.IsoCountryCode,
      new CurrencyTemplate(dto.Currency.Base, dto.Currency.Functional),
      new VatTemplate(dto.Vat.StandardRate, dto.Vat.RegistrationDigits),
      dto.Compliance,
      dto.ChartOfAccounts,
      new ZatcaCountryTemplate(dto.Zatca.Enabled, dto.Zatca.DefaultEnvironment, dto.Zatca.ApiBaseUrl));

  private static IndustryTemplate MapIndustry(IndustryTemplateDto dto)
  {
    if (!Enum.TryParse<BusinessProfile>(dto.BusinessProfile, ignoreCase: true, out var profile))
      throw new InvalidOperationException($"BusinessProfile غير معروف في القالب: {dto.BusinessProfile}");

    return new IndustryTemplate(
      dto.Id,
      profile,
      dto.NameAr,
      dto.NameEn,
      dto.Modules,
      dto.Features,
      new IndustryDefaults(dto.Defaults.CostingMethod, dto.Defaults.DefaultRetentionPercent));
  }

  private static IndustryTemplate BuildDefaultIndustry(BusinessProfile profile) => profile switch
  {
    BusinessProfile.Construction => new IndustryTemplate(
      "construction", BusinessProfile.Construction, "مقاولات عامة", "Construction",
      ["GeneralLedger", "ProjectsConstruction", "Purchasing", "Treasury", "Reports", "Zatca", "FixedAssets", "Payroll"],
      ["Retention", "BOQ", "IFRS15", "ProgressBilling", "Subcontractors", "ProjectProfitability"],
      new IndustryDefaults("WeightedAverage", 10m)),
    BusinessProfile.Manufacturing => new IndustryTemplate(
      "manufacturing", BusinessProfile.Manufacturing, "مصنع / إنتاج", "Manufacturing",
      ["GeneralLedger", "Manufacturing", "Inventory", "Purchasing", "Treasury", "Reports", "Zatca", "FixedAssets", "Payroll"],
      ["BOM", "WorkOrders", "WIP", "IndustrialCosting"],
      new IndustryDefaults("WeightedAverage", 0m)),
    BusinessProfile.Retail => new IndustryTemplate(
      "retail", BusinessProfile.Retail, "محل / تسويق", "Retail",
      ["GeneralLedger", "PointOfSale", "Sales", "Inventory", "Treasury", "Reports", "Zatca", "FixedAssets", "Payroll"],
      ["POS", "Barcode", "DailySales", "MultiBranch"],
      new IndustryDefaults("Fifo", 0m)),
    BusinessProfile.Healthcare => new IndustryTemplate(
      "healthcare", BusinessProfile.Healthcare, "قطاع صحي", "Healthcare",
      ["GeneralLedger", "Sales", "Purchasing", "Inventory", "Treasury", "Reports", "Zatca", "FixedAssets", "Payroll"],
      ["PatientBilling", "Pharmacy", "InsuranceClaims", "ClinicalSupplies"],
      new IndustryDefaults("WeightedAverage", 0m)),
    BusinessProfile.Logistics => new IndustryTemplate(
      "logistics", BusinessProfile.Logistics, "لوجستيات ونقل", "Logistics",
      ["GeneralLedger", "Sales", "Purchasing", "Inventory", "Treasury", "Reports", "Zatca", "FixedAssets", "Payroll"],
      ["Warehousing", "Fleet", "DeliveryRoutes", "3PL"],
      new IndustryDefaults("WeightedAverage", 0m)),
    BusinessProfile.RealEstate => new IndustryTemplate(
      "real-estate", BusinessProfile.RealEstate, "عقارات وإيجارات", "Real Estate",
      ["GeneralLedger", "Sales", "Treasury", "Reports", "Zatca", "FixedAssets", "Payroll"],
      ["LeaseContracts", "TenantBilling", "PropertyUnits", "CAM"],
      new IndustryDefaults("WeightedAverage", 0m)),
    BusinessProfile.Hospitality => new IndustryTemplate(
      "hospitality", BusinessProfile.Hospitality, "ضيافة ومطاعم", "Hospitality",
      ["GeneralLedger", "PointOfSale", "Sales", "Inventory", "Treasury", "Reports", "Zatca", "FixedAssets", "Payroll"],
      ["RestaurantPOS", "RoomRevenue", "Banquet", "KitchenInventory"],
      new IndustryDefaults("Fifo", 0m)),
    _ => new IndustryTemplate(
      "general", BusinessProfile.General, "محاسبة عامة", "General",
      ["GeneralLedger", "Treasury", "Reports", "FixedAssets", "Payroll"],
      ["CoreAccounting", "Treasury", "FinancialReports"],
      new IndustryDefaults("WeightedAverage", 0m)),
  };

  private static string ResolveIndustryId(BusinessProfile profile) => profile switch
  {
    BusinessProfile.Construction => "construction",
    BusinessProfile.Manufacturing => "manufacturing",
    BusinessProfile.Retail => "retail",
    BusinessProfile.Healthcare => "healthcare",
    BusinessProfile.Logistics => "logistics",
    BusinessProfile.RealEstate => "real-estate",
    BusinessProfile.Hospitality => "hospitality",
    _ => "general",
  };

  private static BusinessProfile ResolveProfileFromIndustryId(string industryId) => industryId switch
  {
    "construction" => BusinessProfile.Construction,
    "manufacturing" => BusinessProfile.Manufacturing,
    "retail" => BusinessProfile.Retail,
    "healthcare" => BusinessProfile.Healthcare,
    "logistics" => BusinessProfile.Logistics,
    "real-estate" => BusinessProfile.RealEstate,
    "hospitality" => BusinessProfile.Hospitality,
    _ => BusinessProfile.General,
  };

  private static string NormalizeId(string id) =>
    (id ?? string.Empty).Trim().ToLowerInvariant();

  internal static string ResolveTemplatesRoot()
  {
    var candidates = new List<string>
    {
      Path.Combine(AppContext.BaseDirectory, "config", "templates"),
    };

    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var depth = 0; depth < 8 && dir is not null; depth++, dir = dir.Parent)
    {
      candidates.Add(Path.Combine(dir.FullName, "config", "templates"));
    }

    foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
    {
      if (Directory.Exists(Path.Combine(candidate, "countries")))
        return candidate;
    }

    return Path.Combine(AppContext.BaseDirectory, "config", "templates");
  }

  internal static InventoryCostingMethod ParseCostingMethod(string method) =>
    method.Trim().Equals("Fifo", StringComparison.OrdinalIgnoreCase)
      ? InventoryCostingMethod.Fifo
      : InventoryCostingMethod.WeightedAverage;

  private sealed class CountryTemplateDto
  {
    public string Id { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string IsoCountryCode { get; set; } = "";
    public CurrencyDto Currency { get; set; } = new();
    public VatDto Vat { get; set; } = new();
    public List<string> Compliance { get; set; } = [];
    public string ChartOfAccounts { get; set; } = "";
    public ZatcaDto Zatca { get; set; } = new();
  }

  private sealed class CurrencyDto
  {
    public string Base { get; set; } = "SAR";
    public string Functional { get; set; } = "SAR";
  }

  private sealed class VatDto
  {
    public decimal StandardRate { get; set; }
    public int RegistrationDigits { get; set; }
  }

  private sealed class ZatcaDto
  {
    public bool Enabled { get; set; }
    public string DefaultEnvironment { get; set; } = "Simulation";
    public string ApiBaseUrl { get; set; } = "";
  }

  private sealed class IndustryTemplateDto
  {
    public string Id { get; set; } = "";
    public string BusinessProfile { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string NameEn { get; set; } = "";
    public List<string> Modules { get; set; } = [];
    public List<string> Features { get; set; } = [];
    public DefaultsDto Defaults { get; set; } = new();
  }

  private sealed class DefaultsDto
  {
    public string CostingMethod { get; set; } = "WeightedAverage";
    public decimal DefaultRetentionPercent { get; set; }
  }
}
