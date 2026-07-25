using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Brqosh.Api.Tests;

public class ApiKeyGuardTests
{
    [Fact]
    public async Task ProtectedRoute_WithoutApiKey_Returns401_WhenKeyConfigured()
    {
        await using var factory = new IsolatedApiFactory(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BRQOSH_API_KEY"] = "test-secret-key",
                });
            });
        });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/customers?companyId=1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WithCorrectApiKey_DoesNotReturn401()
    {
        await using var factory = new IsolatedApiFactory(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BRQOSH_API_KEY"] = "test-secret-key",
                });
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-secret-key");
        var response = await client.GetAsync("/api/v1/customers?companyId=1");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_RemainsOpen_WhenApiKeyConfigured()
    {
        await using var factory = new IsolatedApiFactory(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BRQOSH_API_KEY"] = "test-secret-key",
                });
            });
        });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
