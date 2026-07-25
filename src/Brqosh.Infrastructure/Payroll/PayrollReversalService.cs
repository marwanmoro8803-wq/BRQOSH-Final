using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

/// <summary>عكس دورة رواتب مرحّلة — عكس القيد وتحديث الحالة.</summary>
public class PayrollReversalService(BrqoshDbContext db)
{
    public async Task ReverseAsync(long companyId, long runId, DateOnly reversalDate,
        string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanCancelOrReverse();
        username = SecurityContext.ResolveUsername(username);

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var run = await db.PayrollRuns.SingleAsync(
                r => r.CompanyId == companyId && r.Id == runId, token);

            if (run.Status != PayrollRunStatus.Posted)
                throw new AccountingException("لا يُعكس إلا دورة رواتب مرحّلة.");

            if (run.JournalEntryId is not long entryId)
                throw new AccountingException("الدورة بلا قيد محاسبي مرتبط.");

            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, reversalDate);

            await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, reversalDate, username, fromSourceDocument: true, token);

            run.MarkReversed();
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "عكس دورة رواتب",
                "PayrollRun", run.PeriodCode, "تم عكس قيد الاستحقاق");
        }, ct);
    }
}
