using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Companies;

public class BranchAssignmentService(BrqoshDbContext db)
{
    public async Task<long> RequireHeadquartersIdAsync(long companyId, CancellationToken ct = default)
    {
        var hq = await db.Branches.SingleOrDefaultAsync(
                b => b.CompanyId == companyId && b.IsHeadquarters && b.IsActive, ct)
            ?? await db.Branches.Where(b => b.CompanyId == companyId && b.IsActive)
                .OrderBy(b => b.Id)
                .FirstOrDefaultAsync(ct)
            ?? throw new AccountingException("لا يوجد فرع نشط للمنشأة.");
        return hq.Id;
    }

    /// <summary>
    /// يفضّل الفرع الصريح إن كان نشطاً لنفس الشركة، وإلا المقر الرئيسي.
    /// </summary>
    public async Task<long> ResolveBranchIdAsync(
        long companyId, long? preferredBranchId = null, CancellationToken ct = default)
    {
        if (preferredBranchId is long id && id > 0)
        {
            var ok = await db.Branches.AnyAsync(
                b => b.CompanyId == companyId && b.Id == id && b.IsActive, ct);
            if (ok) return id;
        }

        return await RequireHeadquartersIdAsync(companyId, ct);
    }
}
