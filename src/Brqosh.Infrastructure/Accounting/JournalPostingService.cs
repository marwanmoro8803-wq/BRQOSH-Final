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

    /// <summary>
    /// (CEEF-229 · م2-٦) <b>اسمُها «تحقُّق» وفعلُها كتابة.</b>
    ///
    /// <para>حين لا توجد الفترة المالية تُنشئها الدالّة <b>وتحفظها</b> (السطور أدناه) — فهي
    /// ليست قراءةً كما يوحي اسمُها. وكانت الوحيدة بين عمليات خدمات الترحيل التي <b>لا ترفض
    /// «المُطّلِع»</b>، فكشفتها البوّابةُ السلوكية حين استدعت كلّ عملية بدورٍ لا يملك الترحيل.</para>
    ///
    /// <para><b>ولم تكن ثغرةً حيّة:</b> مستدعوها الخمسة والعشرون كلُّهم داخل خدمات ترحيلٍ
    /// وإلغاءٍ تفرض حارسَها قبلها، فلا يبلغها مُطّلِعٌ من أيّ مسارٍ في الواجهة. والحارسُ هنا
    /// عمقٌ دفاعيّ: الدالّة عامّةٌ وتكتب، وكلُّ مستدعٍ شرعيّ يملك حقَّ الترحيل أصلاً فلا يتغيّر
    /// عليه شيء.</para>
    /// </summary>
    public async Task EnsurePeriodOpenAsync(long companyId, DateOnly date)
    {
        SecurityContext.EnsureCanPost();

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
