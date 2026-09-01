using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Platform;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

public class PayrollPostingService(BrqoshDbContext db)
{
    public async Task<PayrollRun> PostAsync(PayrollRun run, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            await BranchAssignmentHelper.EnsureAssignedAsync(run, db, ct: token);

            var salaries = await AccountAsync(run.CompanyId, SaudiChartOfAccounts.SystemCodes.SalariesExpense, token);
            var gosiExp = await AccountAsync(run.CompanyId, "5203", token);
            var salariesPayable = await AccountAsync(run.CompanyId, "2108", token);
            var gosiPayable = await AccountAsync(run.CompanyId, "2109", token);

            await FinancialPostingSupport.ValidateAsync(
                db, run.CompanyId, run.PayDate, PostingDocumentKind.Payroll,
                accountIds: [salaries.Id, gosiExp.Id, salariesPayable.Id, gosiPayable.Id], ct: token);

            // (CEEF-230 · ط2-٤) ‏حسابُ البدلات — يُنشأ من القالب إن غاب، فقواعدُ العملاء
            // ‏تحمله أصلاً (هو في الشجرة منذ البداية) لكنّه لم يكن يُستعمل.
            var allowances = await SystemAccountProvisioning.EnsureAsync(db, run.CompanyId, "5202", token);

            var entry = run.Post(salaries, gosiExp, salariesPayable, gosiPayable, allowances);
            var entryNumber = await new JournalEntryNumberGenerator(db).NextAsync(run.CompanyId, run.PayDate);
            entry.Post(entryNumber);

            db.JournalEntries.Add(entry);

            // (CEEF-230 · ط2-٠) ‏**لا تُضاف دورةٌ محفوظةٌ سلفاً.**
            //
            // ‏كانت تُضاف بلا شرط، **وشاشةُ الرواتب تحفظ المسودّةَ أوّلاً** ثمّ تُعيد
            // ‏تحميلَها وتُمرّرها هنا. فتُدرَج مرّةً ثانيةً بمعرّفٍ قائم ⟵ `23505`.
            // ‏**فشاشةُ الرواتب لم تكن تُرحّل شيئاً**، ووحدةُ الرواتب مُعلَنةٌ على
            // ‏القطاعات الثمانية كلِّها.
            //
            // ‏ولم يكشفه اختبار: كلُّ اختبارات الرواتب تُرحّل دورةً **لم تُحفَظ** — مسارٌ
            // ‏لا يسلكه مستخدم. **واختبارٌ يسلك غيرَ طريق المستخدم يحرس غيرَ ما يُستعمل.**
            //
            // ‏والشرطُ هو اصطلاحُ `JournalPostingService` نفسِه (`if (entry.Id == 0)`) —
            // ‏فيقبل المسارين معاً بدل أن يُصلَح أحدهما بكسر الآخر.
            if (run.Id == 0)
                db.PayrollRuns.Add(run);
            await db.SaveChangesAsync(token);

            run.LinkJournal(entry.Id);
            await db.SaveChangesAsync(token);

            await FinancialPostingSupport.AuditPostAsync(
                db, run.CompanyId, "PayrollRun", run.PeriodCode,
                reason: "ترحيل رواتب", details: $"صافي: {run.TotalNet:N2}", ct: token);

            await DomainEventHub.PublishAsync(new DocumentPostedEvent(
                run.CompanyId, "PayrollRun", run.Id, run.PeriodCode), token);
            await DomainEventHub.PublishAsync(new JournalEntryPostedEvent(
                run.CompanyId, entry.Id, entry.EntryNumber!, run.PayDate), token);

            return run;
        }, ct);
    }

    private async Task<Account> AccountAsync(long companyId, string code, CancellationToken ct) =>
        await db.Accounts.SingleAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}
