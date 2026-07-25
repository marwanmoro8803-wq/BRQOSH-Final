using Brqosh.Application.Platform;
using Brqosh.Domain.Platform;

namespace Brqosh.Infrastructure.Webhooks;

/// <summary>لا إرسال — Desktop / اختبارات بدون HttpClient.</summary>
public sealed class NullWebhookDispatcher : IWebhookDispatcher
{
    public static NullWebhookDispatcher Instance { get; } = new();
    public Task DispatchAsync(DomainEvent domainEvent, CancellationToken ct = default) =>
        Task.CompletedTask;
}
