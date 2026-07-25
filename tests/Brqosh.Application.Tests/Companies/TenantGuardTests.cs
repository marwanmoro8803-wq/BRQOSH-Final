using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Companies;

public class TenantGuardTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public TenantGuardTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Allows_all_when_tenant_header_not_set()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "300000000000005", BusinessProfile.General);

        TenantContext.CurrentTenantId = null;
        Assert.True(await TenantGuard.IsCompanyAllowedAsync(_db, company.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_company_outside_current_tenant()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "300000000000006", BusinessProfile.General);

        TenantContext.CurrentTenantId = Guid.NewGuid();
        Assert.False(await TenantGuard.IsCompanyAllowedAsync(_db, company.Id, CancellationToken.None));

        TenantContext.CurrentTenantId = company.TenantId;
        Assert.True(await TenantGuard.IsCompanyAllowedAsync(_db, company.Id, CancellationToken.None));
    }

    public void Dispose()
    {
        TenantContext.CurrentTenantId = null;
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
