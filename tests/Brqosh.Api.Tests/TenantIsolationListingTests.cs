using System.Net;
using Brqosh.Infrastructure.Companies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Brqosh.Api.Tests;

/// <summary>
/// ع-A1 (CEEF-200): في وضع SaaS، سرد الشركات/المجموعات بلا هوية مستأجر يجب أن يُرفض 403 —
/// وإلا سرّب حاملُ المفتاح المشترك كل الشركات وأرقامها الضريبية بحذف ترويسة X-Tenant-Id.
/// </summary>
public class TenantIsolationListingTests
{
    private static IsolatedApiFactory SaaSFactory() =>
        new(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [Brqosh.Infrastructure.Security.ApiSecuritySettings.SaaSModeEnv] = "true",
            })));

    [Fact]
    public async Task Companies_InSaaSMode_WithoutTenantHeader_Returns403()
    {
        using var factory = SaaSFactory();
        try
        {
            var client = factory.CreateClient();
            var response = await client.GetAsync("/api/v1/companies");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            TenantContext.EnforceTenantIsolation = false;
        }
    }

    [Fact]
    public async Task CompanyGroups_InSaaSMode_WithoutTenantHeader_Returns403()
    {
        using var factory = SaaSFactory();
        try
        {
            var client = factory.CreateClient();
            var response = await client.GetAsync("/api/v1/company-groups");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            TenantContext.EnforceTenantIsolation = false;
        }
    }
}
