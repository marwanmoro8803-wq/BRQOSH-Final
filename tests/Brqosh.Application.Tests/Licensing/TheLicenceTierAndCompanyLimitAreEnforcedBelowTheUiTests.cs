using Brqosh.Application.Licensing;
using Brqosh.Application.Platform;
using Brqosh.Application.Setup;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Setup;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// ‏(Phase 7 · security-licensing-tenancy#2) <b>الفئةُ وحدُّ المنشآت يُفرضان في الخدمات</b> — من مدخلَي <c>edition:</c> و<c>companies:</c>
/// ‏الموقَّعَين؛ وترخيصٌ بلا أيٍّ منهما لا يحصر (كلُّ ما صدر قبل هذا).
/// </summary>
public class TheLicenceTierAndCompanyLimitAreEnforcedBelowTheUiTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public TheLicenceTierAndCompanyLimitAreEnforcedBelowTheUiTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Database.CloseConnection();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static readonly LicenseEntitlements Standard = new(LicenseEdition.Standard, null);
    private static readonly LicenseEntitlements Professional = new(LicenseEdition.Professional, null);

    [Fact]
    public void TheSignedFeatures_NameTheEditionAndTheLimit_AndOldLicencesStayUnrestricted()
    {
        Assert.Equal(LicenseEntitlements.Unrestricted, LicenseEntitlementsReader.From(["core", "zatca", "construction"]));
        Assert.Equal(new LicenseEntitlements(LicenseEdition.Professional, 3),
            LicenseEntitlementsReader.From(["core", "edition:Professional", "companies:3"]));
        // ‏فئةٌ لا يعرفها الإصدارُ قياسيّة، وعددٌ تالفٌ واحد — لا يُفتح شيءٌ بخطأ.
        Assert.Equal(new LicenseEntitlements(LicenseEdition.Standard, 1),
            LicenseEntitlementsReader.From(["edition:enterprise", "companies:zero"]));
        Assert.Equal(["edition:standard", "companies:2"], LicenseEntitlementsReader.ToFeatures(LicenseEdition.Standard, 2));
    }

    [Fact]
    public async Task AStandardLicence_RefusesProfessionalFeatures_InTheServices()
    {
        var company = await InitAsync("منشأة قياسية");
        using var _ = LicenseGate.Use(Standard);

        var payroll = await Assert.ThrowsAsync<AccountingException>(() => new PayrollPostingService(_db).PostAsync(null!, "admin"));
        Assert.Contains("Professional", payroll.Message);
        await Assert.ThrowsAsync<AccountingException>(() =>
            new ConsolidatedTrialBalanceQuery(_db, new ExchangeRateService(_db)).ExecuteAsync(1, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        await Assert.ThrowsAsync<AccountingException>(() => new ProjectProfitabilityQuery(_db).ExecuteAsync(company));
    }

    [Fact]
    public async Task AProfessionalLicence_PassesTheGate()
    {
        var company = await InitAsync("منشأة احترافية");
        using var _ = LicenseGate.Use(Professional);

        Assert.Empty(await new ProjectProfitabilityQuery(_db).ExecuteAsync(company));
        var group = await Assert.ThrowsAsync<AccountingException>(() =>
            new ConsolidatedTrialBalanceQuery(_db, new ExchangeRateService(_db)).ExecuteAsync(999, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        Assert.DoesNotContain("Professional", group.Message);
    }

    [Fact]
    public async Task TheCompanyLimit_StopsTheNextCompany_OnBothProvisioningPaths()
    {
        await InitAsync("المنشأة الأولى");
        if (!await _db.BillingPlans.AnyAsync(p => p.Code == "STANDARD"))
        {
            _db.BillingPlans.Add(BillingPlan.Create("STANDARD", "قياسي", 1));
            await _db.SaveChangesAsync();
        }

        using (LicenseGate.Use(new LicenseEntitlements(LicenseEdition.Standard, 2)))
        {
            await new OrganizationProvisioningService(_db).ProvisionAsync(Request("المنشأة الثانية"));
            var org = await Assert.ThrowsAsync<AccountingException>(() =>
                new OrganizationProvisioningService(_db).ProvisionAsync(Request("المنشأة الثالثة")));
            Assert.Contains("(2)", org.Message);
            await Assert.ThrowsAsync<AccountingException>(() =>
                new TenantProvisioningService(_db).ProvisionAsync(new TenantProvisionRequest("مستأجر", null, "STANDARD")));
        }

        Assert.Equal(2, await _db.Companies.CountAsync());
        // ‏بلا حدٍّ في الترخيص لا حصر.
        await new OrganizationProvisioningService(_db).ProvisionAsync(Request("المنشأة الثالثة"));
        Assert.Equal(3, await _db.Companies.CountAsync());
    }

    [Theory]
    [InlineData("/api/v1/company-groups/4/reports/consolidated-trial-balance", LicensedCapability.Consolidation)]
    [InlineData("/api/v1/companies/7/fx-closing-translation", LicensedCapability.MultiCurrency)]
    [InlineData("/api/v1/exchange-rates", LicensedCapability.MultiCurrency)]
    [InlineData("/api/v1/companies/7/exchange-rates/sync", LicensedCapability.MultiCurrency)]
    [InlineData("/api/v1/reports/project-profitability", LicensedCapability.ProjectsContracts)]
    public void TheApiRoutes_OfProfessionalFeatures_AreNamed(string path, LicensedCapability capability) =>
        Assert.Equal(capability, LicensedApiRoutes.CapabilityFor(path));

    [Theory]
    [InlineData("/api/v1/reports/trial-balance")]
    [InlineData("/api/v1/exchange-rates/lookup")]
    [InlineData("/api/v1/companies")]
    public void OtherApiRoutes_AreNotGated(string path) => Assert.Null(LicensedApiRoutes.CapabilityFor(path));

    private static OrganizationProvisionRequest Request(string name) => new(name, "", BusinessProfile.General);

    private async Task<long> InitAsync(string name) =>
        (await new DatabaseInitializer(_db).InitializeAsync(name, "300000000000157", BusinessProfile.General)).Id;
}
