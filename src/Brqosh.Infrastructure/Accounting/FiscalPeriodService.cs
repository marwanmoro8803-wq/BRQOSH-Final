using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>إغلاق وإعادة فتح الفترات المالية مع تسجيل التدقيق.</summary>
public class FiscalPeriodService(BrqoshDbContext db)
{
    public async Task CloseAsync(long companyId, long periodId, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        username = SecurityContext.ResolveUsername(username);
        var period = await db.FiscalPeriods.SingleAsync(
            p => p.CompanyId == companyId && p.Id == periodId, ct);
        period.Close();
        await db.SaveChangesAsync(ct);

        await new AuditService(db).LogAsync(companyId, username, "إغلاق فترة مالية",
            "FiscalPeriod", period.Code, $"من {period.StartDate:yyyy-MM-dd} إلى {period.EndDate:yyyy-MM-dd}");
    }

    public async Task ReopenAsync(long companyId, long periodId, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        username = SecurityContext.ResolveUsername(username);
        var period = await db.FiscalPeriods.SingleAsync(
            p => p.CompanyId == companyId && p.Id == periodId, ct);
        period.Reopen();
        await db.SaveChangesAsync(ct);

        await new AuditService(db).LogAsync(companyId, username, "إعادة فتح فترة مالية",
            "FiscalPeriod", period.Code, $"من {period.StartDate:yyyy-MM-dd} إلى {period.EndDate:yyyy-MM-dd}");
    }
}
