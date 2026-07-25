using System.Collections.Concurrent;
using Brqosh.Domain.Platform;

namespace Brqosh.Infrastructure.Platform;

/// <summary>ناشر في الذاكرة — يسجّل الأحداث للتحقق/التكامل؛ لا يرسل خارج العملية بعد.</summary>
public sealed class InMemoryDomainEventPublisher : IDomainEventPublisher
{
    private readonly ConcurrentQueue<DomainEvent> _events = new();

    public Task PublishAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _events.Enqueue(domainEvent);
        return Task.CompletedTask;
    }

    public IReadOnlyList<DomainEvent> Drain()
    {
        var list = new List<DomainEvent>();
        while (_events.TryDequeue(out var e))
            list.Add(e);
        return list;
    }
}
