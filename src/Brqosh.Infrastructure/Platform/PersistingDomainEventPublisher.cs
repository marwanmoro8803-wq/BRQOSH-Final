using System.Collections.Concurrent;
using System.Text.Json;
using Brqosh.Domain.Platform;
using Brqosh.Application.Platform;
using Brqosh.Infrastructure.Observability;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Brqosh.Infrastructure.Platform;

/// <summary>
/// ناشر أحداث يحفظ في الذاكرة (للاختبارات Drain) وفي قاعدة البيانات عبر scope منفصل.
/// </summary>
public sealed class PersistingDomainEventPublisher : IDomainEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IServiceScopeFactory _scopes;
    private readonly IWebhookDispatcher _webhooks;
    private readonly ConcurrentQueue<DomainEvent> _events = new();

    public PersistingDomainEventPublisher(IServiceScopeFactory scopes, IWebhookDispatcher webhooks)
    {
        _scopes = scopes;
        _webhooks = webhooks;
    }

    public async Task PublishAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _events.Enqueue(domainEvent);

        var payloadJson = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions);
        var stored = StoredDomainEvent.From(domainEvent, ExtractCompanyId(domainEvent), payloadJson);

        // Outbox (ع-D2، CEEF-204): داخل معاملة جارية — اكتب الحدث في نفس السياق
        // فيلتزم أو يرتدّ مع المستند، ولا تُرسل شيئاً للخارج قبل الالتزام.
        // الإرسال يتولاه OutboxDispatchHook بعد نجاح الـcommit.
        if (AmbientDbContext.Current is { } ambient)
        {
            ambient.StoredDomainEvents.Add(stored);
            await ambient.SaveChangesAsync(ct);
            AppMetrics.IncEventStored();
            return;
        }

        // خارج أي معاملة (نشر مباشر): السلوك السابق — حفظ فوري ثم إرسال.
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrqoshDbContext>();
        db.StoredDomainEvents.Add(stored);
        await db.SaveChangesAsync(ct);
        AppMetrics.IncEventStored();

        try
        {
            await _webhooks.DispatchAsync(domainEvent, ct);
            stored.MarkDispatched();
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            stored.MarkDispatchFailed(ex.Message);
            try { await db.SaveChangesAsync(ct); } catch { /* لا تُخفِ الخطأ الأصلي */ }
            var log = scope.ServiceProvider.GetService<ILogger<PersistingDomainEventPublisher>>();
            log?.LogWarning(ex, "Webhook dispatch pipeline failed for {EventType}", domainEvent.GetType().Name);
        }
    }

    public IReadOnlyList<DomainEvent> Drain()
    {
        var list = new List<DomainEvent>();
        while (_events.TryDequeue(out var e))
            list.Add(e);
        return list;
    }

    public static long? ExtractCompanyId(DomainEvent domainEvent) => domainEvent switch
    {
        JournalEntryPostedEvent j => j.CompanyId,
        DocumentPostedEvent d => d.CompanyId,
        DocumentCancelledEvent c => c.CompanyId,
        _ => null,
    };
}
