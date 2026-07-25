using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Platform;

namespace Brqosh.Application.Tests.Platform;

public class DomainEventPublisherTests
{
    [Fact]
    public async Task InMemoryPublisher_queues_and_drains_events()
    {
        var pub = new InMemoryDomainEventPublisher();
        await pub.PublishAsync(new DocumentPostedEvent(1, "SalesInvoice", 9, "SI-1"));
        await pub.PublishAsync(new DocumentCancelledEvent(1, "SalesInvoice", 9, "SI-1", "خطأ"));
        var drained = pub.Drain();
        Assert.Equal(2, drained.Count);
        Assert.Empty(pub.Drain());
    }
}
