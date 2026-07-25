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

    private StoredDomainEvent()
    {
        EventType = null!;
        PayloadJson = null!;
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
