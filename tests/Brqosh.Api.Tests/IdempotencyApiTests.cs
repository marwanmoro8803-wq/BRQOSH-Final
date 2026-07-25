using System.Net;
using System.Net.Http.Json;
using Brqosh.Api.Idempotency;
using Brqosh.Infrastructure.Webhooks;

namespace Brqosh.Api.Tests;

public class IdempotencyApiTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;

    public IdempotencyApiTests(ApiTestFixture fixture) =>
        _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Post_with_same_idempotency_key_returns_cached_response()
    {
        IdempotencyStore.ResetForTests();
        WebhookSubscriptionRegistry.ResetForTests();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/subscriptions")
        {
            Content = JsonContent.Create(new
            {
                EventType = "DocumentPosted",
                TargetUrl = "https://example.com/hooks/idempotent",
            }),
        };
        request.Headers.Add("Idempotency-Key", "key-webhook-001");

        var first = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstBody = await first.Content.ReadAsStringAsync();

        var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/subscriptions")
        {
            Content = JsonContent.Create(new
            {
                EventType = "DocumentPosted",
                TargetUrl = "https://example.com/hooks/other",
            }),
        };
        secondRequest.Headers.Add("Idempotency-Key", "key-webhook-001");

        var second = await _client.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondBody = await second.Content.ReadAsStringAsync();

        Assert.Equal(firstBody, secondBody);
        Assert.Single(WebhookSubscriptionRegistry.List());
    }
}
