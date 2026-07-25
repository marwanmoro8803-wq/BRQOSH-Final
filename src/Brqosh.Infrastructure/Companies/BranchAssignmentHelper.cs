using Brqosh.Domain.Companies;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Companies;

public static class BranchAssignmentHelper
{
    /// <summary>
    /// يُعيّن الفرع من مستند أصل إن وُجد، وإلا فرع الجلسة، وإلا المقر.
    /// </summary>
    public static async Task EnsureAssignedAsync(
        IBranchScoped entity,
        BrqoshDbContext db,
        long? preferredFromDocument = null,
        CancellationToken ct = default)
    {
        if (entity.BranchId is not null) return;

        if (preferredFromDocument is long docBranch && docBranch > 0)
        {
            var ok = await db.Branches.AnyAsync(
                b => b.CompanyId == entity.CompanyId && b.Id == docBranch && b.IsActive, ct);
            if (ok)
            {
                entity.AssignBranch(docBranch);
                return;
            }
        }

        entity.AssignBranch(await new BranchAssignmentService(db)
            .ResolveBranchIdAsync(entity.CompanyId, BranchSessionContext.PreferredBranchId, ct));
    }
}
