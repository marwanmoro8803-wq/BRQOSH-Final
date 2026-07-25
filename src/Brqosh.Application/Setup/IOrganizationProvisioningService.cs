using Brqosh.Domain.Modules;

namespace Brqosh.Application.Setup;

public record OrganizationProvisionRequest(
    string CompanyName,
    string VatNumber,
    BusinessProfile Profile,
    string CountryTemplateId = "saudi-arabia",
    string? AdminUsername = null,
    string? AdminPassword = null,
    string? AdminDisplayName = null);

public record OrganizationProvisionResult(long CompanyId, string CompanyName, long? AdminUserId);

/// <summary>إنشاء منشأة إضافية — G-69.</summary>
public interface IOrganizationProvisioningService
{
    Task<OrganizationProvisionResult> ProvisionAsync(OrganizationProvisionRequest request, CancellationToken ct = default);
}
