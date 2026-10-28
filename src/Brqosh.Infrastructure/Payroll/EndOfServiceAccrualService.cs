using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

/// <summary>حصيلةُ تخصيصِ شهر — والفرقُ هو ما قُيّد، لا الالتزامُ كلُّه.</summary>
public record EndOfServiceAccrualResult(int Employees, decimal Movement, long? JournalEntryId);

/// <summary>
/// (CEEF-230 · ط2-٣) <b>تخصيصُ مكافأة نهاية الخدمة — شهراً بشهر.</b>
///
/// <para><b>ما كان:</b> حسابُ المخصّص <c>2202</c> في الشجرة <b>ولا كاتبَ له</b>. فالمنشأةُ
/// تحمل التزاماً يتراكم ولا يظهر في ميزانيّتها — <b>وأوّلُ استحقاقٍ يُفاجئها بمبلغٍ لم
/// تُخصّص له</b>، وقوائمُ سنواتٍ مضت كانت تُظهر ربحاً أكبرَ من الحقّ.</para>
///
/// <para><b>والتخصيصُ فرقٌ لا تراكم:</b> يُحسب الالتزامُ الكليُّ اليومَ، ويُقارَن بما في
/// الحساب، <b>ويُقيَّد الفرقُ وحده</b>. ولذلك سببٌ نظاميّ: الأجرُ المعتبَر هو <b>الأخير</b>،
/// فترقيةٌ في الشهر الأخير تُعيد تقدير الالتزام كلِّه بأثرٍ رجعيّ. <b>وتراكمٌ أعمى يُخطئ
/// عند أوّل ترقية</b>.</para>
///
/// <para><b>ويُقدَّر بافتراض إنهاءٍ من صاحب العمل</b> — أي بالمستحقّ الكامل. فالمنشأةُ لا
/// تعرف يومَ التخصيص كيف ستنتهي الخدمة، <b>والتخصيصُ بالأدنى يُخفي التزاماً قد يقع</b>،
/// وهو مذهبُ الحيطة.</para>
///
/// <para><b>ودفعةٌ يُطلقها المستخدم</b> على مثال <c>DepreciationService.RunMonthlyAsync</c> —
/// فلا مُجدوِلَ في هذا التطبيق.</para>
/// </summary>
public class EndOfServiceAccrualService(BrqoshDbContext db)
{
    /// <summary>حسابُ مصروف المكافأة — يُنشأ من القالب إن غاب.</summary>
    private const string ExpenseCode = SaudiChartOfAccounts.PostingRoleCodes.EndOfServiceExpense;

    public async Task<EndOfServiceAccrualResult> RunMonthlyAsync(
        long companyId, DateOnly asOf, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var period = FiscalPeriod.CodeFor(asOf);
            var sourceDocument = $"EndOfServiceAccrual:{period}";

            // ‏**ولا حارسَ «مرّةً في الشهر» هنا — وإزالتُه قرارٌ أخرجته طفرة.**
            //
            // ‏كتبتُ فحصاً يرفض تشغيلاً ثانياً للفترة نفسِها، **فنجَت طفرةُ نزعِه**: التخصيصُ
            // ‏**فرقٌ** لا تراكم، فتشغيلٌ ثانٍ بلا تغييرٍ يُنتج حركةً صفراً ولا يُقيّد شيئاً.
            // ‏فالمُطاوَعةُ قائمةٌ بالبناء لا بالحارس.
            //
            // ‏**وكان الحارسُ أسوأَ من زائد**: يمنع تصحيحاً مشروعاً داخل الشهر نفسِه —
            // ‏فمَن رفع راتباً ثمّ أعاد التشغيل لا يرى أثراً، **ويظنّ الالتزامَ محدَّثاً
            // ‏وهو قديم**. فحُذف، وحلّ محلّه قياسان: تشغيلٌ بلا تغييرٍ لا يُقيّد، وتغييرٌ
            // ‏داخل الشهر يُقيَّد.

            var employees = await db.Employees
                .Where(e => e.CompanyId == companyId)
                .ToListAsync(token);

            // ‏**ومَن انتهت خدمتُه يبقى في الحساب حتى تُصفَّى مستحقّاتُه**: إسقاطُه فورَ
            // ‏الإيقاف يعكس التزاماً ما زال قائماً ولم يُدفع.
            //
            // ‏(CEEF-232 · ر-١) **ومَن صُفّيت مستحقّاتُه يخرج** — وبغير هذا يُبطل التخصيصُ
            // ‏التصفيةَ في الشهر التالي: **يرى المخصّصَ ناقصاً فيُعيد بناءه**، فيُقيَّد
            // ‏مصروفٌ ثانٍ لمستحقٍّ دُفع، **ويعود الالتزامُ الذي أُبرئ للتوّ**.
            var settled = await EndOfServiceSettlementService
                .SettledEmployeeIdsAsync(db, companyId, asOf, token);

            var required = employees
                .Where(e => !settled.Contains(e.Id))
                .Where(e => e.IsEmployedOn(asOf) || e.TerminationDate is not null)
                .Sum(e => EndOfServiceCalculator.BenefitFor(
                    e, e.TerminationDate is DateOnly t && t < asOf ? t : asOf,
                    EndOfServiceReason.EmployerTermination));

            var provisioned = -await ProvisionBalanceAsync(companyId, asOf, token);
            var movement = Math.Round(required - provisioned, 2, MidpointRounding.AwayFromZero);

            if (movement == 0m)
                return new EndOfServiceAccrualResult(employees.Count, 0m, null);

            var expense = await SystemAccountProvisioning.EnsureAsync(db, companyId, await Brqosh.Infrastructure.Accounting.PostingRoleService.ResolveAsync(db, companyId, ExpenseCode, token), token);
            var provision = await SystemAccountProvisioning.EnsureAsync(db, companyId, await Brqosh.Infrastructure.Accounting.PostingRoleService.ResolveAsync(db, companyId, SaudiChartOfAccounts.PostingRoleCodes.EndOfServiceProvision, token), token);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, asOf, PostingDocumentKind.JournalEntry,
                accountIds: [expense.Id, provision.Id], ct: token);

            var entry = JournalEntry.CreateDraft(companyId, asOf,
                $"تخصيص مكافأة نهاية الخدمة — {period}", sourceDocument: sourceDocument);

            // ‏الزيادةُ مصروفٌ ومخصّص، والنقصُ عكسُهما — وردُّ مخصّصٍ زائدٍ إيرادُ تصحيحٍ
            // ‏لا ربحُ تشغيل، فيُخصم من المصروف نفسِه.
            if (movement > 0m)
            {
                entry.AddLine(expense, movement, 0m, "مصروف مكافأة نهاية الخدمة");
                entry.AddLine(provision, 0m, movement, "مخصص مكافأة نهاية الخدمة");
            }
            else
            {
                entry.AddLine(provision, -movement, 0m, "رد مخصص مكافأة نهاية الخدمة");
                entry.AddLine(expense, 0m, -movement, "تصحيح مصروف مكافأة نهاية الخدمة");
            }

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, asOf);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "EndOfServiceAccrual", period,
                reason: "تخصيص مكافأة نهاية الخدمة",
                details: $"المطلوب {required:N2} · المخصَّص {provisioned:N2} · الحركة {movement:N2}",
                ct: token);

            return new EndOfServiceAccrualResult(employees.Count, movement, entry.Id);
        }, ct);
    }

    /// <summary>رصيدُ حساب المخصّص حتى تاريخ — بإشارةِ (مدين − دائن)، فهو دائنُ الطبيعة.</summary>
    private async Task<decimal> ProvisionBalanceAsync(long companyId, DateOnly asOf, CancellationToken ct)
    {
        var provisionCode = await Brqosh.Infrastructure.Accounting.PostingRoleService.ResolveAsync(db, companyId, SaudiChartOfAccounts.PostingRoleCodes.EndOfServiceProvision, ct);
        var lines = await db.JournalEntries
            .Where(e => e.CompanyId == companyId && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                        && e.EntryDate <= asOf)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == provisionCode)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync(ct);

        return Math.Round(lines.Sum(l => l.Debit - l.Credit), 2, MidpointRounding.AwayFromZero);
    }
}
