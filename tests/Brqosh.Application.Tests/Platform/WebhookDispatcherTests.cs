using System.Net;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Webhooks;

namespace Brqosh.Application.Tests.Platform;

public class WebhookDispatcherTests
{
    [Fact]
    public async Task DocumentPostedEvent_posts_to_registered_subscriber()
    {
        WebhookSubscriptionRegistry.ResetForTests();
        WebhookSubscriptionRegistry.Register("DocumentPosted", "https://hooks.test/brqosh");

        var handler = new RecordingHttpHandler();
        var dispatcher = new WebhookDispatcher(new HttpClient(handler));

        await dispatcher.DispatchAsync(new DocumentPostedEvent(1, "SalesInvoice", 5, "SI-5"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://hooks.test/brqosh", request.RequestUri?.ToString());
    }

    [Fact]
    public async Task Wildcard_subscription_receives_any_event()
    {
        WebhookSubscriptionRegistry.ResetForTests();
        WebhookSubscriptionRegistry.Register("*", "https://hooks.test/all");

        var handler = new RecordingHttpHandler();
        var dispatcher = new WebhookDispatcher(new HttpClient(handler));

        await dispatcher.DispatchAsync(new DocumentCancelledEvent(2, "SalesInvoice", 3, "SI-3", "test"));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task No_subscription_skips_http()
    {
        WebhookSubscriptionRegistry.ResetForTests();
        var handler = new RecordingHttpHandler();
        var dispatcher = new WebhookDispatcher(new HttpClient(handler));

        await dispatcher.DispatchAsync(new DocumentPostedEvent(1, "SalesInvoice", 5, "SI-5"));

        Assert.Empty(handler.Requests);
    }

    private sealed class RecordingHttpHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
