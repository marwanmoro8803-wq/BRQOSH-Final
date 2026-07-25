using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Security;

public class ImmutableAuditTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public ImmutableAuditTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditLogImmutabilityInterceptor())
            .Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AuditLog_Insert_Allowed_WithReason()
    {
        await new DatabaseInitializer(_db)
            .InitializeAsync("شركة تدقيق", "310123456700003", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        await new AuditService(_db).LogAsync(
            1, "admin", "ترحيل", "SalesInvoice", "INV-001", "تفاصيل", "سبب الترحيل");

        var entry = await _db.AuditLogEntries.SingleAsync();
        Assert.Equal("سبب الترحيل", entry.Reason);
    }

    [Fact]
    public async Task AuditLog_Update_Throws()
    {
        await new DatabaseInitializer(_db)
            .InitializeAsync("شركة تدقيق", "310123456700003", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        await new AuditService(_db).LogAsync(1, "admin", "ترحيل", "Test", "1");

        var entry = await _db.AuditLogEntries.SingleAsync();
        _db.Entry(entry).Property("Details").CurrentValue = "معدّل";

        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task AuditLog_Delete_Throws()
    {
        await new DatabaseInitializer(_db)
            .InitializeAsync("شركة تدقيق", "310123456700003", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        await new AuditService(_db).LogAsync(1, "admin", "ترحيل", "Test", "1");

        var entry = await _db.AuditLogEntries.SingleAsync();
        _db.AuditLogEntries.Remove(entry);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _db.SaveChangesAsync());
    }
}
