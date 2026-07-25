using Brqosh.Application.Platform;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Platform;

public class BillingPlanCatalog(BrqoshDbContext db) : IBillingPlanCatalog
{
    public async Task<IReadOnlyList<BillingPlanDto>> ListActiveAsync(CancellationToken ct = default)
    {
        return await db.BillingPlans.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new BillingPlanDto(p.Code, p.NameAr, p.MaxCompanies))
            .ToListAsync(ct);
    }

    public async Task<TenantSubscriptionDto?> GetSubscriptionAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return null;

        return await db.TenantSubscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Join(db.BillingPlans.AsNoTracking(), s => s.BillingPlanId, p => p.Id,
                (s, p) => new TenantSubscriptionDto(s.TenantId, p.Code, p.NameAr, s.Status, s.StartedAtUtc))
            .FirstOrDefaultAsync(ct);
    }
}
