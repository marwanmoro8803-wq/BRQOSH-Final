using Brqosh.Domain.Platform;
using Brqosh.Infrastructure;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Application.Tests.Platform;

[Collection("SequentialEnv")]
public class DomainEventPersistenceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _services;
    private readonly IDomainEventPublisher _publisher;

    public DomainEventPersistenceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"brqosh-events-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={_dbPath}";

        var collection = new ServiceCollection();
        collection.AddBrqoshInfrastructure(connectionString, usePostgres: false);
        _services = collection.BuildServiceProvider();
        _publisher = _services.GetRequiredService<IDomainEventPublisher>();

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrqoshDbContext>();
        db.Database.EnsureCreated();
        new BrqoshSchemaUpdater(db).ApplyAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        DomainEventHub.Current = new InMemoryDomainEventPublisher();
        _services.Dispose();
        try { File.Delete(_dbPath); } catch { /* */ }
    }

    [Fact]
    public async Task PersistingPublisher_WritesRow_ToDomainEventsTable()
    {
        var evt = new JournalEntryPostedEvent(42, 7, "JE-001", new DateOnly(2026, 7, 8));

        await _publisher.PublishAsync(evt);

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrqoshDbContext>();
        var row = await db.StoredDomainEvents.SingleAsync();

        Assert.Equal(evt.EventId, row.EventId);
        Assert.Equal(42, row.CompanyId);
        Assert.Equal(nameof(JournalEntryPostedEvent), row.EventType);
        Assert.Contains("journalEntryId", row.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(evt.OccurredAtUtc, row.OccurredAtUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task PersistingPublisher_Drain_StillReturnsInMemoryCopy()
    {
        var evt = new DocumentPostedEvent(1, "SalesInvoice", 99, "INV-1");

        await _publisher.PublishAsync(evt);

        var drained = _publisher.Drain();
        var doc = Assert.Single(drained.OfType<DocumentPostedEvent>());
        Assert.Equal(99, doc.DocumentId);
    }

    [Fact]
    public void ExtractCompanyId_ReturnsNull_ForUnknownEventTypes()
    {
        var unknown = new TestDomainEvent();
        Assert.Null(PersistingDomainEventPublisher.ExtractCompanyId(unknown));
    }

    private sealed record TestDomainEvent() : DomainEvent(DateTime.UtcNow);
}
