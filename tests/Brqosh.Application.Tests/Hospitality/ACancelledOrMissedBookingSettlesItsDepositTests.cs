using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Hospitality;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Hospitality;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٨٣ · <c>sector-modules#5</c>) <b>الحجزُ يُلغى أو يُسجَّل غيابُ صاحبه — وعربونُه يُردّ أو يُصادَر.</b>
///
/// <para><b>ما كان:</b> <c>Reservation.Cancel()</c> بلا مستدعٍ واحد، ولا خدمةَ ولا زرَّ لإلغاءٍ أو غياب، <b>ولا مسارَ للعربون إلّا
/// الإنزالُ عند المغادرة</b>. فحجزٌ أُلغي يبقى «محجوزاً» يحجب غرفتَه في كلّ فحص تداخل، <b>وعربونُه التزامٌ خالدٌ في <c>2105</c></b>:
/// لا يُردّ للنزيل بقيد، ولا يصير إيراداً إن صُودر.</para>
/// </summary>
public class ACancelledOrMissedBookingSettlesItsDepositTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _guestId;
    private long _roomId;

    private static readonly DateOnly Booked = new(2026, 2, 10);
    private static readonly DateOnly CheckIn = new(2026, 3, 1);
    private static readonly DateOnly CheckOut = new(2026, 3, 3);

    public ACancelledOrMissedBookingSettlesItsDepositTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>إلغاءٌ بردّ العربون</b>: أربعُمئةٍ قُبضت (٣٤٧٫٨٣ التزاماً و٥٢٫١٧ ضريبة) تُردّ كلُّها — <b>فالالتزامُ والضريبةُ والصندوقُ أصفار</b>،
    /// ولا إيراد.
    /// </summary>
    [Fact]
    public async Task Cancelling_WithARefund_ReturnsTheDepositAndItsVat()
    {
        await SetupAsync();
        var reservation = await BookedWithDepositAsync("R-C1", 400m);

        await new FolioService(_db).CancelAsync(_companyId, reservation.Id, new DateOnly(2026, 2, 20), DepositOutcome.Refund);

        Assert.Equal(ReservationStatus.Cancelled, await StatusAsync(reservation.Id));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatOutput));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.SalesRevenue));
    }

    /// <summary>
    /// <b>غيابٌ بمصادرة العربون</b>: صافيه إيرادٌ (٣٤٧٫٨٣) <b>وضريبتُه باقيةٌ مستحقّة</b> (٥٢٫١٧) — فالمصادَرُ مقابلٌ لتوريدٍ خاضع،
    /// والنقدُ عند الفندق.
    /// </summary>
    [Fact]
    public async Task ANoShow_WithTheDepositForfeited_MakesItRevenue_AndKeepsItsVat()
    {
        await SetupAsync();
        var reservation = await BookedWithDepositAsync("R-C2", 400m);

        await new FolioService(_db).MarkNoShowAsync(_companyId, reservation.Id, CheckIn, DepositOutcome.Forfeit);

        Assert.Equal(ReservationStatus.NoShow, await StatusAsync(reservation.Id));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers));
        Assert.Equal(-347.83m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.SalesRevenue));
        Assert.Equal(-52.17m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatOutput));
        Assert.Equal(400m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
    }

    /// <summary>
    /// <b>وضريبةُ المردود تخرج من إقرار شهر الردّ</b> — فبرايرُ أقرّ ٥٢٫١٧ يومَ القبض، ومارسُ يخصمها يومَ الردّ. <b>وبدون ذلك تبقى
    /// ضريبةُ مالٍ رُدّ مدفوعةً للهيئة.</b>
    /// </summary>
    [Fact]
    public async Task TheRefundedVat_LeavesTheReturnOfTheRefundMonth()
    {
        await SetupAsync();
        var reservation = await BookedWithDepositAsync("R-C3", 400m);

        await new FolioService(_db).CancelAsync(_companyId, reservation.Id, new DateOnly(2026, 3, 5), DepositOutcome.Refund);

        var february = await new VatReturnQuery(_db).ExecuteAsync(_companyId, new(2026, 2, 1), new(2026, 2, 28));
        var march = await new VatReturnQuery(_db).ExecuteAsync(_companyId, new(2026, 3, 1), new(2026, 3, 31));
        Assert.Equal(52.17m, february.NetOutputVat);
        Assert.Equal(-52.17m, march.NetOutputVat);
    }

    /// <summary><b>ولا يُسجَّل غيابٌ قبل يوم الوصول</b> — فالنزيلُ لم يتأخّر بعد، ومصادرةُ عربونه يومئذٍ مصادرةٌ بلا سبب.</summary>
    [Fact]
    public async Task ANoShowBeforeTheArrivalDay_IsRefused()
    {
        await SetupAsync();
        var reservation = await BookedWithDepositAsync("R-C4", 400m);

        await Assert.ThrowsAsync<AccountingException>(() => new FolioService(_db)
            .MarkNoShowAsync(_companyId, reservation.Id, CheckIn.AddDays(-1), DepositOutcome.Forfeit));

        Assert.Equal(ReservationStatus.Booked, await StatusAsync(reservation.Id));
        Assert.Equal(-347.83m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers));
    }

    /// <summary>
    /// <b>وحجزٌ أُلغي لا يُلغى ثانيةً</b> — ولا يُسجَّل غيابُه بعد إلغائه؛ <b>وإلّا انقلب إلى «لم يحضر» وصودر ما رُدّ</b>.
    /// </summary>
    [Fact]
    public async Task ACancelledReservation_CannotBeCancelledAgain_NorMarkedAsNoShow()
    {
        await SetupAsync();
        var reservation = await BookedWithDepositAsync("R-C5", 400m);
        var service = new FolioService(_db);
        await service.CancelAsync(_companyId, reservation.Id, new DateOnly(2026, 2, 20), DepositOutcome.Refund);

        await Assert.ThrowsAsync<AccountingException>(() =>
            service.CancelAsync(_companyId, reservation.Id, new DateOnly(2026, 2, 21), DepositOutcome.Refund));
        await Assert.ThrowsAsync<AccountingException>(() =>
            service.MarkNoShowAsync(_companyId, reservation.Id, CheckIn, DepositOutcome.Forfeit));

        Assert.Equal(ReservationStatus.Cancelled, await StatusAsync(reservation.Id));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
    }

    /// <summary>
    /// <b>وما يحتجزه الفندقُ بعد المغادرة صفر</b> — وكان المُنزَلُ يُطرح بإشارته فيُضاعَف: عربونُ ٤٠٠ يُقرأ ٨٠٠ بعد إنزاله.
    /// <b>وهو ما تسأله الشاشةُ قبل الإلغاء</b>.
    /// </summary>
    [Fact]
    public async Task AfterCheckOut_NoDepositIsHeld()
    {
        await SetupAsync();
        var reservation = await BookedWithDepositAsync("R-C6", 400m);
        await new FolioService(_db).CheckInAsync(_companyId, reservation.Id);
        await new FolioService(_db).CheckOutAsync(_companyId, reservation.Id, CheckOut);

        Assert.Equal(0m, await new ReservationDepositService(_db).HeldAsync(_companyId, reservation.Id));
    }

    /// <summary>
    /// <b>وعربونٌ قُبض قبل ح-٨١ بلا ضريبة يُصادَر بضريبته الآن</b> — فالمصادَرُ مقابلٌ خاضع، وضريبتُه لم تُقَرّ قطّ: ٣٤٧٫٨٣ إيراداً
    /// و٥٢٫١٧ مخرجات. <b>ويُردّ كلُّه إن رُدّ</b>.
    /// </summary>
    [Fact]
    public async Task ALegacyDepositForfeited_ChargesItsVatNow()
    {
        await SetupAsync();
        var reservation = await BookedAsync("R-C7");
        await LegacyDepositAsync(reservation.Id, 400m);

        await new FolioService(_db).MarkNoShowAsync(_companyId, reservation.Id, CheckIn, DepositOutcome.Forfeit);

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers));
        Assert.Equal(-347.83m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.SalesRevenue));
        Assert.Equal(-52.17m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatOutput));
    }

    /// <summary>
    /// <b>شاهدُ ضبط: إلغاءٌ بلا عربونٍ لا يُرحّل شيئاً</b> — والحالةُ وحدَها تتغيّر.
    /// </summary>
    [Fact]
    public async Task CancellingWithoutADeposit_PostsNothing()
    {
        await SetupAsync();
        var reservation = await BookedAsync("R-C8");

        await new FolioService(_db).CancelAsync(_companyId, reservation.Id, new DateOnly(2026, 2, 20), DepositOutcome.Refund);

        Assert.Equal(ReservationStatus.Cancelled, await StatusAsync(reservation.Id));
        Assert.False(await _db.JournalEntries.AnyAsync(e => e.CompanyId == _companyId && e.SourceDocument != null
                                                            && e.SourceDocument.StartsWith("ReservationDeposit")));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<ReservationStatus> StatusAsync(long reservationId) =>
        (await _db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId)).Status;

    private async Task<Reservation> BookedAsync(string number)
    {
        var reservation = Reservation.Create(_companyId, _roomId, _guestId, number, CheckIn, CheckOut, 500m);
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();
        return reservation;
    }

    private async Task<Reservation> BookedWithDepositAsync(string number, decimal deposit)
    {
        var reservation = await BookedAsync(number);
        await new ReservationDepositService(_db).TakeAsync(_companyId, reservation.Id, deposit, Booked, "admin");
        return reservation;
    }

    private async Task LegacyDepositAsync(long reservationId, decimal amount)
    {
        var cash = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var advances = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers);
        var legacy = JournalEntry.CreateDraft(_companyId, Booked, "عربون حجز قديم",
            $"{ReservationDepositService.TakenPrefix}{reservationId}");
        legacy.AddLine(cash, amount, 0m, "قبض");
        legacy.AddLine(advances, 0m, amount, "التزام");
        legacy.Post("JE-LEGACY-DEP");
        _db.JournalEntries.Add(legacy);
        await _db.SaveChangesAsync();
    }

    private async Task<decimal> BalanceAsync(string code) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == code)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Debit - l.Credit);

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("فندق", "310123456700044", BusinessProfile.Hospitality);
        _companyId = company.Id;

        var guest = Customer.Create(_companyId, "نزيل", "");
        _db.Customers.Add(guest);

        var room = Room.Create(_companyId, "201", RoomKind.Double, 500m, floor: 2);
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();

        _guestId = guest.Id;
        _roomId = room.Id;
    }
}
