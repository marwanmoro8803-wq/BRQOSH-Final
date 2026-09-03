using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

/// <summary>
/// حصيلةُ تصفيةٍ واحدة — <b>والمستحقُّ غيرُ المخصَّص</b>، والفرقُ يعود إلى المصروف.
/// </summary>
public record EndOfServiceSettlementResult(
    long EmployeeId, decimal Provisioned, decimal Benefit, decimal Adjustment, long JournalEntryId);

/// <summary>
/// (CEEF-232 · ر-١) <b>تصفيةُ نهاية الخدمة — الطرفُ الذي يُبرئ المخصّص.</b>
///
/// <para><b>ما كان:</b> <c>EndOfServiceAccrualService</c> يُصدِّر <b>دالّةً واحدة</b>:
/// <c>RunMonthlyAsync</c>. فالمخصّصُ <c>2202</c> <b>يُبنى شهراً بشهرٍ ولا يُبرَأ أبداً</b> —
/// ولا مسارَ دفعٍ في المستودع كلِّه. واختبارٌ قائمٌ يُوثّق نصفَ الحقيقة
/// (<c>ATerminatedEmployee_StaysProvisionedUntilSettled</c>) <b>ولا شيءَ يُصفّي</b>.</para>
///
/// <para><b>والأثرُ مركَّب:</b> الالتزامُ في الميزانيّة <b>ينمو بلا حدّ</b> ويحمل مستحقّاتِ
/// موظّفين غادروا منذ سنوات؛ <b>ومستحقُّ الموظّف يُدفع خارجَ الدفاتر</b> أو لا يُدفع.
/// وهو حقٌّ نظاميٌّ لا يسقط (نظام العمل، المادّتان 84 و85).</para>
///
/// <para><b>والسببُ يُسأل عنه هنا ولا يُسأل عند التخصيص</b> — وهذا جوهرُ البند: التخصيصُ
/// يجهل كيف ستنتهي الخدمة <b>فيحتاط بالمستحقّ الكامل</b>، والتصفيةُ تعرف. <b>فمَن استقال
/// بعد ثلاث سنواتٍ يستحقّ الثلث</b> (المادة 85)، <b>والفرقُ ليس ربحاً</b>: مخصّصٌ زائدٌ
/// يُردّ إلى المصروف نفسِه — على اصطلاح خدمة التخصيص حرفاً بحرف.</para>
///
/// <para><b>ولا تُدفع نقداً هنا</b>: تُقيَّد على «رواتب مستحقة» (<c>2108</c>) — <b>وهو
/// المسارُ الذي تسلكه الرواتبُ نفسُها</b> (<c>PayrollPostingService</c>)، فيخرج المالُ
/// بسند صرفٍ له دورتُه ومُوافقتُه. <b>وصرفٌ مباشرٌ من الصندوق هنا يُنشئ باباً ثانياً
/// للنقد لا يمرّ بتلك الدورة.</b></para>
///
/// <para><b>وهويّةُ ر-١:</b> بعد التصفية: <c>حركةُ 2202 = −المخصَّص للموظّف</c>،
/// <c>و2108 يزيد بالمستحقّ</c>، <b>وتشغيلُ التخصيص التالي لا يُعيد تخصيصَه</b>.</para>
/// </summary>
public class EndOfServiceSettlementService(BrqoshDbContext db)
{
    /// <summary>بادئةُ مصدر قيد التصفية — <b>وهي علامةُ «صُفّي» التي يقرؤها التخصيص</b>.</summary>
    public const string SourceDocumentPrefix = "EndOfServiceSettlement:";

    /// <summary>حسابُ مصروف المكافأة — نفسُه الذي يُحمَّل عند التخصيص.</summary>
    private const string ExpenseCode = "5219";

    /// <summary>حسابُ المخصّص.</summary>
    private const string ProvisionCode = "2202";

    /// <summary>رواتبُ مستحقّة — <b>مخرجُ المال الوحيد في الرواتب</b>.</summary>
    private const string PayableCode = "2108";

    public async Task<EndOfServiceSettlementResult> SettleAsync(
        long companyId, long employeeId, EndOfServiceReason reason,
        DateOnly settlementDate, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var employee = await db.Employees
                .SingleAsync(e => e.CompanyId == companyId && e.Id == employeeId, token);

            // ‏**ولا تُصفّى خدمةٌ قائمة**: التصفيةُ إبراءُ ذمّةٍ عن خدمةٍ انتهت،
            // ‏**وإبراءُ ذمّةِ موظّفٍ على رأس عمله يُسقط التزاماً ما زال ينمو**.
            if (employee.TerminationDate is not DateOnly termination)
                throw new AccountingException("لا تُصفّى مستحقّاتُ موظّفٍ لم تنتهِ خدمتُه.");

            if (settlementDate < termination)
                throw new AccountingException("تاريخ التصفية لا يسبق تاريخ انتهاء الخدمة.");

            // ‏**ولا تُصفّى مرّتين.** والعلامةُ قيدُ التصفية نفسُه لا حقلٌ على الموظّف —
            // ‏**فالدفترُ هو المصدر**، ولا يحتاج البندُ عموداً جديداً ولا هجرة.
            if (await IsSettledAsync(db, companyId, employeeId, token))
                throw new AccountingException("مستحقّاتُ هذا الموظّف مُصفّاةٌ مسبقاً.");

            // ‏**والمخصَّصُ يُقاس بما احتاطت له الخدمةُ الشهرية** — أي بالمستحقّ الكامل
            // ‏عند تاريخ الانتهاء. **ومقياسٌ آخر يترك في الحساب بقيّةً لا صاحبَ لها.**
            var provisioned = EndOfServiceCalculator.BenefitFor(
                employee, termination, EndOfServiceReason.EmployerTermination);

            // ‏**والمستحقُّ فعلاً يُقاس بالسبب** — وهو ما يُسأل عنه هنا وحدَه.
            var benefit = EndOfServiceCalculator.BenefitFor(employee, termination, reason);

            var adjustment = Math.Round(provisioned - benefit, 2, MidpointRounding.AwayFromZero);

            var expense = await SystemAccountProvisioning.EnsureAsync(db, companyId, ExpenseCode, token);
            var provision = await SystemAccountProvisioning.EnsureAsync(db, companyId, ProvisionCode, token);
            var payable = await SystemAccountProvisioning.EnsureAsync(db, companyId, PayableCode, token);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, settlementDate, PostingDocumentKind.JournalEntry,
                accountIds: [expense.Id, provision.Id, payable.Id], ct: token);

            var entry = JournalEntry.CreateDraft(companyId, settlementDate,
                $"تصفية مكافأة نهاية الخدمة — {employee.NameAr}",
                sourceDocument: $"{SourceDocumentPrefix}{employeeId}");

            if (provisioned > 0m)
                entry.AddLine(provision, provisioned, 0m, "إبراء مخصص مكافأة نهاية الخدمة");
            if (benefit > 0m)
                entry.AddLine(payable, 0m, benefit, "مستحق مكافأة نهاية الخدمة");

            // ‏**والفائضُ يُردّ إلى المصروف لا إلى الإيراد** — فهو تصحيحُ تقديرٍ لا ربحُ
            // ‏تشغيل. **وإدخالُه في الإيرادات يُضخّم أرباحَ سنةٍ باستقالةِ موظّف.**
            if (adjustment > 0m)
                entry.AddLine(expense, 0m, adjustment, "رد مخصص زائد — تصفية نهاية خدمة");
            else if (adjustment < 0m)
                entry.AddLine(expense, -adjustment, 0m, "عجز مخصص — تصفية نهاية خدمة");

            if (entry.Lines.Count == 0)
                throw new AccountingException("لا مستحقّ ولا مخصّص لهذا الموظّف — لا قيدَ للتصفية.");

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, settlementDate);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "EndOfServiceSettlement", employee.Code,
                reason: "تصفية مكافأة نهاية الخدمة",
                details: $"المخصَّص {provisioned:N2} · المستحقّ {benefit:N2} · الفرق {adjustment:N2}",
                ct: token);

            return new EndOfServiceSettlementResult(
                employeeId, provisioned, benefit, adjustment, entry.Id);
        }, ct);
    }

    /// <summary>هل صُفّيت مستحقّاتُ الموظّف — <b>يُقرأ من الدفتر لا من حقل</b>.</summary>
    internal static Task<bool> IsSettledAsync(
        BrqoshDbContext db, long companyId, long employeeId, CancellationToken ct = default) =>
        db.JournalEntries.AnyAsync(
            e => e.CompanyId == companyId
                 && e.Status == JournalEntryStatus.Posted
                 && e.SourceDocument == SourceDocumentPrefix + employeeId, ct);

    /// <summary>مُعرِّفاتُ مَن صُفّيت مستحقّاتُهم — لاستبعادهم من التخصيص الشهريّ.</summary>
    internal static async Task<HashSet<long>> SettledEmployeeIdsAsync(
        BrqoshDbContext db, long companyId, DateOnly asOf, CancellationToken ct = default)
    {
        var sources = await db.JournalEntries
            .Where(e => e.CompanyId == companyId
                        && e.Status == JournalEntryStatus.Posted
                        && e.EntryDate <= asOf
                        && e.SourceDocument != null
                        && e.SourceDocument.StartsWith(SourceDocumentPrefix))
            .Select(e => e.SourceDocument!)
            .ToListAsync(ct);

        return [.. sources
            .Select(s => long.TryParse(s[SourceDocumentPrefix.Length..], out var id) ? id : 0L)
            .Where(id => id > 0)];
    }
}
