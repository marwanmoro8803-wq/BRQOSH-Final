namespace Brqosh.Infrastructure.Webhooks;

public sealed record WebhookSubscription(long Id, string EventType, string TargetUrl, DateTime CreatedAtUtc);

public sealed record WebhookSubscribeRequest(string EventType, string TargetUrl);

/// <summary>سجل اشتراكات Webhook — في الذاكرة (M9) · DB لاحقاً في SaaS.</summary>
public static class WebhookSubscriptionRegistry
{
    private static long _nextId = 1;
    private static readonly List<WebhookSubscription> Items = [];
    private static readonly Lock Gate = new();

    public static WebhookSubscription Register(string eventType, string targetUrl, bool allowInsecureLocal = false)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("eventType required");
        if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out _))
            throw new ArgumentException("targetUrl must be absolute URI");

        WebhookUrlValidator.ValidateOrThrow(targetUrl, allowInsecureLocal);

        lock (Gate)
        {
            var sub = new WebhookSubscription(_nextId++, eventType.Trim(), targetUrl.Trim(), DateTime.UtcNow);
            Items.Add(sub);
            return sub;
        }
    }

    public static IReadOnlyList<WebhookSubscription> List()
    {
        lock (Gate)
            return Items.ToList();
    }

    public static IReadOnlyList<WebhookSubscription> ListForEvent(string eventType)
    {
        lock (Gate)
        {
            return Items
                .Where(s => s.EventType == "*" ||
                            string.Equals(s.EventType, eventType, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(s.EventType + "Event", eventType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public static void ResetForTests()
    {
        lock (Gate)
        {
            Items.Clear();
            _nextId = 1;
        }
    }
}
