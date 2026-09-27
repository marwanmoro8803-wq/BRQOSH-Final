using Brqosh.Domain.Construction;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Construction;

public class ConstructionProjectService(BrqoshDbContext db)
{
    public async Task<ConstructionProject> CreateAsync(ConstructionProject project, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        var exists = await db.ConstructionProjects
            .AnyAsync(p => p.CompanyId == project.CompanyId && p.Code == project.Code, ct);
        if (exists)
            throw new InvalidOperationException($"كود المشروع '{project.Code}' مستخدم مسبقاً.");

        db.ConstructionProjects.Add(project);
        await db.SaveChangesAsync(ct);
        return project;
    }

    public async Task UpdateAsync(long companyId, long projectId, string nameAr, long? customerId,
        DateOnly? startDate, DateOnly? endDate, decimal contractValue, decimal retentionPercent,
        string location, ConstructionProjectStatus status, decimal estimatedTotalCost = 0m,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        var project = await db.ConstructionProjects
            .SingleAsync(p => p.CompanyId == companyId && p.Id == projectId, ct);
        project.Update(nameAr, customerId, startDate, endDate, contractValue, retentionPercent, location, status,
            estimatedTotalCost);
        await db.SaveChangesAsync(ct);

        // ‏(جاهزيّةُ الإطلاق · ح-٨٠) ‏**ومراجعةُ التقدير أو قيمة العقد لحاقٌ تراكميّ** — يُقيَّد يومَ المراجعة.
        await new ContractRevenueTrueUp(db).ApplyAsync(companyId, projectId, DateOnly.FromDateTime(DateTime.Today), ct);
    }

    public async Task ReplaceBoqAsync(long companyId, long projectId, IReadOnlyList<BoqItem> items,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        var project = await db.ConstructionProjects
            .SingleAsync(p => p.CompanyId == companyId && p.Id == projectId, ct);

        project.ClearBoq();
        foreach (var item in items)
        {
            project.AddBoqItem(item.ItemNumber, item.Description, item.Unit, item.Quantity, item.UnitPrice);
        }

        await db.SaveChangesAsync(ct);
    }
}
