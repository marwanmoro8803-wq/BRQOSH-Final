using System.Net;
using Brqosh.Application.Licensing;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Api.Tests;

/// <summary>‏(Phase 7 · security-licensing-tenancy#2) ‏مسارُ ميزةٍ احترافيّة بترخيصٍ قياسيّ يُرفض بـ403 <c>licence_tier</c> في الـAPI نفسه.</summary>
public class AStandardLicenceIsRefusedProfessionalRoutesTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public AStandardLicenceIsRefusedProfessionalRoutesTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ConsolidationAndProjects_AreForbidden_UnderAStandardLicence()
    {
        var previous = LicenseGate.Default;
        LicenseGate.Default = new FixedProvider(new LicenseEntitlements(LicenseEdition.Standard, null));
        try
        {
            var client = _fixture.Factory.CreateClient();
            var consolidated = await client.GetAsync(
                "/api/v1/company-groups/1/reports/consolidated-trial-balance?from=2026-01-01&to=2026-12-31");
            Assert.Equal(HttpStatusCode.Forbidden, consolidated.StatusCode);
            Assert.Contains("licence_tier", await consolidated.Content.ReadAsStringAsync());

            var projects = await client.GetAsync("/api/v1/reports/project-profitability?companyId=1");
            Assert.Equal(HttpStatusCode.Forbidden, projects.StatusCode);
        }
        finally
        {
            LicenseGate.Default = previous;
        }
    }

    private sealed class FixedProvider(LicenseEntitlements value) : ILicenseEntitlementsProvider
    {
        public LicenseEntitlements Current() => value;
    }
}
