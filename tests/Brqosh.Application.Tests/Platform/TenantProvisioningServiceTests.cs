using Brqosh.Application.Platform;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Platform;

public class TenantProvisioningServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public TenantProvisioningServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Provision_creates_company_subscription_and_chart()
    {
        await SeedPlansAsync();

        var result = await new TenantProvisioningService(_db).ProvisionAsync(
            new TenantProvisionRequest("شركة SaaS", "300000000000099", "TRIAL", BusinessProfile.General));

        Assert.NotEqual(Guid.Empty, result.TenantId);
        Assert.True(result.CompanyId > 0);

        var accounts = await _db.Accounts.CountAsync(a => a.CompanyId == result.CompanyId);
        Assert.True(accounts > 50);

        var sub = await new BillingPlanCatalog(_db).GetSubscriptionAsync(result.TenantId);
        Assert.NotNull(sub);
        Assert.Equal("TRIAL", sub!.PlanCode);
    }

    private async Task SeedPlansAsync()
    {
        if (await _db.BillingPlans.AnyAsync())
            return;
        _db.BillingPlans.AddRange(
            Domain.Platform.BillingPlan.Create("TRIAL", "تجريبي"),
            Domain.Platform.BillingPlan.Create("STANDARD", "قياسي"));
        await _db.SaveChangesAsync();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
