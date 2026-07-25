using System.Net;
using System.Net.Http.Json;
using Brqosh.Infrastructure.Webhooks;

namespace Brqosh.Api.Tests;

public class WebhookSubscriptionApiTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;

    public WebhookSubscriptionApiTests(ApiTestFixture fixture) =>
        _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Register_and_list_webhook_subscription()
    {
        WebhookSubscriptionRegistry.ResetForTests();

        var response = await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions", new
        {
            EventType = "DocumentPosted",
            TargetUrl = "https://example.com/hooks/brqosh",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var list = await _client.GetFromJsonAsync<List<WebhookSubscription>>("/api/v1/webhooks/subscriptions");
        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.Equal("DocumentPosted", list[0].EventType);
    }

    [Fact]
    public async Task Register_private_url_returns_bad_request()
    {
        WebhookSubscriptionRegistry.ResetForTests();

        var response = await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions", new
        {
            EventType = "DocumentPosted",
            TargetUrl = "https://127.0.0.1/hooks/brqosh",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_invalid_url_returns_bad_request()
    {
        WebhookSubscriptionRegistry.ResetForTests();

        var response = await _client.PostAsJsonAsync("/api/v1/webhooks/subscriptions", new
        {
            EventType = "DocumentPosted",
            TargetUrl = "not-a-url",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
