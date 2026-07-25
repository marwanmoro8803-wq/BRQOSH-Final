using Brqosh.Domain.Accounting;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Workflow;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// بوابة الترحيل الموحدة: كل قيد يمر من هنا — يتحقق من الفترة المالية قبل الترحيل،
/// وينشئ الفترة مفتوحةً تلقائياً إن لم توجد (أول استخدام للشهر).
/// </summary>
public class JournalPostingService(BrqoshDbContext db)
{
    private readonly PostingOrchestrator _orchestrator = new(db);

    public Task<JournalEntry> PostAsync(JournalEntry entry) =>
        UnitOfWork.ExecuteInTransactionAsync(db, async _ =>
        {
            SecurityContext.EnsureCanPostAccounting();
            if (entry.BranchId is null)
                entry.AssignBranch(await new BranchAssignmentService(db)
                .ResolveBranchIdAsync(entry.CompanyId, BranchSessionContext.PreferredBranchId));
            if (entry.IsManual && entry.WorkflowInstanceId is not null)
                await new ManualJournalEntryWorkflowService(db, new WorkflowEngine(db))
                    .EnsureWorkflowApprovedAsync(entry);

            await _orchestrator.ValidateJournalEntryAsync(entry);

            var number = await new JournalEntryNumberGenerator(db).NextAsync(entry.CompanyId, entry.EntryDate);
            entry.Post(number);

            if (entry.Id == 0)
                db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();

            await AuditPostingHelper.LogPostAsync(
                db, entry.CompanyId, "JournalEntry", entry.EntryNumber ?? entry.Id.ToString(),
                reason: "ترحيل قيد يومية", details: entry.Description ?? "");

            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                entry.CompanyId, entry.Id, entry.EntryNumber!, entry.EntryDate));

            return entry;
        });

    public async Task EnsurePeriodOpenAsync(long companyId, DateOnly date)
    {
        var code = FiscalPeriod.CodeFor(date);
        var period = await db.FiscalPeriods
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Code == code);

        if (period is null)
        {
            period = FiscalPeriod.Create(companyId, date.Year, date.Month);
            db.FiscalPeriods.Add(period);
            await db.SaveChangesAsync();
            return;
        }

        if (period.Status == FiscalPeriodStatus.Closed)
            throw new AccountingException(
                $"الفترة المالية {code} مقفلة — لا يمكن الترحيل فيها. أعد فتح الفترة أولاً إن كنت مخوّلاً.");
    }
}
