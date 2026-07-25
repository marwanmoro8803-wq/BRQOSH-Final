using System.Text.Json;
using Brqosh.Application.Platform;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Platform;

/// <summary>
/// مُفرِّغ طابور الأحداث (Outbox — ع-D2 / CEEF-204).
///
/// يُستدعى بعد نجاح الالتزام: يقرأ الأحداث التي لم تُرسَل بعد ويُطلق الويبهوكس،
/// ثم يعلّمها مُرسَلة. الحدث الذي يفشل إرساله يبقى في الطابور بعدّاد محاولات وسبب
/// آخر إخفاق — فلا يضيع ولا يُعاد إرساله بلا أثر.
///
/// الضمانة: **لا يُرسَل حدث عن مستند لم يلتزم**، لأن الحدث نفسه يُكتب داخل معاملة
/// المستند فيرتدّ معه.
/// </summary>
public sealed class OutboxDispatcher(IWebhookDispatcher webhooks)
{
    /// <summary>سقف الدفعة — يمنع أن يعلّق تفريغ طابور متضخم عمليةً تفاعلية.</summary>
    public const int BatchSize = 100;

    public async Task<int> DispatchPendingAsync(BrqoshDbContext db, CancellationToken ct = default)
    {
        var pending = await db.StoredDomainEvents
            .Where(e => e.DispatchedAtUtc == null)
            .OrderBy(e => e.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return 0;

        var dispatched = 0;
        foreach (var stored in pending)
        {
            try
            {
                var domainEvent = Rehydrate(stored);
                if (domainEvent is null)
                {
                    // نوع غير معروف (نسخة أقدم/أحدث) — لا تُبقِه يدور في الطابور للأبد.
                    stored.MarkDispatched();
                    continue;
                }

                await webhooks.DispatchAsync(domainEvent, ct);
                stored.MarkDispatched();
                dispatched++;
            }
            catch (Exception ex)
            {
                stored.MarkDispatchFailed(ex.Message);
            }
        }

        await db.SaveChangesAsync(ct);
        return dispatched;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>إعادة بناء الحدث من الحمولة المخزّنة — null لنوع غير معروف.</summary>
    private static DomainEvent? Rehydrate(StoredDomainEvent stored) => stored.EventType switch
    {
        nameof(JournalEntryPostedEvent) =>
            JsonSerializer.Deserialize<JournalEntryPostedEvent>(stored.PayloadJson, JsonOptions),
        nameof(DocumentPostedEvent) =>
            JsonSerializer.Deserialize<DocumentPostedEvent>(stored.PayloadJson, JsonOptions),
        nameof(DocumentCancelledEvent) =>
            JsonSerializer.Deserialize<DocumentCancelledEvent>(stored.PayloadJson, JsonOptions),
        _ => null,
    };
}
