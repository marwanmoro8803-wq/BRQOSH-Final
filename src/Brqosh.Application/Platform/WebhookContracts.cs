using Brqosh.Domain.Platform;

namespace Brqosh.Application.Platform;

public interface IWebhookDispatcher
{
    Task DispatchAsync(DomainEvent domainEvent, CancellationToken ct = default);
}
