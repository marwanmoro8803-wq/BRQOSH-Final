using Brqosh.Domain.Modules;
using Brqosh.Domain.Platform;

namespace Brqosh.Application.Platform;

public record BillingPlanDto(string Code, string NameAr, int MaxCompanies);

public record TenantSubscriptionDto(
    Guid TenantId,
    string PlanCode,
    string PlanNameAr,
    SubscriptionStatus Status,
    DateTime StartedAtUtc);

public record TenantProvisionRequest(
    string CompanyNameAr,
    string? VatNumber,
    string PlanCode,
    BusinessProfile Profile = BusinessProfile.General);

public record TenantProvisionResult(Guid TenantId, long CompanyId, string PlanCode);

public interface IBillingPlanCatalog
{
    Task<IReadOnlyList<BillingPlanDto>> ListActiveAsync(CancellationToken ct = default);
    Task<TenantSubscriptionDto?> GetSubscriptionAsync(Guid tenantId, CancellationToken ct = default);
}

public interface ITenantProvisioningService
{
    Task<TenantProvisionResult> ProvisionAsync(TenantProvisionRequest request, CancellationToken ct = default);
}
