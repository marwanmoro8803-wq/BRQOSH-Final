using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>يقرأ شهرَ بداية السنة المالية للشركة ويشتقّ منه سنةَ التاريخ المالية — مفتاحُ الأقفال والإقفال والترقيم.</summary>
public static class FiscalYearResolver
{
    public static async Task<int> StartMonthAsync(BrqoshDbContext db, long companyId, CancellationToken ct = default) =>
        FiscalYear.NormalizeStartMonth(await db.Companies.IgnoreQueryFilters()
            .Where(c => c.Id == companyId).Select(c => c.FiscalYearStartMonth).FirstOrDefaultAsync(ct));

    public static int StartMonth(BrqoshDbContext db, long companyId) =>
        FiscalYear.NormalizeStartMonth(db.Companies.IgnoreQueryFilters()
            .Where(c => c.Id == companyId).Select(c => c.FiscalYearStartMonth).FirstOrDefault());

    public static async Task<int> StartYearForAsync(BrqoshDbContext db, long companyId, DateOnly date, CancellationToken ct = default) =>
        FiscalYear.StartYearFor(date, await StartMonthAsync(db, companyId, ct));
}
