using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Hospitality;

/// <summary>
/// (CEEF-232 · ح-١) <b>عربونُ الحجز — مالٌ يُقبض قبل أن يُستحقّ.</b>
///
/// <para><b>ما كان:</b> <c>Reservation</c> بلا حقلِ عربون، و<c>FolioService</c> بلا مسارٍ
/// له — <c>CheckInAsync</c> و<c>AccrueNightsAsync</c> و<c>AddChargeAsync</c>
/// و<c>CheckOutAsync</c> ليس فيها واحدةٌ تقبض شيئاً قبل الإقامة. <b>وحسابُ «دفعات مقدَّمة
/// من العملاء» (<c>2105</c>) كاتبُه الوحيد في المستودع كلِّه خدمةُ المقاولات</b>.</para>
///
/// <para><b>فالعربونُ إمّا لا يُسجَّل أصلاً</b> — مالٌ في الصندوق بلا قيد، وهو أسوأُ ما
/// يقع في فندق — <b>أو يُقبض بسند قبضٍ فيُطفئ ذمّةً لم تنشأ بعد</b>، فيظهر النزيلُ دائناً
/// ويُعترف بالإيراد قبل الإقامة. <b>وهو خطأُ قطعٍ في كلّ حجزٍ مقدَّم</b>: ربعٌ يُقفل على
/// إيرادِ ليالٍ لم تُقضَ، والليالي تقع في الربع التالي بلا إيراد.</para>
///
/// <para><b>والعربونُ التزامٌ لا إيراد:</b> <c>مدين النقد / دائن 2105</c>. <b>ولا ضريبةَ
/// عليه هنا</b> — التوريدُ لم يقع، والفاتورةُ الضريبيّةُ تصدر عند المغادرة بكامل الإقامة.</para>
///
/// <para><b>وهويّةُ ح-١:</b> قبل المغادرة: <c>2105 يحمل العربون</c> ولا إيراد.
/// وبعدها: <c>2105 يعود صفراً</c> <b>وذمّةُ النزيل تنقص بالعربون</b> — في الأستاذ
/// وفي الدفتر المساعد معاً.</para>
/// </summary>
public class ReservationDepositService(BrqoshDbContext db)
{
    /// <summary>بادئةُ مصدر قيد قبض العربون.</summary>
    public const string TakenPrefix = "ReservationDeposit:";

    /// <summary>بادئةُ مصدر قيد إنزال العربون على فاتورة المغادرة.</summary>
    public const string AppliedPrefix = "ReservationDepositApplied:";

    /// <summary>
    /// يقبض عربوناً على حجزٍ قائم — <b>ويُقيّده التزاماً لا إيراداً</b>.
    /// </summary>
    public async Task<JournalEntry> TakeAsync(
        long companyId, long reservationId, decimal amount, DateOnly on, string username,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPostAccounting();
        username = SecurityContext.ResolveUsername(username);

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            if (amount <= 0m)
                throw new AccountingException("مبلغ العربون يجب أن يكون موجباً.");

            var reservation = await db.Reservations
                .SingleOrDefaultAsync(r => r.CompanyId == companyId && r.Id == reservationId, token)
                ?? throw new AccountingException("الحجز غير موجود.");

            // ‏**ولا يُقبض عربونٌ على حجزٍ غادر صاحبُه** — فالتصفيةُ وقعت، **وعربونٌ بعدها
            // ‏يبقى في `2105` بلا فاتورةٍ يُنزَل عليها**: التزامٌ خالدٌ تجاه نزيلٍ رحل.
            if (reservation.Status is ReservationStatus.CheckedOut or ReservationStatus.Cancelled)
                throw new AccountingException("لا يُقبض عربونٌ على حجزٍ مُغادَرٍ أو مُلغى.");

            var cash = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.CashOnHand, token);
            var advances = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers, token);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, on, PostingDocumentKind.JournalEntry,
                accountIds: [cash.Id, advances.Id], ct: token);

            var entry = JournalEntry.CreateDraft(companyId, on,
                $"عربون حجز {reservation.ReservationNumber}",
                sourceDocument: $"{TakenPrefix}{reservationId}");

            entry.AddLine(cash, amount, 0m, "قبض عربون حجز");
            entry.AddLine(advances, 0m, amount, "عربون حجز — التزام");

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, on);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "ReservationDeposit", reservation.ReservationNumber,
                reason: "قبض عربون حجز", details: $"المبلغ {amount:N2}", ct: token);

            return entry;
        }, ct);
    }

    /// <summary>
    /// <b>يُنزل العربونَ على ذمّة النزيل عند المغادرة</b> — <c>مدين 2105 / دائن 1104</c>.
    /// </summary>
    /// <remarks>
    /// <b>ولا يُقيَّد إن لم يوجد عربون</b>، ولا يُنزَل مرّتين. <b>ويُستدعى داخل معاملة
    /// المغادرة نفسِها</b> — فمغادرةٌ تنجح وإنزالٌ يفشل تترك النزيلَ مطالَباً بما دفعه.
    /// </remarks>
    internal async Task<decimal> ApplyOnCheckOutAsync(
        long companyId, long reservationId, DateOnly on, CancellationToken ct = default)
    {
        var outstanding = await OutstandingAsync(companyId, reservationId, ct);
        if (outstanding <= 0m)
            return 0m;

        var receivable = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.AccountsReceivable, ct);
        var advances = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers, ct);

        var entry = JournalEntry.CreateDraft(companyId, on,
            $"إنزال عربون حجز {reservationId}",
            sourceDocument: $"{AppliedPrefix}{reservationId}");

        entry.AddLine(advances, outstanding, 0m, "إنزال عربون على فاتورة المغادرة");
        entry.AddLine(receivable, 0m, outstanding, "تخفيض ذمة النزيل بالعربون");

        var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, on);
        entry.Post(number);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        return outstanding;
    }

    /// <summary>العربونُ المقبوضُ ولم يُنزَل بعد — <b>يُقرأ من الأستاذ لا من حقل</b>.</summary>
    internal async Task<decimal> OutstandingAsync(
        long companyId, long reservationId, CancellationToken ct = default)
    {
        var taken = await SumAsync(companyId, $"{TakenPrefix}{reservationId}", ct);
        var applied = await SumAsync(companyId, $"{AppliedPrefix}{reservationId}", ct);
        return Math.Round(taken - applied, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>مجموعُ ما دائنَ حسابُ الدفعات المقدَّمة في قيودِ مصدرٍ بعينه.</summary>
    private async Task<decimal> SumAsync(long companyId, string sourceDocument, CancellationToken ct)
    {
        var accountId = await db.Accounts
            .Where(a => a.CompanyId == companyId
                        && a.Code == SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers)
            .Select(a => (long?)a.Id)
            .FirstOrDefaultAsync(ct);
        if (accountId is null) return 0m;

        return await db.JournalEntries
            .Where(e => e.CompanyId == companyId
                        && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed)
                        && e.SourceDocument == sourceDocument)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountId == accountId)
            .SumAsync(l => (decimal?)(l.Credit - l.Debit), ct) ?? 0m;
    }
}
