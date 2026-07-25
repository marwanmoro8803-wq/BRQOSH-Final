using Brqosh.Application.Setup;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Setup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Setup;

public class ConfigurationTemplateTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly ConfigurationTemplateLoader _loader;

    public ConfigurationTemplateTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new BrqoshDbContext(options);
        _loader = new ConfigurationTemplateLoader();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void DefaultCountry_IsSaudiArabia()
    {
        Assert.Equal("saudi-arabia", _loader.DefaultCountryId);
    }

    [Fact]
    public void LoadCountry_SaudiArabia_HasVatAndZatca()
    {
        var country = _loader.LoadCountry("saudi-arabia");

        Assert.Equal("SA", country.IsoCountryCode);
        Assert.Equal("SAR", country.Currency.Base);
        Assert.Equal(0.15m, country.Vat.StandardRate);
        Assert.Contains("ZATCA", country.Compliance);
        Assert.True(country.Zatca.Enabled);
    }

    [Fact]
    public void LoadIndustry_Construction_MatchesBusinessProfile()
    {
        var industry = _loader.LoadIndustry(BusinessProfile.Construction);

        Assert.Equal(BusinessProfile.Construction, industry.BusinessProfile);
        Assert.Equal("construction", industry.Id);
        Assert.Contains("BOQ", industry.Features);
        Assert.Contains("ProjectsConstruction", industry.Modules);
        Assert.Equal(10m, industry.Defaults.DefaultRetentionPercent);
    }

    [Fact]
    public void Resolve_MergesCountryAndIndustry()
    {
        var resolved = _loader.Resolve("saudi-arabia", BusinessProfile.Construction);

        Assert.Equal("saudi-arabia", resolved.Country.Id);
        Assert.Equal(BusinessProfile.Construction, resolved.Industry.BusinessProfile);
    }

    [Fact]
    public void ListCountries_IncludesSaudiArabia()
    {
        var countries = _loader.ListCountries();

        Assert.Contains(countries, c => c.Id == "saudi-arabia");
    }

    [Fact]
    public void ListIndustries_CoversAllBusinessProfiles()
    {
        var industries = _loader.ListIndustries();

        Assert.Equal(Enum.GetValues<BusinessProfile>().Length, industries.Count);
        Assert.Contains(industries, i => i.BusinessProfile == BusinessProfile.Retail);
    }

    [Fact]
    public async Task DatabaseInitializer_AppliesTemplateOnCompanyCreate()
    {
        var initializer = new DatabaseInitializer(_db, _loader);
        var company = await initializer.InitializeAsync(
            "شركة قوالب", "310123456700003", BusinessProfile.Construction);

        Assert.Equal("SAR", company.BaseCurrencyCode);
        Assert.Equal(InventoryCostingMethod.WeightedAverage, company.CostingMethod);

        var zatca = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
        Assert.Contains("zatca.gov.sa", zatca.ApiBaseUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadIndustry_Retail_UsesFifoDefault()
    {
        var industry = _loader.LoadIndustry(BusinessProfile.Retail);

        Assert.Equal("Fifo", industry.Defaults.CostingMethod);
    }
}
