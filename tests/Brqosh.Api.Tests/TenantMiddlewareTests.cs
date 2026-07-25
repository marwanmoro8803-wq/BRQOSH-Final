using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Brqosh.Api.Tests;

public class TenantMiddlewareTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public TenantMiddlewareTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Customers_WithWrongTenant_Returns403()
    {
        var client = _fixture.Factory.CreateClient();
        var companiesResponse = await client.GetAsync("/api/v1/companies");
        Assert.Equal(HttpStatusCode.OK, companiesResponse.StatusCode);

        var companiesJson = await companiesResponse.Content.ReadAsStringAsync();
        if (!companiesJson.Contains("\"id\"", StringComparison.OrdinalIgnoreCase))
            return;

        var companyId = ExtractFirstId(companiesJson);
        var tenantId = ExtractFirstTenantId(companiesJson);
        Assert.NotNull(companyId);
        Assert.NotNull(tenantId);

        client.DefaultRequestHeaders.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        var response = await client.GetAsync($"/api/v1/customers?companyId={companyId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customers_WithMatchingTenant_DoesNotReturn403()
    {
        var client = _fixture.Factory.CreateClient();
        var companiesResponse = await client.GetAsync("/api/v1/companies");
        var companiesJson = await companiesResponse.Content.ReadAsStringAsync();
        if (!companiesJson.Contains("\"id\"", StringComparison.OrdinalIgnoreCase))
            return;

        var companyId = ExtractFirstId(companiesJson);
        var tenantId = ExtractFirstTenantId(companiesJson);
        Assert.NotNull(companyId);
        Assert.NotNull(tenantId);

        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        var response = await client.GetAsync($"/api/v1/customers?companyId={companyId}");
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Health_RemainsOpen_WithTenantHeader()
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string? ExtractFirstId(string json)
    {
        var marker = "\"id\":";
        var idx = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + marker.Length;
        var end = json.IndexOf(',', start);
        if (end < 0) end = json.IndexOf('}', start);
        return json[start..end].Trim();
    }

    private static string? ExtractFirstTenantId(string json)
    {
        var marker = "\"tenantId\":";
        var idx = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + marker.Length;
        var end = json.IndexOf(',', start);
        if (end < 0) end = json.IndexOf('}', start);
        var raw = json[start..end].Trim().Trim('"');
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }
}
