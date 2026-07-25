using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Companies;

/// <summary>تحقق عزل المستأجر — يُفعَّل فقط عند ضبط TenantContext من middleware.</summary>
public static class TenantGuard
{
    public static async Task<bool> IsCompanyAllowedAsync(
        BrqoshDbContext db, long companyId, CancellationToken ct)
    {
        if (TenantContext.EnforceTenantIsolation && TenantContext.CurrentTenantId is null)
            return false;

        if (TenantContext.CurrentTenantId is null)
            return true;

        return await db.Companies.AsNoTracking()
            .AnyAsync(c => c.Id == companyId && c.TenantId == TenantContext.CurrentTenantId.Value, ct);
    }

    public static async Task<bool> IsGroupAllowedAsync(
        BrqoshDbContext db, long groupId, CancellationToken ct)
    {
        if (TenantContext.EnforceTenantIsolation && TenantContext.CurrentTenantId is null)
            return false;

        if (TenantContext.CurrentTenantId is null)
            return true;

        var tenantId = TenantContext.CurrentTenantId.Value;
        var members = await db.Companies.AsNoTracking()
            .Where(c => c.CompanyGroupId == groupId)
            .Select(c => c.TenantId)
            .ToListAsync(ct);

        if (members.Count == 0)
            return false;

        return members.All(t => t == tenantId);
    }
}
