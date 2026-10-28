using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Hospitality;

/// <summary> ما يُصنع بعربون حجزٍ أُلغي أو غاب صاحبُه.</summary>
public enum DepositOutcome
{
    /// <summary>يُردّ إلى النزيل — صافيه وضريبتُه.</summary>
    Refund = 0,

    /// <summary>يُصادَر إيراداً — وضريبتُه باقيةٌ مستحقّة: المصادَرُ مقابلٌ لتوريدٍ خاضع.</summary>
    Forfeit = 1,
}

/// <summary>
/// <b>عربونُ الحجز — مالٌ يُقبض قبل أن يُستحقّ.</b>
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
/// <para><b>والعربونُ التزامٌ لا إيراد:</b> <c>مدين النقد / دائن 2105</c> بصافيه. <b>وضريبتُه تُستحقّ يومَ
/// قبضه</b> (ح-٨١) — المقابلُ المقبوضُ مقدّماً نقطةُ استحقاق — فتُقيَّد في <c>2106</c> وتُردّ عند إنزاله على
/// فاتورة المغادرة التي تحمل ضريبةَ الإقامة كلِّها.</para>
///
/// <para><b>وهويّةُ ح-١:</b> قبل المغادرة: <c>2105 يحمل صافيَ العربون</c> و<c>2106</c> ضريبتَه ولا إيراد.
/// وبعدها: <c>2105 يعود صفراً</c> <b>وذمّةُ النزيل تنقص بالعربون</b> — في الأستاذ
/// وفي الدفتر المساعد معاً.</para>
/// </summary>
public class ReservationDepositService(BrqoshDbContext db)
{
    /// <summary>بادئةُ مصدر قيد قبض العربون.</summary>
    public const string TakenPrefix = "ReservationDeposit:";

    /// <summary>بادئةُ مصدر قيد إنزال العربون على فاتورة المغادرة.</summary>
    public const string AppliedPrefix = "ReservationDepositApplied:";

    /// <summary>(ح-٨٣) بادئةُ مصدر قيد ردّ العربون عند الإلغاء أو الغياب.</summary>
    public const string RefundedPrefix = "ReservationDepositRefunded:";

    /// <summary>(ح-٨٣) بادئةُ مصدر قيد مصادرة العربون عند الإلغاء أو الغياب.</summary>
    public const string ForfeitedPrefix = "ReservationDepositForfeited:";

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
            if (reservation.Status is ReservationStatus.CheckedOut or ReservationStatus.Cancelled or ReservationStatus.NoShow)
                throw new AccountingException("لا يُقبض عربونٌ على حجزٍ مُغادَرٍ أو مُلغى أو غاب صاحبُه.");

            var cash = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.CashOnHand, token);
            var advances = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers, token);
            var vatOutput = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.VatOutput, token);

            await FinancialPostingSupport.ValidateAsync(
                db, companyId, on, PostingDocumentKind.JournalEntry,
                accountIds: [cash.Id, advances.Id, vatOutput.Id], ct: token);

            var entry = JournalEntry.CreateDraft(companyId, on,
                $"عربون حجز {reservation.ReservationNumber}",
                sourceDocument: $"{TakenPrefix}{reservationId}");

            // ‏ ‏**والعربونُ نقطةُ استحقاقٍ للضريبة — يوم القبض.**
            //
            // ‏كان يُقيَّد كلُّه التزاماً في `2105` بلا ضريبة، **فتُستحقّ ضريبتُه يوم المغادرة لا يوم القبض**: عربونُ
            // ‏فبراير لإقامةِ مارس يغيب عن إقرار فبراير. ونظامُ الضريبة يجعل المقابلَ المقبوضَ مقدّماً نقطةَ استحقاق.
            // ‏**فالعربونُ شاملٌ للضريبة**: صافيه التزامٌ، وضريبتُه مخرجاتٌ تبلغ إقرارَ شهرها (قيدٌ ليس مستنداً محسوباً).
            var (net, vat) = SplitVatInclusive(amount);
            entry.AddLine(cash, amount, 0m, "قبض عربون حجز");
            entry.AddLine(advances, 0m, net, "عربون حجز — التزام (صافياً من الضريبة)");
            if (vat > 0m)
                entry.AddLine(vatOutput, 0m, vat, "ضريبةُ العربون — تُستحقّ عند القبض");

            var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, on);
            entry.Post(number);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(token);

            // ‏ ‏**وللعربون فاتورةُ دفعةٍ مقدّمة (386) يومَ قبضه** — والتوليدُ داخل المعاملة
            // ‏كعرف المستودع، والإرسالُ يتولّاه مسارُ المعلَّق. وعربونٌ بلا ضريبةٍ (منشأةٌ غيرُ مسجَّلة) لا فاتورةَ ضريبةٍ له.
            if (vat > 0m)
                await ZatcaPostingOrchestrator.GenerateInsideTransactionAsync(
                    db, companyId,
                    () => new ZatcaEInvoiceGenerator(db).TryGenerateForPrepaymentAsync(entry.Id),
                    token);

            await FinancialPostingSupport.AuditPostAsync(
                db, companyId, "ReservationDeposit", reservation.ReservationNumber,
                reason: "قبض عربون حجز", details: $"المبلغ {amount:N2}", ct: token);

            return entry;
        }, ct);
    }

    /// <summary>
    /// <b>يُنزل العربونَ على ذمّة النزيل عند المغادرة</b> — <c>مدين 2105 و2106 / دائن 1104</c>.
    /// </summary>
    /// <remarks>
    /// <b>ولا يُقيَّد إن لم يوجد عربون</b>، ولا يُنزَل مرّتين. <b>ويُستدعى داخل معاملة
    /// المغادرة نفسِها</b> — فمغادرةٌ تنجح وإنزالٌ يفشل تترك النزيلَ مطالَباً بما دفعه.
    /// </remarks>
    internal async Task<decimal> ApplyOnCheckOutAsync(
        long companyId, long reservationId, DateOnly on, CancellationToken ct = default)
    {
        // ‏(ح-٨١) ‏**صافي العربون وضريبتُه كلٌّ من حسابه** — وعربونٌ قُبض قبل هذا بلا ضريبةٍ يُنزَل كلُّه كما كان.
        var net = await OutstandingOnAsync(companyId, reservationId, SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers, ct);
        var vat = await OutstandingOnAsync(companyId, reservationId, SaudiChartOfAccounts.SystemCodes.VatOutput, ct);
        var outstanding = net + vat;
        if (outstanding <= 0m)
            return 0m;

        var receivable = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.AccountsReceivable, ct);
        var advances = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers, ct);

        var entry = JournalEntry.CreateDraft(companyId, on,
            $"إنزال عربون حجز {reservationId}",
            sourceDocument: $"{AppliedPrefix}{reservationId}");

        if (net > 0m)
            entry.AddLine(advances, net, 0m, "إنزال عربون على فاتورة المغادرة");

        // ‏(ح-٨١) ‏**وضريبةُ العربون تُردّ من المخرجات** — ففاتورةُ المغادرة تحمل ضريبةَ الإقامة كلِّها، ومنها ما
        // ‏استُحقّ عند القبض؛ وبلا هذا تُدفع ضريبةُ العربون مرّتين.
        if (vat > 0m)
        {
            var vatOutput = await SystemAccountProvisioning.EnsureAsync(
                db, companyId, SaudiChartOfAccounts.SystemCodes.VatOutput, ct);
            entry.AddLine(vatOutput, vat, 0m, "ضريبةُ العربون المستحقّةُ سلفاً — تُخصم من ضريبة الفاتورة");
        }

        entry.AddLine(receivable, 0m, outstanding, "تخفيض ذمة النزيل بالعربون");

        var number = await new JournalEntryNumberGenerator(db).NextAsync(companyId, on);
        entry.Post(number);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        return outstanding;
    }

    /// <summary>
    /// العربونُ الذي يحتجزه الفندقُ الآن — <b>يُقرأ من الأستاذ لا من حقل</b>، صافيه وضريبتُه. وهو ما تسأله الشاشةُ قبل
    /// الإلغاء: يُردّ أم يُصادَر.
    /// </summary>
    public async Task<decimal> HeldAsync(
        long companyId, long reservationId, CancellationToken ct = default) =>
        await OutstandingOnAsync(companyId, reservationId, SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers, ct)
        + await OutstandingOnAsync(companyId, reservationId, SaudiChartOfAccounts.SystemCodes.VatOutput, ct);

    /// <summary>
    /// (ح-٨١) ما بقي من العربون على حسابٍ بعينه — المقبوضُ ناقصاً ما أُنزل أو رُدّ أو صُودر.
    ///
    /// <para>(ح-٨٣) <b>ومجموعُ الدائن ناقصاً المدين في قيود كلّ مصدرٍ يحمل إشارتَه</b>: القبضُ موجبٌ، والإنزالُ والردُّ
    /// والمصادرةُ سالبة — فتُجمع. <b>وكان الإنزالُ يُطرح بإشارته فيُضاعَف</b>: عربونٌ أُنزل يُقرأ ضعفَه، ولم يُرَ لأنّ السؤالَ
    /// لم يُسأل بعد الإنزال قطّ — حتى صار الإلغاءُ يسأله.</para>
    /// </summary>
    private async Task<decimal> OutstandingOnAsync(
        long companyId, long reservationId, string accountCode, CancellationToken ct)
    {
        var held = 0m;
        foreach (var prefix in new[] { TakenPrefix, AppliedPrefix, RefundedPrefix, ForfeitedPrefix })
            held += await SumAsync(companyId, $"{prefix}{reservationId}", accountCode, ct);
        return Math.Round(held, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// <b>حجزٌ أُلغي أو غاب صاحبُه — وعربونُه يُردّ أو يُصادَر.</b> وكان لا مسارَ له إلّا الإنزالُ عند
    /// المغادرة، فيبقى التزاماً خالداً في <c>2105</c>.
    ///
    /// <para><b>الردُّ</b>: مدين <c>2105</c> بالصافي و<c>2106</c> بالضريبة، دائن النقد بالإجمالي — والإقرارُ يقرأ ردَّ الضريبة
    /// خصماً في شهره. <b>والمصادرةُ</b>: مدين <c>2105</c>، دائن الإيراد بالصافي — <b>والضريبةُ باقية</b>، فالمصادَرُ مقابلٌ
    /// لتوريدٍ خاضع؛ وعربونٌ قُبض قبل ح-٨١ بلا ضريبةٍ تُستخرج ضريبتُه الآن.</para>
    /// </summary>
    internal async Task SettleOnCancellationAsync(
        long companyId, Reservation reservation, DateOnly on, DepositOutcome outcome, CancellationToken ct)
    {
        var net = await OutstandingOnAsync(companyId, reservation.Id, SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers, ct);
        var vat = await OutstandingOnAsync(companyId, reservation.Id, SaudiChartOfAccounts.SystemCodes.VatOutput, ct);
        if (net + vat <= 0m)
            return;

        // ‏**والمالُ يتحرّك هنا** — فحارسُ القبض نفسُه: من لا يقبض العربونَ لا يردّه ولا يصادره.
        SecurityContext.EnsureCanPostAccounting();

        var advances = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers, ct);
        var vatOutput = await SystemAccountProvisioning.EnsureAsync(
            db, companyId, SaudiChartOfAccounts.SystemCodes.VatOutput, ct);
        var counter = await SystemAccountProvisioning.EnsureAsync(db, companyId,
            outcome == DepositOutcome.Refund
                ? SaudiChartOfAccounts.SystemCodes.CashOnHand
                : SaudiChartOfAccounts.SystemCodes.SalesRevenue, ct);

        await FinancialPostingSupport.ValidateAsync(
            db, companyId, on, PostingDocumentKind.JournalEntry,
            accountIds: [advances.Id, vatOutput.Id, counter.Id], ct: ct);

        var refund = outcome == DepositOutcome.Refund;
        var entry = JournalEntry.CreateDraft(companyId, on,
            (refund ? "ردّ عربون حجز " : "مصادرة عربون حجز ") + reservation.ReservationNumber,
            sourceDocument: $"{(refund ? RefundedPrefix : ForfeitedPrefix)}{reservation.Id}");

        if (net > 0m)
            entry.AddLine(advances, net, 0m, refund ? "ردّ العربون — التزامٌ يُقضى" : "مصادرة العربون — التزامٌ يسقط");

        if (refund)
        {
            if (vat > 0m)
                entry.AddLine(vatOutput, vat, 0m, "ضريبةُ العربون المردود — تُخصم من مخرجات شهر الردّ");
            entry.AddLine(counter, 0m, net + vat, "ردّ العربون إلى النزيل");
        }
        else
        {
            // ‏**وعربونٌ قُبض قبل ح-٨١ بلا ضريبة** (net = الإجماليّ، vat = صفر) — المصادَرُ شاملٌ للضريبة، فتُستخرج الآن.
            var (revenue, due) = vat > 0m ? (net, 0m) : SplitVatInclusive(net);
            entry.AddLine(counter, 0m, revenue, "عربونٌ مصادَرٌ — إيرادُ الغرفة التي حُجزت ولم تُشغَل");
            if (due > 0m)
                entry.AddLine(vatOutput, 0m, due, "ضريبةُ عربونٍ مصادَرٍ قُبض بلا ضريبة");
        }

        entry.Post(await new JournalEntryNumberGenerator(db).NextAsync(companyId, on, ct));
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        // ‏ ‏**والردُّ إشعارٌ دائنٌ لكلّ فاتورةِ دفعة (386)** — يُشير إليها بمبلغها. والمصادرةُ
        // ‏لا إشعارَ لها: المصادَرُ مقابلُ توريدٍ خاضعٍ وفاتورتُه قائمة.
        if (refund)
        {
            var source = $"{TakenPrefix}{reservation.Id}";
            var deposits = await db.JournalEntries
                .Where(e => e.CompanyId == companyId && e.SourceDocument == source)
                .WhereStanding(db.JournalEntries)
                .Select(e => e.Id)
                .ToListAsync(ct);
            foreach (var depositId in deposits)
                await ZatcaPostingOrchestrator.GenerateInsideTransactionAsync(
                    db, companyId,
                    () => new ZatcaEInvoiceGenerator(db).TryGenerateRefundForPrepaymentAsync(depositId, on),
                    ct);
        }

        await FinancialPostingSupport.AuditPostAsync(
            db, companyId, "ReservationDeposit", reservation.ReservationNumber,
            reason: refund ? "ردّ عربون حجز" : "مصادرة عربون حجز", details: $"المبلغ {net + vat:N2}", ct: ct);
    }

    /// <summary>
    /// (ح-٨١) <b>العربونُ شاملٌ للضريبة</b> — صافيه وضريبتُه بالنسبة الأساسيّة، والضريبةُ مقرَّبةٌ والصافي باقيه فمجموعُهما
    /// المقبوضُ بالهللة.
    /// </summary>
    internal static (decimal Net, decimal Vat) SplitVatInclusive(decimal gross)
    {
        var vat = Math.Round(gross * VatCalculator.SaudiVatRate / (1m + VatCalculator.SaudiVatRate), 2,
            MidpointRounding.AwayFromZero);
        return (gross - vat, vat);
    }

    /// <summary>مجموعُ ما دائنَ حسابٌ بعينه في قيودِ مصدرٍ بعينه.</summary>
    private async Task<decimal> SumAsync(long companyId, string sourceDocument, string accountCode, CancellationToken ct)
    {
        var accountId = await db.Accounts
            .Where(a => a.CompanyId == companyId && a.Code == accountCode)
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
