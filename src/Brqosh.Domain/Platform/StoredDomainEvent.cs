namespace Brqosh.Domain.Platform;

/// <summary>حدث نطاق مُخزَّن — سجل دائم لأحداث DomainEvent المنشورة.</summary>
public class StoredDomainEvent
{
    public long Id { get; private set; }
    public Guid EventId { get; private set; }
    public long? CompanyId { get; private set; }
    public string EventType { get; private set; }
    public string PayloadJson { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>وقت نجاح الإرسال الخارجي (ويبهوك). null = ما زال في الطابور (Outbox).</summary>
    public DateTime? DispatchedAtUtc { get; private set; }

    /// <summary>عدد محاولات الإرسال — يميّز «لم يُحاول» عن «فشل مراراً».</summary>
    public int DispatchAttempts { get; private set; }

    /// <summary>سبب آخر إخفاق في الإرسال — للتشخيص دون فتح السجلات.</summary>
    public string? LastDispatchError { get; private set; }

    /// <summary>الحدث ينتظر إرسالاً خارجياً.</summary>
    public bool IsPendingDispatch => DispatchedAtUtc is null;

    private StoredDomainEvent()
    {
        EventType = null!;
        PayloadJson = null!;
    }

    public void MarkDispatched()
    {
        DispatchedAtUtc = DateTime.UtcNow;
        DispatchAttempts++;
        LastDispatchError = null;
    }

    public void MarkDispatchFailed(string error)
    {
        DispatchAttempts++;
        LastDispatchError = error.Length > 512 ? error[..512] : error;
    }

    public static StoredDomainEvent From(DomainEvent e, long? companyId, string payloadJson) =>
        new()
        {
            EventId = e.EventId,
            CompanyId = companyId,
            EventType = e.GetType().Name,
            PayloadJson = payloadJson,
            OccurredAtUtc = e.OccurredAtUtc,
        };
}
