using System.Net;
using System.Net.Http.Json;
using Brqosh.Application.Platform;
using Brqosh.Domain.Modules;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Brqosh.Api.Tests;

public class TenantProvisioningApiTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;

    public TenantProvisioningApiTests(ApiTestFixture fixture) =>
        _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task BillingPlans_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/billing-plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Provision_requires_key_when_configured()
    {
        await using var factory = new IsolatedApiFactory(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [Brqosh.Infrastructure.Security.ApiSecuritySettings.ProvisioningKeyEnv] = "provision-test-key",
                });
            });
        });

        var client = factory.CreateClient();
        var denied = await client.PostAsJsonAsync("/api/v1/tenants/provision",
            new TenantProvisionRequest("Denied Co", "300000000000099", "TRIAL", BusinessProfile.General));
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        client.DefaultRequestHeaders.Add("X-Provisioning-Key", "provision-test-key");
        var ok = await client.PostAsJsonAsync("/api/v1/tenants/provision",
            new TenantProvisionRequest("Allowed Co", "300000000000100", "TRIAL", BusinessProfile.General));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Provision_then_tenant_isolation()
    {
        var provision = await _client.PostAsJsonAsync("/api/v1/tenants/provision",
            new TenantProvisionRequest("API Tenant Co", "300000000000088", "TRIAL", BusinessProfile.General));
        Assert.Equal(HttpStatusCode.OK, provision.StatusCode);

        var body = await provision.Content.ReadFromJsonAsync<ProvisionResponse>();
        Assert.NotNull(body);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/companies");
        req.Headers.Add("X-Tenant-Id", body!.TenantId.ToString());
        var companies = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, companies.StatusCode);

        var list = await companies.Content.ReadFromJsonAsync<List<CompanyRow>>();
        Assert.NotNull(list);
        Assert.All(list!, c => Assert.Equal(body.TenantId, c.TenantId));
    }

    private sealed record ProvisionResponse(Guid TenantId, long CompanyId, string PlanCode);
    private sealed record CompanyRow(long Id, Guid TenantId);
}
