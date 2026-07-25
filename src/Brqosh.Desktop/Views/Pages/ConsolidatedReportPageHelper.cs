using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

internal static class ConsolidatedReportPageHelper
{
    internal record GroupItem(long Id, string Display)
    {
        public override string ToString() => Display;
    }

    internal static async Task<IReadOnlyList<GroupItem>> LoadGroupsAsync(BrqoshDbContext db, long companyId)
    {
        var company = await db.Companies.AsNoTracking().SingleAsync(c => c.Id == companyId);
        var query = db.CompanyGroups.AsNoTracking().AsQueryable();
        if (company.CompanyGroupId is long gid)
            query = query.Where(g => g.Id == gid);

        return await query
            .OrderBy(g => g.Code)
            .Select(g => new GroupItem(
                g.Id,
                g.Code + " — " + g.NameAr + " (" + g.ReportingCurrencyCode + ")"))
            .ToListAsync();
    }
}
