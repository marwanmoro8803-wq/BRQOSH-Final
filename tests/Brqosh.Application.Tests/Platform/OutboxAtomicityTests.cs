using Brqosh.Application.Platform;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Platform;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// ع-D2 (CEEF-204) — نمط Outbox: أحداث النطاق تُكتب داخل معاملة المستند،
/// والإرسال الخارجي يقع بعد الالتزام حصراً.
///
/// قبل الإصلاح كان الناشر يفتح سياقاً منفصلاً (اتصال آخر) فيحفظ الحدث ويُطلق الويبهوك
/// فوراً — فترحيلٌ يرتدّ لاحقاً يترك حدثاً «شبحاً» ويُخطر أنظمة خارجية عن فاتورة لا وجود لها.
/// </summary>
public class OutboxAtomicityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _productId;
    private readonly RecordingWebhookDispatcher _webhooks = new();

    public OutboxAtomicityTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";

        // ناشر يكتب في السياق المحيط (سلوك الإنتاج) بلا حاوية حقن.
        DomainEventHub.Current = new AmbientOutboxPublisher();
        OutboxDispatchHook.Wire((db, ct) => new OutboxDispatcher(_webhooks).DispatchPendingAsync(db, ct));
    }

    public void Dispose()
    {
        OutboxDispatchHook.Reset();
        DomainEventHub.Current = new InMemoryDomainEventPublisher();
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة الطابور", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل الطابور", "311122233300003");
        var product = Product.Create(_companyId, "OB-P", "صنف الطابور", "قطعة", ProductType.Stockable, 100m);
        _db.Customers.Add(customer);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _productId = product.Id;
    }

    /// <summary>ترحيل ناجح: الحدث محفوظ، والويبهوك أُطلق بعد الالتزام، والحدث معلَّم مُرسَلاً.</summary>
    [Fact]
    public async Task SuccessfulPosting_StoresEvent_AndDispatchesAfterCommit()
    {
        await SetupAsync();

        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 7, 9), "قيد الطابور");
        var cash = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == "1101" && !a.IsGroup);
        var capital = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == "3101" && !a.IsGroup);
        entry.AddLine(cash, 500m, 0m);
        entry.AddLine(capital, 0m, 500m);

        await new JournalPostingService(_db).PostAsync(entry);

        var events = await _db.StoredDomainEvents.AsNoTracking().ToListAsync();
        Assert.Single(events);
        Assert.False(events[0].IsPendingDispatch);          // أُرسل بعد الالتزام
        Assert.Single(_webhooks.Dispatched);
    }

    /// <summary>
    /// ترحيل يفشل داخل المعاملة: لا حدث «شبح» في القاعدة، ولا إخطار خارجي —
    /// وهو جوهر ضمانة Outbox.
    /// </summary>
    [Fact]
    public async Task FailedPosting_LeavesNoGhostEvent_AndDispatchesNothing()
    {
        await SetupAsync();

        // فاتورة بكمية تتجاوز المخزون: الترحيل يرتدّ داخل المعاملة بعد نشر الحدث.
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("بيع بلا رصيد", 999m, 300m, _productId);

        await Assert.ThrowsAnyAsync<Exception>(
            () => new SalesInvoicePostingService(_db).PostAsync(invoice));

        // السياق الجاري قد يحمل كياناً متتبَّعاً من المحاولة المرتدّة — نقرأ من القاعدة مباشرة.
        await using var verify = new BrqoshDbContext(
            new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);

        Assert.Empty(await verify.StoredDomainEvents.AsNoTracking().ToListAsync());
        Assert.Empty(await verify.SalesInvoices.AsNoTracking().ToListAsync());
        Assert.Empty(_webhooks.Dispatched);
    }

    /// <summary>فشل الإرسال الخارجي لا يُفشل الترحيل — الحدث يبقى في الطابور بسبب الإخفاق.</summary>
    [Fact]
    public async Task WebhookFailure_DoesNotFailPosting_AndKeepsEventPending()
    {
        await SetupAsync();
        _webhooks.ThrowOnDispatch = true;

        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 7, 9), "قيد رغم فشل الويبهوك");
        var cash = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == "1101" && !a.IsGroup);
        var capital = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == "3101" && !a.IsGroup);
        entry.AddLine(cash, 250m, 0m);
        entry.AddLine(capital, 0m, 250m);

        var posted = await new JournalPostingService(_db).PostAsync(entry);
        Assert.Equal(JournalEntryStatus.Posted, posted.Status);   // الترحيل نجح

        var stored = await _db.StoredDomainEvents.AsNoTracking().SingleAsync();
        Assert.True(stored.IsPendingDispatch);                    // باقٍ في الطابور
        Assert.Equal(1, stored.DispatchAttempts);
        Assert.False(string.IsNullOrWhiteSpace(stored.LastDispatchError));
    }

    /// <summary>إعادة المحاولة تنجح لاحقاً: الحدث المعلّق يُرسَل ويُعلَّم.</summary>
    [Fact]
    public async Task PendingEvent_IsDispatchedOnRetry()
    {
        await SetupAsync();
        _webhooks.ThrowOnDispatch = true;

        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 7, 9), "قيد لإعادة المحاولة");
        var cash = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == "1101" && !a.IsGroup);
        var capital = await _db.Accounts.FirstAsync(a => a.CompanyId == _companyId && a.Code == "3101" && !a.IsGroup);
        entry.AddLine(cash, 75m, 0m);
        entry.AddLine(capital, 0m, 75m);
        await new JournalPostingService(_db).PostAsync(entry);

        Assert.True((await _db.StoredDomainEvents.AsNoTracking().SingleAsync()).IsPendingDispatch);

        _webhooks.ThrowOnDispatch = false;
        var sent = await new OutboxDispatcher(_webhooks).DispatchPendingAsync(_db);

        Assert.Equal(1, sent);
        Assert.False((await _db.StoredDomainEvents.AsNoTracking().SingleAsync()).IsPendingDispatch);
    }

    /// <summary>ناشر اختبار يحاكي سلوك الإنتاج: الكتابة في السياق المحيط دون إرسال.</summary>
    private sealed class AmbientOutboxPublisher : IDomainEventPublisher
    {
        private readonly List<DomainEvent> _drained = [];

        public async Task PublishAsync(DomainEvent domainEvent, CancellationToken ct = default)
        {
            _drained.Add(domainEvent);
            var ambient = AmbientDbContext.Current;
            Assert.NotNull(ambient);   // النشر أثناء ترحيل يجب أن يجد معاملة جارية

            var payload = System.Text.Json.JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
            ambient!.StoredDomainEvents.Add(StoredDomainEvent.From(
                domainEvent, PersistingDomainEventPublisher.ExtractCompanyId(domainEvent), payload));
            await ambient.SaveChangesAsync(ct);
        }

        public IReadOnlyList<DomainEvent> Drain()
        {
            var copy = _drained.ToList();
            _drained.Clear();
            return copy;
        }
    }

    private sealed class RecordingWebhookDispatcher : IWebhookDispatcher
    {
        public List<DomainEvent> Dispatched { get; } = [];
        public bool ThrowOnDispatch { get; set; }

        public Task DispatchAsync(DomainEvent domainEvent, CancellationToken ct = default)
        {
            if (ThrowOnDispatch)
                throw new InvalidOperationException("محاكاة فشل الويبهوك");
            Dispatched.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}
