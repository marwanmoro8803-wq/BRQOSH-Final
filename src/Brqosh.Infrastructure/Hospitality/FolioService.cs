using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Hospitality;

/// <summary>حصيلةُ تشغيلةِ تقييدِ الليالي — وما تعذّر يُسمّى.</summary>
public record NightAccrualResult(int NightsPosted, int FoliosTouched, IReadOnlyList<string> Refusals);

/// <summary>
/// (CEEF-230 · ط3-٣ب) <b>حسابُ النزيل — الليالي تُقيَّد ليلةً بليلة، والحساب يُصفّى مرّةً.</b>
///
/// <para><b>وإيرادُ الغرفة يُستحقّ بمرور الليلة لا بالوصول</b>: نزيلٌ يصل في ٢٨ ديسمبر
/// لعشر ليالٍ، <b>وقيدُ إيرادِ العشر يومَ وصوله يُدخل سبعَ ليالٍ من إيراد يناير في
/// ديسمبر</b> — فتُقفل سنةٌ بإيرادٍ ليس لها ويُدفع عنه زكاةٌ قبل أوانه. وفي فندقٍ ممتلئٍ
/// آخرَ السنة الفرقُ ليس بنداً بل قائمةً كاملة.</para>
///
/// <para><b>ودفعةٌ يُطلقها المستخدم</b> على مثال <c>RecurringInvoiceService</c> — فلا
/// مُجدوِلَ عملٍ في هذا التطبيق.</para>
/// </summary>
public class FolioService(BrqoshDbContext db)
{
    /// <summary>يُنزل النزيلَ ويفتح حسابَه — <b>ولا نزولَ في غرفةٍ محجوزةٍ لغيره</b>.</summary>
    public async Task<GuestFolio> CheckInAsync(long companyId, long reservationId,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var reservation = await db.Reservations
                .SingleOrDefaultAsync(r => r.Id == reservationId && r.CompanyId == companyId, token)
                ?? throw new AccountingException("الحجز غير موجود.");

            var room = await db.Rooms
                .SingleOrDefaultAsync(r => r.Id == reservation.RoomId, token)
                ?? throw new AccountingException("الغرفة غير موجودة.");

            // ‏**والتداخلُ يُفحص بالتواريخ لا بحالة الغرفة** — كما في `ط3-١`. وحالةُ الغرفة
            // ‏لا تعرف أنّ نزيلَها يغادر غداً، **فمنعُ حجزِ الغد يُعطّل الغرفةَ بلا سبب**.
            var clashes = await db.Reservations
                .Where(r => r.CompanyId == companyId && r.RoomId == reservation.RoomId
                            && r.Id != reservation.Id
                            && (r.Status == ReservationStatus.CheckedIn
                                || r.Status == ReservationStatus.Booked))
                .ToListAsync(token);

            var clash = clashes.FirstOrDefault(
                r => r.Status == ReservationStatus.CheckedIn
                     && r.OverlapsWith(reservation.CheckInDate, reservation.CheckOutDate));

            if (clash is not null)
                throw new AccountingException(
                    $"الغرفة مشغولةٌ بالحجز «{clash.ReservationNumber}» "
                    + $"من {clash.CheckInDate:yyyy-MM-dd} إلى {clash.CheckOutDate:yyyy-MM-dd}.");

            reservation.CheckIn(room);

            var folio = GuestFolio.OpenFor(companyId, reservation.Id);
            db.GuestFolios.Add(folio);
            await db.SaveChangesAsync(token);

            return folio;
        }, ct);
    }

    /// <summary>
    /// يُقيّد كلَّ ليلةٍ مضت ولم تُقيَّد حتى <paramref name="asOf"/>.
    /// </summary>
    /// <param name="asOf">
    /// <b>وسيطٌ لا <c>DateTime.Today</c></b> — فتشغيلةٌ تقرأ ساعةَ الجهاز لا تُعاد بنتيجتها.
    /// </param>
    public async Task<NightAccrualResult> AccrueNightsAsync(long companyId, DateOnly asOf,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        var reservations = await db.Reservations
            .Where(r => r.CompanyId == companyId && r.Status == ReservationStatus.CheckedIn)
            .ToListAsync(ct);

        var posted = 0;
        var touched = 0;
        var refusals = new List<string>();

        foreach (var reservation in reservations)
        {
            try
            {
                var added = await AccrueOneAsync(reservation, asOf, ct);
                posted += added;
                if (added > 0) touched++;
            }
            catch (Exception ex)
            {
                refusals.Add($"«{reservation.ReservationNumber}»: {FirstLine(ex)}");
            }
        }

        return new NightAccrualResult(posted, touched, refusals);
    }

    /// <summary>
    /// <b>والليلةُ الأخيرة هي ما قبل المغادرة</b>: النطاق <c>[الوصول، المغادرة)</c>.
    ///
    /// <para>فنزيلٌ يصل الأوّلَ ويغادر الثالث <b>بات ليلتين</b>. <b>وشمولُ يوم المغادرة
    /// يُضيف ليلةً إلى كلّ فاتورةٍ في الفندق</b> — يراها النزيلُ عند الحساب فيُنازع،
    /// ويُخصمها الموظّفُ يدوياً فيختلّ الإيرادُ عن الإشغال.</para>
    /// </summary>
    private async Task<int> AccrueOneAsync(Reservation reservation, DateOnly asOf, CancellationToken ct) =>
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var folio = await db.GuestFolios
                .SingleOrDefaultAsync(f => f.ReservationId == reservation.Id && !f.IsSettled, token)
                ?? throw new AccountingException("لا حسابَ مفتوحاً لهذا الحجز.");

            var added = 0;
            var last = asOf < reservation.CheckOutDate ? asOf : reservation.CheckOutDate.AddDays(-1);

            for (var night = reservation.CheckInDate; night <= last; night = night.AddDays(1))
            {
                if (folio.HasRoomNight(night)) continue;
                folio.AddRoomNight(night, reservation.RatePerNight);
                added++;
            }

            if (added > 0) await db.SaveChangesAsync(token);
            return added;
        }, ct);

    /// <summary>يُضيف رسماً غيرَ ليليّ — مطعمٌ أو غسيلٌ أو ثلاجة.</summary>
    public async Task AddChargeAsync(long companyId, long folioId, DateOnly date,
        FolioChargeKind kind, string description, decimal amount, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        var folio = await db.GuestFolios
            .SingleOrDefaultAsync(f => f.Id == folioId && f.CompanyId == companyId, ct)
            ?? throw new AccountingException("حساب النزيل غير موجود.");

        folio.AddCharge(date, kind, description, amount);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// يُغادر النزيلُ: تُقيَّد الليالي الباقية، ويُصفّى الحساب بفاتورةٍ واحدة.
    ///
    /// <para><b>وهويّةُ هـ-١٢:</b> مجموعُ رسوم الحساب = إجماليُّ الفاتورة. <b>ورسمٌ يسقط
    /// بينهما إيرادٌ قُدِّم ولم يُطالَب به</b> — والنزيلُ غادر ولا سبيلَ إلى تحصيله.</para>
    ///
    /// <para><b>وسطرٌ لكلّ رسمٍ لا سطرٌ واحدٌ بالمجموع</b>: النزيلُ ينازع في الثلاجة لا في
    /// «المجموع»، <b>وفاتورةٌ بسطرٍ واحدٍ تجعل كلَّ نزاعٍ مراجعةً يدويةً للسجلّ</b>.</para>
    /// </summary>
    public async Task<long> CheckOutAsync(long companyId, long reservationId, DateOnly on,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        var invoiceId = await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var reservation = await db.Reservations
                .SingleOrDefaultAsync(r => r.Id == reservationId && r.CompanyId == companyId, token)
                ?? throw new AccountingException("الحجز غير موجود.");

            var room = await db.Rooms
                .SingleOrDefaultAsync(r => r.Id == reservation.RoomId, token)
                ?? throw new AccountingException("الغرفة غير موجودة.");

            var folio = await db.GuestFolios
                .SingleOrDefaultAsync(f => f.ReservationId == reservation.Id && !f.IsSettled, token)
                ?? throw new AccountingException("لا حسابَ مفتوحاً لهذا الحجز.");

            // ‏**وما بقي من الليالي يُقيَّد قبل التصفية**: مغادرةٌ قبل تشغيل الدفعة تُصفّي
            // ‏حساباً ناقصاً ليالٍ — **وإيرادٌ يضيع بلا أثرٍ يدلّ عليه**.
            for (var night = reservation.CheckInDate; night < reservation.CheckOutDate; night = night.AddDays(1))
            {
                if (!folio.HasRoomNight(night))
                    folio.AddRoomNight(night, reservation.RatePerNight);
            }

            var invoice = SalesInvoice.CreateDraft(companyId, reservation.GuestCustomerId, on);
            foreach (var charge in folio.Charges.OrderBy(c => c.ChargeDate))
                invoice.AddLine(charge.Description, 1m, charge.Amount, vatCategory: charge.VatCategory);

            await new SalesInvoicePostingService(db).PostCoreAsync(invoice, token);

            folio.MarkSettled();
            reservation.CheckOut(room, invoice.Id);
            await db.SaveChangesAsync(token);

            return invoice.Id;
        }, ct);

        // (CEEF-230 · ط6-٧) ‏**والمستندُ وُلِّد مع الترحيل، والإبلاغُ بعد الـcommit** —
        // ‏فاتورةُ المغادرة فاتورةُ ضريبةٍ كاملة، **والنزيلُ يغادر بها**.
        await ZatcaPostingOrchestrator.SubmitAfterCommitAsync(
            db, companyId, ZatcaDocumentType.SalesInvoice, invoiceId, ct);

        return invoiceId;
    }

    private static string FirstLine(Exception ex)
    {
        var deepest = ex;
        while (deepest.InnerException is not null) deepest = deepest.InnerException;
        return deepest.Message.Split('\n')[0].Trim();
    }
}
