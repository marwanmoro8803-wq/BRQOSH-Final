using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

public class EventTrackingQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public EventTrackingQueryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        new BrqoshSchemaUpdater(_db).ApplyAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_GroupsStoredEvents_ByEventType()
    {
        const long companyId = 42;
        var now = DateTime.UtcNow;

        _db.StoredDomainEvents.Add(StoredDomainEvent.From(
            new JournalEntryPostedEvent(companyId, 1, "JE-1", new DateOnly(2026, 7, 1)),
            companyId,
            """{"journalEntryId":1}"""));
        _db.StoredDomainEvents.Add(StoredDomainEvent.From(
            new JournalEntryPostedEvent(companyId, 2, "JE-2", new DateOnly(2026, 7, 2)),
            companyId,
            """{"journalEntryId":2}"""));
        _db.StoredDomainEvents.Add(StoredDomainEvent.From(
            new DocumentPostedEvent(companyId, "SalesInvoice", 10, "INV-10"),
            companyId,
            """{"documentId":10}"""));
        await _db.SaveChangesAsync();

        var query = new EventTrackingQuery(_db);
        var rows = await query.ExecuteAsync(companyId, now.AddHours(-1), now.AddHours(1));

        Assert.Equal(2, rows.Count);
        var jeRow = rows.Single(r => r.EventType == nameof(JournalEntryPostedEvent));
        Assert.Equal(2, jeRow.Count);
        var docRow = rows.Single(r => r.EventType == nameof(DocumentPostedEvent));
        Assert.Equal(1, docRow.Count);
    }

    [Fact]
    public async Task ExecuteAsync_FiltersByUtcRange()
    {
        const long companyId = 7;
        var baseTime = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        var oldEvent = StoredDomainEvent.From(
            new JournalEntryPostedEvent(companyId, 1, "JE-OLD", new DateOnly(2026, 6, 1)),
            companyId,
            "{}");
        _db.Entry(oldEvent).Property(nameof(StoredDomainEvent.OccurredAtUtc)).CurrentValue =
            baseTime.AddDays(-2);

        var recentEvent = StoredDomainEvent.From(
            new JournalEntryPostedEvent(companyId, 2, "JE-NEW", new DateOnly(2026, 7, 1)),
            companyId,
            "{}");
        _db.Entry(recentEvent).Property(nameof(StoredDomainEvent.OccurredAtUtc)).CurrentValue =
            baseTime;

        _db.StoredDomainEvents.AddRange(oldEvent, recentEvent);
        await _db.SaveChangesAsync();

        var query = new EventTrackingQuery(_db);
        var rows = await query.ExecuteAsync(companyId, baseTime.AddHours(-1), baseTime.AddHours(1));

        var row = Assert.Single(rows);
        Assert.Equal(1, row.Count);
    }
}
