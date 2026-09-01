using Brqosh.Application.Setup;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Payroll;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// تهيئة قاعدة البيانات عند أول تشغيل: إنشاء المخطط، إنشاء الشركة، وزرع دليل الحسابات السعودي.
/// ملاحظة هندسية: EnsureCreated مرحلي — سننتقل إلى EF Migrations عند استقرار المخطط.
/// </summary>
public class DatabaseInitializer(BrqoshDbContext db, IConfigurationTemplateLoader? templateLoader = null)
{
    private readonly IConfigurationTemplateLoader _templates =
        templateLoader ?? new ConfigurationTemplateLoader();

    public async Task<Company> InitializeAsync(
        string companyName,
        string vatNumber,
        BusinessProfile profile,
        string countryTemplateId = "saudi-arabia")
    {
        await new DatabaseSchemaService(db).EnsureSchemaAsync();

        var existing = await db.Companies.FirstOrDefaultAsync();
        if (existing is not null)
            return existing;

        var config = _templates.Resolve(countryTemplateId, profile);
        var company = Company.Create(companyName, vatNumber, profile);
        ApplyConfiguration(company, config);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        await SeedChartOfAccountsAsync(company.Id, config.Country.ChartOfAccounts);
        await SeedDefaultWarehouseAsync(company.Id);
        await SeedUnitsOfMeasureAsync(company.Id);
        await SeedGosiPolicyAsync(company.Id);
        await SeedHeadquartersBranchAsync(company.Id);
        await SeedZatcaSettingsAsync(company.Id, config.Country.Zatca);
        return company;
    }

    /// <summary>زرع الدليل والمستودع والفرع وZATCA لشركة موجودة.</summary>
    public async Task SeedCompanyDefaultsAsync(
        long companyId,
        BusinessProfile profile = BusinessProfile.General,
        string countryTemplateId = "saudi-arabia",
        CancellationToken ct = default)
    {
        var config = _templates.Resolve(countryTemplateId, profile);
        await SeedChartOfAccountsAsync(companyId, config.Country.ChartOfAccounts);
        await SeedDefaultWarehouseAsync(companyId);
        await SeedUnitsOfMeasureAsync(companyId);
        await SeedGosiPolicyAsync(companyId);
        await SeedHeadquartersBranchAsync(companyId);
        await SeedZatcaSettingsAsync(companyId, config.Country.Zatca);
    }

    /// <summary>إنشاء شركة إضافية مع زرع الدليل — للاختبارات والمجموعات.</summary>
    public async Task<Company> CreateAdditionalCompanyAsync(
        string companyName,
        string vatNumber,
        BusinessProfile profile,
        string countryTemplateId = "saudi-arabia")
    {
        var config = _templates.Resolve(countryTemplateId, profile);
        var company = Company.Create(companyName, vatNumber, profile);
        ApplyConfiguration(company, config);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        await SeedCompanyDefaultsAsync(company.Id, profile, countryTemplateId);
        return company;
    }

    private static void ApplyConfiguration(Company company, ResolvedCompanyConfiguration config)
    {
        company.SetCurrencies(config.Country.Currency.Base, config.Country.Currency.Functional);
        company.SetCostingMethod(ConfigurationTemplateLoader.ParseCostingMethod(config.Industry.Defaults.CostingMethod));
    }

    private async Task SeedHeadquartersBranchAsync(long companyId)
    {
        if (await db.Branches.AnyAsync(b => b.CompanyId == companyId))
            return;
        db.Branches.Add(Branch.Create(companyId, "HQ", "المركز الرئيسي", isHeadquarters: true));
        await db.SaveChangesAsync();
    }

    private async Task SeedZatcaSettingsAsync(long companyId, ZatcaCountryTemplate zatcaTemplate)
    {
        if (await db.ZatcaSettings.AnyAsync(s => s.CompanyId == companyId))
            return;

        var settings = ZatcaSettings.CreateDefault(companyId);
        if (!string.IsNullOrWhiteSpace(zatcaTemplate.ApiBaseUrl))
            settings.ApplyTemplateDefaults(zatcaTemplate.Enabled, zatcaTemplate.ApiBaseUrl);
        db.ZatcaSettings.Add(settings);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// (CEEF-230 · ط1-١) ‏بذرُ وحدات القياس.
    ///
    /// <para><b>ويُبذر لكلّ منشأةٍ نسختُها</b> لا جدولٌ عامّ: معاملُ «العلبة» قرارٌ تجاريّ
    /// تُصحّحه المنشأةُ لنفسها، ولا يجوز أن يتغيّر بتغييرِ منشأةٍ أخرى.</para>
    /// </summary>
    private async Task SeedUnitsOfMeasureAsync(long companyId)
    {
        if (await db.UnitsOfMeasure.AnyAsync(u => u.CompanyId == companyId))
            return;

        foreach (var seed in UnitCatalog.Template)
            db.UnitsOfMeasure.Add(UnitOfMeasure.Create(
                companyId, seed.Code, seed.NameAr, seed.UnCode, seed.BaseCode, seed.Factor));

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// (CEEF-230 · ط2-٢) ‏بذرُ نِسَب التأمينات بقيمها المبدئية.
    ///
    /// <para><b>وتُبذَر لكلّ منشأةٍ لتكون قابلةً للتصحيح</b> — والنِّسَبُ يُعدّلها المُنظِّم،
    /// فثابتٌ في الشيفرة يجعل كلَّ تعديلٍ نظاميٍّ إصداراً جديداً.</para>
    /// </summary>
    private async Task SeedGosiPolicyAsync(long companyId)
    {
        if (await db.GosiPolicies.AnyAsync(g => g.CompanyId == companyId))
            return;

        db.GosiPolicies.Add(GosiPolicy.CreateDefault(companyId));
        await db.SaveChangesAsync();
    }

    private async Task SeedDefaultWarehouseAsync(long companyId)
    {
        if (await db.Warehouses.AnyAsync(w => w.CompanyId == companyId))
            return;
        db.Warehouses.Add(Warehouse.Create(companyId, "WH-001", "المستودع الرئيسي", isDefault: true));
        await db.SaveChangesAsync();
    }

    /// <summary>زرع دليل الحسابات السعودي مع الحفاظ على الشجرة (الآباء قبل الأبناء).</summary>
    private async Task SeedChartOfAccountsAsync(long companyId, string chartTemplate)
    {
        if (await db.Accounts.AnyAsync(a => a.CompanyId == companyId))
            return;

        if (!chartTemplate.Equals("saudi", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"قالب الدليل غير مدعوم بعد: {chartTemplate}");

        var idByCode = new Dictionary<string, long>();
        foreach (var seed in SaudiChartOfAccounts.Template)
        {
            long? parentId = seed.ParentCode is null ? null : idByCode[seed.ParentCode];
            var account = Account.Create(
                seed.Code, seed.NameAr, seed.NameEn, seed.Type,
                parentId, seed.IsGroup, seed.IsSystem, companyId);

            db.Accounts.Add(account);
            await db.SaveChangesAsync();
            idByCode[seed.Code] = account.Id;
        }
    }
}
