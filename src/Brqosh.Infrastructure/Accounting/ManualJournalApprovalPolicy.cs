using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// <b>حدُّ اعتماد القيد اليدويّ يغيّره المديرُ وحده — ويُسجَّل.</b>
///
/// <para>الحدُّ ضابطٌ رقابيّ: من رفعه أعفى قيوده من الاعتماد. فتغييرُه لا يكون بحقلٍ في صفحةٍ يحفظها كلُّ من فتحها، بل بهذه
/// الخدمة التي تشترط المديرَ وتكتب القيمتين القديمة والجديدة في سجلّ التدقيق.</para>
/// </summary>
public class ManualJournalApprovalPolicy(BrqoshDbContext db)
{
    public async Task SetThresholdAsync(long companyId, decimal threshold, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var previous = company.ManualJournalApprovalThreshold;
        company.SetManualJournalApprovalThreshold(threshold);
        if (company.ManualJournalApprovalThreshold == previous)
            return;

        await db.SaveChangesAsync(ct);
        await new AuditService(db).LogAsync(companyId, SecurityContext.ResolveUsername(), "تعديل", "Company",
            companyId.ToString(), $"حد اعتماد القيد اليدوي: {previous:N2} ← {company.ManualJournalApprovalThreshold:N2}",
            null, ct);
    }
}
