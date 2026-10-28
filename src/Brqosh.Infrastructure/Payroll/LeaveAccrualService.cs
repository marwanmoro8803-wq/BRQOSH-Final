using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

/// <summary>حصيلةُ تخصيصِ شهر — <b>والفرقُ هو ما قُيّد، لا الالتزامُ كلُّه</b>.</summary>
public record LeaveAccrualResult(int Employees, decimal Movement, long? JournalEntryId);

/// <summary>
/// <b>تخصيصُ الإجازات — التزامٌ يتراكم كلَّ يومٍ ولم يكن في الدفاتر.</b>
///
/// <para><b>ما كان:</b> لا كيانَ إجازاتٍ أصلاً، <b>فحسابُ <c>2203</c> لم يكن موجوداً</b>.
/// والمنشأةُ تحمل رصيدَ إجازاتِ موظّفيها — <b>وهو دَينٌ نقديٌّ بنصّ المادة 111</b> يُدفع
/// عند انتهاء الخدمة عمّا لم يُستعمل — <b>ولا يظهر في ميزانيّتها بريال</b>. فتُقفل السنةُ
/// على ربحٍ أكبرَ من الحقّ، <b>وتُوزَّع أرباحٌ ويُدفع زكاةٌ على التزامٍ قائم</b>.</para>
///
/// <para><b>والأثرُ بالأرقام:</b> منشأةٌ بمئة موظّفٍ متوسّطُ أجرهم عشرةُ آلافٍ ورصيدُ كلٍّ
/// منهم خمسةَ عشرَ يوماً ⟵ <b>خمسُمئة ألفِ ريالٍ التزامٌ غيرُ مُعلَن</b>. <b>ولا يُكتشف
/// إلّا حين يغادر أوّلُهم فيُطالِب.</b></para>
///
/// <para><b>والتخصيصُ فرقٌ لا تراكم</b> — على سُنّة <c>EndOfServiceAccrualService</c>:
/// يُحسب الالتزامُ الكليُّ اليومَ ويُقارَن بما في الحساب، <b>ويُقيَّد الفرقُ وحدَه</b>.
/// ولذلك سببٌ: <b>الرصيدُ يُقيَّم بالأجر الأخير</b>، فترقيةٌ تُعيد تقدير الالتزام كلِّه —
/// <b>وتراكمٌ أعمى يُخطئ عند أوّل ترقية</b>، وينحرف عند أوّل إلغاءِ إجازة.</para>
///
/// <para><b>وهويّةُ هـ-٢٧:</b> <c>رصيدُ 2203 = مجموعُ (رصيدِ أيّامِ كلّ موظّفٍ × أجرِه اليوميّ)</c>.</para>
/// </summary>
public class LeaveAccrualService(BrqoshDbContext db)
{
    public async Task<LeaveAccrualResult> RunMonthlyAsync(
        long companyId, DateOnly asOf, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var period = FiscalPeriod.CodeFor(asOf);

            var employees = await db.Employees
                .Where(e => e.CompanyId == companyId)
                .ToListAsync(token);

            var balances = await new LeaveBalanceQuery(db).ForAllAsync(companyId, asOf, token);
            var byEmployee = balances.ToDictionary(b => b.EmployeeId);

            // ‏**ومَن صُفّيت مستحقّاتُه يخرج** — فبدلُ إجازته دُفع مع التصفية،
            // ‏**وإبقاؤه يُعيد بناءَ التزامٍ أُبرئ للتوّ** في الشهر التالي.
            var settled = await EndOfServiceSettlementService
                .SettledEmployeeIdsAsync(db, companyId, asOf, token);

            var required = 0m;
            foreach (var employee in employees)
            {
                if (settled.Contains(employee.Id)) continue;
                if (!employee.IsEmployedOn(asOf) && employee.TerminationDate is null) continue;
                if (!byEmployee.TryGetValue(employee.Id, out var balance)) continue;

                // ‏**والرصيدُ السالبُ لا يُخصَّص** — إجازةٌ مُنحت زائدةً دَينٌ **على** الموظّف
                // ‏لا عليه، **وجمعُها بالسالب يُنقص التزامَ المنشأة تجاه الباقين**.
                if (balance.BalanceDays <= 0m) continue;

                required += LeaveEntitlementCalculator.LeaveCompensation(
                    employee.GrossPay, balance.BalanceDays);
            }

            required = Math.Round(required, 2, MidpointRounding.AwayFromZero);

            var provisioned = -await ProvisionBalanceAsync(companyId, asOf, token);
            var movement = Math.Round(required - provisioned, 2, MidpointRounding.AwayFromZero);

            if (movement == 0m)
                return new LeaveAccrualResult(employees.Count, 0m, null);

            var expense = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.LeaveExpense, token);
            var provision = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.LeaveProvision, token);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, asOf, PostingDocumentKind.JournalEntry,
                accountIds: [expense.Id, provision.Id], ct: token);

            var entry = JournalEntry.CreateDraft(companyId, asOf,
                $"تخصيص الإجازات — {period}", sourceDocument: $"LeaveAccrual:{period}");

            // ‏والزيادةُ مصروفٌ ومخصّص، والنقصُ عكسُهما — **وردُّ مخصّصٍ زائدٍ تصحيحُ تقديرٍ
            // ‏لا ربحُ تشغيل**، فيُخصم من المصروف نفسِه كما في مخصّص نهاية الخدمة.
            if (movement > 0m)
            {
                entry.AddLine(expense, movement, 0m, "مصروف الإجازات");
                entry.AddLine(provision, 0m, movement, "مخصص الإجازات");
            }
            else
            {
                entry.AddLine(provision, -movement, 0m, "رد مخصص الإجازات");
                entry.AddLine(expense, 0m, -movement, "تصحيح مصروف الإجازات");
            }

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, asOf);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "LeaveAccrual", period,
                reason: "تخصيص الإجازات",
                details: $"المطلوب {required:N2} · المخصَّص {provisioned:N2} · الحركة {movement:N2}",
                ct: token);

            return new LeaveAccrualResult(employees.Count, movement, entry.Id);
        }, ct);
    }

    /// <summary>رصيدُ حساب المخصّص حتى تاريخ — بإشارةِ (مدين − دائن)، فهو دائنُ الطبيعة.</summary>
    internal async Task<decimal> ProvisionBalanceAsync(
        long companyId, DateOnly asOf, CancellationToken ct = default)
    {
        var lines = await db.JournalEntries
            .Where(e => e.CompanyId == companyId && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                        && e.EntryDate <= asOf)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.LeaveProvision)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync(ct);

        return Math.Round(lines.Sum(l => l.Debit - l.Credit), 2, MidpointRounding.AwayFromZero);
    }
}
