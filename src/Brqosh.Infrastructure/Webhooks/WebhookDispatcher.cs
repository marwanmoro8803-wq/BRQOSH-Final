using System.Net.Http.Json;
using System.Text.Json;
using Brqosh.Application.Platform;
using Brqosh.Domain.Platform;
using Microsoft.Extensions.Logging;

namespace Brqosh.Infrastructure.Webhooks;

/// <summary>إرسال HTTP POST للمشتركين — لا يفشل الترحيل المحاسبي عند خطأ شبكة.</summary>
public sealed class WebhookDispatcher(HttpClient http, ILogger<WebhookDispatcher>? logger = null) : IWebhookDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task DispatchAsync(DomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var eventType = domainEvent.GetType().Name;
        var subs = WebhookSubscriptionRegistry.ListForEvent(eventType);
        if (subs.Count == 0)
            return;

        var envelope = new
        {
            eventId = domainEvent.EventId,
            eventType,
            occurredAtUtc = domainEvent.OccurredAtUtc,
            payload = domainEvent,
        };

        foreach (var sub in subs)
        {
            try
            {
                using var response = await http.PostAsJsonAsync(sub.TargetUrl, envelope, JsonOptions, ct);
                if (!response.IsSuccessStatusCode)
                    logger?.LogWarning("Webhook {Url} returned {Status} for {EventType}",
                        sub.TargetUrl, (int)response.StatusCode, eventType);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Webhook dispatch failed for {Url}", sub.TargetUrl);
            }
        }
    }
}
