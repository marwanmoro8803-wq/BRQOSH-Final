using Brqosh.Domain.Accounting;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

public class FiscalPeriodAuditTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public FiscalPeriodAuditTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    [Fact]
    public async Task Close_and_reopen_log_audit_entries()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة", "300000000000020",
            Brqosh.Domain.Modules.BusinessProfile.General);

        var period = FiscalPeriod.Create(company.Id, 2026, 7);
        _db.FiscalPeriods.Add(period);
        await _db.SaveChangesAsync();

        var service = new FiscalPeriodService(_db);
        await service.CloseAsync(company.Id, period.Id, "admin");
        await service.ReopenAsync(company.Id, period.Id, "admin");

        var logs = await _db.AuditLogEntries
            .Where(a => a.CompanyId == company.Id && a.EntityType == "FiscalPeriod")
            .ToListAsync();

        Assert.Contains(logs, l => l.Action == "إغلاق فترة مالية");
        Assert.Contains(logs, l => l.Action == "إعادة فتح فترة مالية");
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
