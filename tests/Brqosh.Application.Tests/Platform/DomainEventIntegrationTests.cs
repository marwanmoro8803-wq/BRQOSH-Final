using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Platform;

/// <summary>إثبات تكامل فعلي — ليس stub معزول. متسلسل بسبب DomainEventHub الثابت.</summary>
[Collection("SequentialEnv")]
public class DomainEventIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly InMemoryDomainEventPublisher _publisher;

    public DomainEventIntegrationTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _publisher = new InMemoryDomainEventPublisher();
        DomainEventHub.Current = _publisher;
    }

    public void Dispose()
    {
        DomainEventHub.Current = new InMemoryDomainEventPublisher();
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Initialize_SeedsHeadquartersBranch_AndSarCurrency()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة تكامل", "310123456700003", BusinessProfile.Retail);

        Assert.Equal("SAR", company.BaseCurrencyCode);
        Assert.Equal("SAR", company.FunctionalCurrencyCode);

        var hq = await _db.Branches.SingleAsync(b => b.CompanyId == company.Id);
        Assert.Equal("HQ", hq.Code);
        Assert.True(hq.IsHeadquarters);
        Assert.True(hq.IsActive);
    }

    [Fact]
    public async Task JournalPosting_Publishes_JournalEntryPostedEvent()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة أحداث", "310123456700003", BusinessProfile.Retail);

        var cash = await _db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "1101");
        var capital = await _db.Accounts.SingleAsync(a => a.CompanyId == company.Id && a.Code == "3101");
        var draft = JournalEntry.CreateDraft(company.Id, new DateOnly(2026, 8, 1), "اختبار حدث");
        draft.AddLine(cash, 100m, 0m);
        draft.AddLine(capital, 0m, 100m);

        _ = _publisher.Drain();
        await new JournalPostingService(_db).PostAsync(draft);

        var events = _publisher.Drain();
        var posted = Assert.Single(events.OfType<JournalEntryPostedEvent>());
        Assert.Equal(company.Id, posted.CompanyId);
        Assert.Equal(draft.Id, posted.JournalEntryId);
        Assert.False(string.IsNullOrWhiteSpace(posted.EntryNumber));
    }

    [Fact]
    public async Task SalesInvoicePosting_Publishes_DocumentAndJournalEvents()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة مبيعات", "310123456700003", BusinessProfile.Retail);
        var customer = Customer.Create(company.Id, "عميل أحداث");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 8, 2));
        invoice.AddLine("خدمة", 1m, 1000m);

        _ = _publisher.Drain();
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var events = _publisher.Drain();
        Assert.Contains(events, e => e is DocumentPostedEvent d
            && d.DocumentType == "SalesInvoice"
            && d.DocumentNumber == invoice.InvoiceNumber);
        Assert.Contains(events, e => e is JournalEntryPostedEvent);
    }
}
