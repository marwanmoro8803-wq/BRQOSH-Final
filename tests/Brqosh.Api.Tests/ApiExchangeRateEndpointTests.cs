using System.Net;
using System.Net.Http.Json;
using Brqosh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Api.Tests;

public class ApiExchangeRateEndpointTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public ApiExchangeRateEndpointTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Record_and_lookup_exchange_rate()
    {
        var companyId = await SeedCompanyAsync();
        var client = _fixture.Factory.CreateClient();

        var post = await client.PostAsync(
            $"/api/v1/exchange-rates?companyId={companyId}&fromCurrency=USD&toCurrency=SAR&effectiveDate=2026-07-01&rate=3.75",
            null);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var lookup = await client.GetAsync(
            $"/api/v1/exchange-rates/lookup?companyId={companyId}&fromCurrency=USD&toCurrency=SAR&asOf=2026-07-15");
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        var body = await lookup.Content.ReadFromJsonAsync<LookupResponse>();
        Assert.Equal(3.75m, body!.Rate);
    }

    [Fact]
    public async Task List_exchange_rates_returns_ok()
    {
        var companyId = await SeedCompanyAsync();
        var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/companies/{companyId}/exchange-rates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<long> SeedCompanyAsync()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BrqoshDbContext>();
        var company = await new DatabaseInitializer(db)
            .InitializeAsync("API FX Co", "300000000000077", Domain.Modules.BusinessProfile.General);
        return company.Id;
    }

    private sealed record LookupResponse(decimal Rate);
}
