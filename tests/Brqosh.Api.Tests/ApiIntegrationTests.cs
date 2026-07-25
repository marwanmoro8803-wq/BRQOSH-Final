using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Brqosh.Api.Tests;

public class ApiIntegrationTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(ApiTestFixture fixture) =>
        _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Brqosh.Api", body);
    }

    [Fact]
    public async Task HealthReady_ReturnsOk()
    {
        var response = await _client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Brqosh.Api", body);
        Assert.Contains("\"database\":\"ok\"", body);
    }

    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        var response = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("liveness", body);
    }

    [Fact]
    public async Task CompanyGroups_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/company-groups");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Companies_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/companies");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TrialBalance_WithoutCompanyId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/v1/reports/trial-balance?from=2026-01-01&to=2026-12-31");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customers_WithoutCompanyId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/v1/customers");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Branches_WithoutCompanyId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/v1/branches");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DomainEvents_WithoutCompanyId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/v1/domain-events");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DataQuality_WithoutCompanyId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/v1/data-quality");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EventTracking_WithoutCompanyId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/v1/event-tracking");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReportSnapshots_WithoutCompanyId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/v1/report-snapshots");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Intelligence_WithoutCompanyId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/v1/intelligence/financial");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
