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

            // (CEEF-230 · ط2-٥) ‏حسابُ السُّلَف — ولا يُنشأ إلا حين يوجد استقطاع، فقاعدةٌ
            // ‏لا تُسلّف أحداً لا يظهر فيها حسابٌ بلا حركة.
            var advances = run.TotalAdvanceDeduction > 0m
                ? await SystemAccountProvisioning.EnsureAsync(
                    db, run.CompanyId, SaudiChartOfAccounts.SystemCodes.EmployeeAdvances, token)
                : null;

            // (CEEF-232 · م-١) ‏**ومراكزُ تكلفةِ الموظّفين تُقرأ فيُفكَّك بها مصروفُ الأجر.**
            //
            // ‏وكان `Employee.CostCenterId` مُخزَّناً في المخطَّط **و`AssignCostCenter` بلا
            // ‏مستدعٍ واحد**، وقيدُ الرواتب سطرٌ مُجمَّعٌ بلا بُعد. **فالميزانُ سليمٌ وكلُّ
            // ‏قائمةِ مركزِ تكلفةٍ تُسقط أكبرَ بنودها** — والأجرُ عادةً أكبرُ المصروفات.
            //
            // ‏**ولا يُقرأ إلا لموظّفي هذه الدورة** — فقاعدةٌ فيها ألفُ موظّفٍ لا تُحمَّل
            // ‏كلُّها لدورةٍ فيها عشرة.
            var employeeIds = run.Lines
                .Where(l => l.EmployeeId != null)
                .Select(l => l.EmployeeId!.Value)
                .Distinct()
                .ToList();

            var costCenters = employeeIds.Count == 0
                ? null
                : await db.Employees
                    .Where(e => e.CompanyId == run.CompanyId && employeeIds.Contains(e.Id))
                    .Select(e => new { e.Id, e.CostCenterId })
                    .ToDictionaryAsync(e => e.Id, e => e.CostCenterId, token);

            var entry = run.Post(salaries, gosiExp, salariesPayable, gosiPayable, allowances, advances,
                costCenters);
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

            // (CEEF-231 · ط2-٧) ‏**والسُّلفةُ المسدَّدةُ تُقفَل هنا لا في شاشتها.**
            //
            // ‏كان تحديثُ الحالات زرّاً في شاشة السُّلَف — **والزرُّ يُغلق الفجوةَ لمَن
            // ‏يتذكّره**. فسُلفةٌ سُدِّد آخرُ قسطها تبقى «قائمةً» في كلّ تقريرٍ يقرأ الحالة،
            // ‏**وتُعرض ديناً لا وجودَ له** حتى يفتح أحدٌ تلك الشاشة. والترحيلُ هو اللحظةُ
            // ‏التي يتغيّر فيها الرصيد، فهو موضعُ التحديث.
            await new EmployeeAdvanceService(db).RefreshStatusesAsync(run.CompanyId, token);

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
