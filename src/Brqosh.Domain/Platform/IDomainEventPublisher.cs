namespace Brqosh.Domain.Platform;

/// <summary>ناشر أحداث نطاق داخل العملية — استبداله بـ bus خارجي في SaaS.</summary>
public interface IDomainEventPublisher
{
    Task PublishAsync(DomainEvent domainEvent, CancellationToken ct = default);
    IReadOnlyList<DomainEvent> Drain();
}
