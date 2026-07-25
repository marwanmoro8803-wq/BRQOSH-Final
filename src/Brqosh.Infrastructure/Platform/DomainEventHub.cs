using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Platform;

namespace Brqosh.Infrastructure.Platform;

/// <summary>
/// نقطة نشر عالمية للعمليات المُنشأة بـ <c>new Service(db)</c> (خارج DI).
/// يُزامَن مع تسجيل DI عند توفره.
/// </summary>
public static class DomainEventHub
{
    private static IDomainEventPublisher _publisher = new InMemoryDomainEventPublisher();

    public static IDomainEventPublisher Current
    {
        get => _publisher;
        set => _publisher = value ?? throw new ArgumentNullException(nameof(value));
    }

    public static Task PublishAsync(DomainEvent domainEvent, CancellationToken ct = default) =>
        Current.PublishAsync(domainEvent, ct);

    public static IReadOnlyList<DomainEvent> Drain() => Current.Drain();
}
