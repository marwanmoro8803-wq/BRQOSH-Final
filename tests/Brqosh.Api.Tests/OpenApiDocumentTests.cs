namespace Brqosh.Api.Tests;

public class OpenApiDocumentTests
{
    [Fact]
    public async Task Swagger_json_available_in_development()
    {
        await using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"title\": \"BRQOSH API\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/health/live", body, StringComparison.OrdinalIgnoreCase);
    }
}
