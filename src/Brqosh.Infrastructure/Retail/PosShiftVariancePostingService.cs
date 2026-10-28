using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Retail;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Retail;

/// <summary>
/// حصيلةُ إغلاق وردية — <b>و<c>JournalEntryId</c> فارغٌ حين لا فرق</b>، ووردياتٌ كثيرةٌ
/// تُغلق على الصفر بحقّ.
/// </summary>
public record PosShiftVarianceResult(decimal Expected, decimal Counted, decimal Difference, long? JournalEntryId);

/// <summary>
/// <b>فرقُ نقدِ الوردية — يُحسب ويُعرض ولا يُقيَّد.</b>
///
/// <para><b>ما كان:</b> <c>PosShift</c> يحسب <c>ExpectedCash</c> و<c>CashDifference</c>،
/// <b>والشاشةُ تعرضهما وتُجمّع «ورديات بعجز» في مؤشّر</b> — <c>PosShiftsPage.OnCloseShift</c>
/// يُغلق الوردية ويكتب <b>سجلَّ تدقيقٍ فقط</b>. فلا قيدَ ولا حساب.</para>
///
/// <para><b>والأثرُ دائمٌ لا عابر:</b> المبيعاتُ النقدية تُقيَّد على <c>1101</c> بالكامل،
/// <b>فرصيدُ الصندوق في الأستاذ هو المتوقَّع لا المعدود</b>. وكلُّ عجزٍ أو زيادةٍ تُترك
/// <b>تُباعد بين الدفتر والدرج إلى الأبد</b> — ولا يُجدي جردٌ بعدها، إذ لا يُعرف أيُّ
/// وردياتِ السنة أحدثت الفارق. <b>وهو أيضاً بابُ الاختلاس الذي لا يترك أثراً محاسبياً</b>:
/// كاشيرٌ يأخذ مئةً كلَّ يومٍ لا يظهر في أيّ قائمة.</para>
///
/// <para><b>والعجزُ مصروفٌ والزيادةُ إيراد</b> — ولا يُقاصّان: وردياتُ شهرٍ فيها عجزُ
/// ألفٍ وزيادةُ ألفٍ ليست «صفراً»، <b>بل ألفان من الخلل</b>. والمقاصّةُ تُخفي الاثنين.</para>
///
/// <para><b>وهويّةُ ن-١:</b> بعد إغلاق الوردية: <c>رصيدُ 1101 = النقدُ المعدود</c> —
/// لا المتوقَّع.</para>
/// </summary>
public class PosShiftVariancePostingService(BrqoshDbContext db)
{
    /// <summary>مصروفاتٌ متنوّعة — وعاءُ العجز.</summary>
    private const string ShortageExpenseCode = SaudiChartOfAccounts.PostingRoleCodes.MiscellaneousExpenses;

    public async Task<PosShiftVarianceResult> PostForShiftAsync(
        long companyId, long shiftId, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var shift = await db.PosShifts
                .SingleAsync(s => s.CompanyId == companyId && s.Id == shiftId, token);

            // ‏**ولا يُقيَّد فرقُ ورديةٍ مفتوحة**: العدُّ لم يقع بعد، و«الفرق» عندئذٍ
            // ‏هو النقدُ الافتتاحيُّ سالباً — **رقمٌ لا معنى له يُقيَّد عجزاً**.
            if (shift.Status != PosShiftStatus.Closed)
                throw new AccountingException("لا يُقيَّد فرقُ وردية لم تُغلق.");

            var difference = shift.CashDifference;
            var sourceDocument = $"PosShiftVariance:{shiftId}";

            // ‏**ولا يُقيَّد الفرقُ مرّتين** — والعلامةُ القيدُ نفسُه، فلا عمودَ جديد ولا هجرة.
            if (await db.JournalEntries.AnyAsync(
                    e => e.CompanyId == companyId
                         && e.Status == JournalEntryStatus.Posted
                         && e.SourceDocument == sourceDocument, token))
                throw new AccountingException("فرقُ هذه الوردية مُقيَّدٌ مسبقاً.");

            // ‏**والصفرُ لا يُقيَّد** — وقيدٌ بلا مبلغٍ يملأ الأستاذ بضجيجٍ يُخفي الفروقَ الحقيقيّة.
            if (difference == 0m)
                return new PosShiftVarianceResult(shift.ExpectedCash, shift.ClosingCash, 0m, null);

            var closedOn = DateOnly.FromDateTime((shift.ClosedAtUtc ?? DateTime.UtcNow).ToLocalTime());

            var cash = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.CashOnHand, token);
            var counterpart = await SystemAccountProvisioning.EnsureAsync(
                db, companyId,
                difference < 0m ? await Brqosh.Infrastructure.Accounting.PostingRoleService.ResolveAsync(db, companyId, ShortageExpenseCode, token) : SaudiChartOfAccounts.SystemCodes.OtherIncome,
                token);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, closedOn, PostingDocumentKind.JournalEntry,
                accountIds: [cash.Id, counterpart.Id], ct: token);

            var entry = JournalEntry.CreateDraft(companyId, closedOn,
                $"فرق نقد وردية {shift.CashierName}", sourceDocument: sourceDocument);

            if (difference < 0m)
            {
                // ‏عجز: الدرجُ أقلُّ من الدفتر — **فيُنزَل الدفترُ إلى المعدود** ويُحمَّل الفرق.
                entry.AddLine(counterpart, -difference, 0m, "عجز نقد وردية");
                entry.AddLine(cash, 0m, -difference, "تسوية صندوق — عجز");
            }
            else
            {
                entry.AddLine(cash, difference, 0m, "تسوية صندوق — زيادة");
                entry.AddLine(counterpart, 0m, difference, "زيادة نقد وردية");
            }

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, closedOn);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "PosShiftVariance", shiftId.ToString(),
                reason: "تقييد فرق نقد وردية",
                details: $"المتوقَّع {shift.ExpectedCash:N2} · المعدود {shift.ClosingCash:N2} "
                       + $"· الفرق {difference:N2}",
                ct: token);

            return new PosShiftVarianceResult(
                shift.ExpectedCash, shift.ClosingCash, difference, entry.Id);
        }, ct);
    }
}
