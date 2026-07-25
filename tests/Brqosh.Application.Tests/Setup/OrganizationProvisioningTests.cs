using Brqosh.Application.Setup;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Setup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Setup;

public class OrganizationProvisioningTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public OrganizationProvisioningTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Theory]
    [InlineData(BusinessProfile.Healthcare)]
    [InlineData(BusinessProfile.Logistics)]
    [InlineData(BusinessProfile.RealEstate)]
    [InlineData(BusinessProfile.Hospitality)]
    public async Task Provision_creates_company_with_chart_for_new_profiles(BusinessProfile profile)
    {
        await SeedAdminAsync();

        var service = new OrganizationProvisioningService(_db);
        var result = await service.ProvisionAsync(new OrganizationProvisionRequest(
            $"شركة {profile}", "310123456700003", profile));

        Assert.True(result.CompanyId > 0);
        var accounts = await _db.Accounts.CountAsync(a => a.CompanyId == result.CompanyId);
        Assert.True(accounts > 10);
        var company = await _db.Companies.SingleAsync(c => c.Id == result.CompanyId);
        Assert.Equal(profile, company.Profile);
    }

    [Fact]
    public async Task Provision_with_admin_creates_user()
    {
        await SeedAdminAsync();

        var service = new OrganizationProvisioningService(_db);
        var result = await service.ProvisionAsync(new OrganizationProvisionRequest(
            "شركة ثانية", "", BusinessProfile.Retail,
            AdminUsername: "admin2", AdminPassword: "SecurePass12!", AdminDisplayName: "مدير 2"));

        Assert.NotNull(result.AdminUserId);
        var user = await _db.AppUsers.SingleAsync(u => u.Id == result.AdminUserId);
        Assert.Equal(UserRole.Administrator, user.Role);
        Assert.Equal(result.CompanyId, user.CompanyId);
    }

    private async Task SeedAdminAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("أولى", "310123456700003", BusinessProfile.General);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "seed";
        await new UserAuthService(_db).CreateInitialAdminAsync(company.Id, "seed", "SecurePass12!");
    }
}
