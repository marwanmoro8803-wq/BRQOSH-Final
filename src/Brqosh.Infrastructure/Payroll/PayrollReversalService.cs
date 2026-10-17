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

            // (جاهزيّةُ الإطلاق · ح-٨٤) ‏**ولا تُعكس دورةٌ خرج مالُها** — فعكسُها يُعيد `2108` مديناً بما صُرف والمالُ خارجٌ،
            // ‏ويبقى الالتزامُ سالباً لا يُفسَّر. والصرفُ نفسُه بلا عكسٍ بعد (`treasury-payroll-assets#10`).
            if (await new PayrollPaymentService(db).AnyPaidAsync(companyId, runId, token))
                throw new AccountingException("صُرفت رواتبُ الدورة أو سُدِّدت تأميناتُها — ولا تُعكس دورةٌ خرج مالُها.");

            await new JournalPostingService(db).EnsurePeriodOpenAsync(companyId, reversalDate);

            await new JournalReversalService(db).ReverseAsync(
                companyId, entryId, reversalDate, username, fromSourceDocument: true, token);

            // ‏(ح-١٤٦) ‏**وتكلفةُ المشاريع التي حملها قيدُ الدورة تسقط معه** — وإنجازُ عقودها يُسوّى.
            var source = PayrollPostingService.PayrollProjectSource(run.PeriodCode);
            var projectCosts = await db.ProjectCostEntries
                .Where(c => c.CompanyId == companyId && c.SourceDocument == source
                            && c.JournalEntryId == entryId && !c.IsReversed)
                .ToListAsync(token);
            foreach (var cost in projectCosts)
                cost.MarkReversed();

            run.MarkReversed();
            await db.SaveChangesAsync(token);

            foreach (var projectId in projectCosts.Select(c => c.ProjectId).Distinct())
                await new Construction.ContractRevenueTrueUp(db).ApplyAsync(companyId, projectId, reversalDate, token);

            // (CEEF-231 · ط2-٧) ‏**والعكسُ يُعيد الدَّين.**
            //
            // ‏الاستقطاعُ الذي أقفل السُّلفةَ ارتدّ بارتداد الدورة — **وسُلفةٌ تبقى
            // ‏«مسدَّدة» بعد ارتداد سدادها دَينٌ سقط بلا قبض**. والاتّجاهان في كاتبٍ
            // ‏واحد، فلا يُقفَل بلا أن يُعاد.
            await new EmployeeAdvanceService(db).RefreshStatusesAsync(companyId, token);

            await new AuditService(db).LogAsync(companyId, username, "عكس دورة رواتب",
                "PayrollRun", run.PeriodCode, "تم عكس قيد الاستحقاق");
        }, ct);
    }
}
