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
/// (CEEF-232 · ح-١/ح-٢) <b>عربونُ الحجز — مالٌ يُقبض قبل أن يُستحقّ، ولم يكن له حساب.</b>
///
/// <para><b>ما كان:</b> <c>Reservation</c> بلا حقلِ عربون، و<c>FolioService</c> بلا مسارٍ
/// له، <b>و<c>2105</c> كاتبُه الوحيد في المستودع كلِّه خدمةُ المقاولات</b>. فالعربونُ
/// <b>إمّا لا يُسجَّل</b> — مالٌ في الصندوق بلا قيد — <b>أو يُقبض بسند قبضٍ فيُطفئ ذمّةً
/// لم تنشأ بعد</b>، فيُعترف بالإيراد قبل الإقامة.</para>
///
/// <para><b>وهو خطأُ قطعٍ في كلّ حجزٍ مقدَّم:</b> ربعٌ يُقفل على إيرادِ ليالٍ لم تُقضَ،
/// والليالي تقع في الربع التالي بلا إيراد.</para>
///
/// <para><b>وهويّةُ ح-١:</b> قبل المغادرة <c>2105</c> يحمل العربون ولا إيراد.
/// <b>وهويّةُ ح-٢:</b> بعدها <c>2105</c> يعود صفراً <b>وذمّةُ النزيل تنقص بالعربون —
/// في الأستاذ وفي الدفتر المساعد معاً</b>.</para>
/// </summary>
public class ReservationDepositTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _guestId;
    private long _roomId;

    private static readonly DateOnly Booked = new(2026, 2, 1);
    private static readonly DateOnly CheckIn = new(2026, 3, 1);
    private static readonly DateOnly CheckOut = new(2026, 3, 3);

    public ReservationDepositTests()
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
    /// <b>القياسُ الحاكم (ح-١): العربونُ التزامٌ — والإيرادُ لا يتحرّك بريال.</b>
    ///
    /// <para><b>وهذا هو العطلُ كلُّه</b>: بلا <c>2105</c> يُقيَّد العربونُ إيراداً أو
    /// يُطفئ ذمّةً لم تنشأ — <b>فيُعترف بإيرادِ إقامةٍ لم تقع بعد</b>.</para>
    /// </summary>
    [Fact]
    public async Task ADeposit_IsALiability_AndNeverTouchesRevenue()
    {
        await SetupAsync();
        var reservation = await BookedReservationAsync("R-D1");
        var revenueBefore = await BalanceAsync("4101");

        await new ReservationDepositService(_db)
            .TakeAsync(_companyId, reservation.Id, 400m, Booked, "admin");

        // ‏(ح-٨١) ‏**والعربونُ شاملٌ للضريبة**: ٣٤٧٫٨٣ التزاماً و٥٢٫١٧ ضريبةً مستحقّةً يوم القبض — وكان ٤٠٠ التزاماً بلا ضريبة.
        Assert.Equal(-347.83m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers));
        Assert.Equal(-52.17m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatOutput));
        Assert.Equal(400m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
        Assert.Equal(revenueBefore, await BalanceAsync("4101"));

        // ‏**ولا ذمّةَ على النزيل بعد** — فالتوريدُ لم يقع، والدفترُ المساعد يقول صفراً.
        Assert.Equal(0m, await SubledgerAsync());
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٨١ · <c>sector-modules#6</c>) <b>وضريبةُ العربون لا تُدفع مرّتين</b> — فاتورةُ المغادرة تحمل ضريبةَ
    /// الإقامة كلِّها (١٥٠)، ومنها ما استُحقّ عند القبض (٥٢٫١٧) فيُردّ من المخرجات عند الإنزال: <b>المخرجاتُ ١٥٠ لا ٢٠٢٫١٧</b>.
    /// </summary>
    [Fact]
    public async Task AtCheckOut_TheDepositsVat_IsNotChargedTwice()
    {
        await SetupAsync();
        var reservation = await BookedReservationAsync("R-V1");
        await new ReservationDepositService(_db).TakeAsync(_companyId, reservation.Id, 400m, Booked, "admin");

        await new FolioService(_db).CheckInAsync(_companyId, reservation.Id);
        await new FolioService(_db).CheckOutAsync(_companyId, reservation.Id, CheckOut);

        Assert.Equal(-150m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatOutput));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers));
        Assert.Equal(750m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));
    }

    /// <summary>
    /// (ح-٨١) <b>وضريبةُ العربون تبلغ إقرارَ شهر قبضه</b> — فبرايرُ يُقرّ ٥٢٫١٧، ومارسُ ما بقي (١٥٠ − ٥٢٫١٧)، والمجموعُ ضريبةُ
    /// الإقامة حرفاً. <b>وكان فبرايرُ صفراً ومارسُ ١٥٠.</b>
    /// </summary>
    [Fact]
    public async Task TheDepositsVat_ReachesTheReturnOfTheMonthItWasReceived()
    {
        await SetupAsync();
        var reservation = await BookedReservationAsync("R-V2");
        await new ReservationDepositService(_db).TakeAsync(_companyId, reservation.Id, 400m, Booked, "admin");
        await new FolioService(_db).CheckInAsync(_companyId, reservation.Id);
        await new FolioService(_db).CheckOutAsync(_companyId, reservation.Id, CheckOut);

        var february = await new VatReturnQuery(_db).ExecuteAsync(_companyId, new(2026, 2, 1), new(2026, 2, 28));
        var march = await new VatReturnQuery(_db).ExecuteAsync(_companyId, new(2026, 3, 1), new(2026, 3, 31));

        Assert.Equal(52.17m, february.NetOutputVat);
        Assert.Equal(97.83m, march.NetOutputVat);
    }

    /// <summary>
    /// (ح-٨١) <b>شاهدُ ضبط — عربونٌ قُبض قبل الإصلاح</b> بلا سطر ضريبة يُنزَل كلُّه كما كان: الذمّةُ ٧٥٠ والمخرجاتُ ضريبةُ الفاتورة.
    /// </summary>
    [Fact]
    public async Task ADepositTakenBeforeTheFix_IsStillAppliedWhole()
    {
        await SetupAsync();
        var reservation = await BookedReservationAsync("R-V3");
        var cash = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var advances = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers);
        var legacy = Brqosh.Domain.Accounting.JournalEntry.CreateDraft(_companyId, Booked, "عربون حجز قديم",
            $"{ReservationDepositService.TakenPrefix}{reservation.Id}");
        legacy.AddLine(cash, 400m, 0m, "قبض");
        legacy.AddLine(advances, 0m, 400m, "التزام");
        legacy.Post("JE-LEGACY-DEP");
        _db.JournalEntries.Add(legacy);
        await _db.SaveChangesAsync();

        await new FolioService(_db).CheckInAsync(_companyId, reservation.Id);
        await new FolioService(_db).CheckOutAsync(_companyId, reservation.Id, CheckOut);

        Assert.Equal(750m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers));
        Assert.Equal(-150m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatOutput));
    }

    /// <summary><b>والتقسيمُ بالهللة</b> — ٤٠٠ ⟵ ٣٤٧٫٨٣ و٥٢٫١٧؛ و١١٥ ⟵ ١٠٠ و١٥.</summary>
    [Fact]
    public void TheDepositSplit_IsVatInclusive_ToTheHalala()
    {
        Assert.Equal((347.83m, 52.17m), ReservationDepositService.SplitVatInclusive(400m));
        Assert.Equal((100m, 15m), ReservationDepositService.SplitVatInclusive(115m));
    }

    /// <summary>
    /// <b>(ح-٢) وعند المغادرة يُنزَل العربونُ — فيعود الالتزامُ صفراً وتنقص الذمّة.</b>
    ///
    /// <para>إقامةُ ليلتين بخمسمئةٍ لكلٍّ ⟵ فاتورةٌ بـ<b>١٬١٥٠</b> بالضريبة، وعربونٌ
    /// أربعُمئة ⟵ <b>الباقي ٧٥٠</b>. <b>والفاتورةُ تصدر بكامل الإقامة</b> — فالعربونُ
    /// سدادٌ مقدَّمٌ من الثمن لا خصمٌ منه، <b>والتوريدُ وقع بكامله</b>.</para>
    /// </summary>
    [Fact]
    public async Task OnCheckOut_TheDepositClearsTheLiability_AndReducesTheGuestsDebt()
    {
        await SetupAsync();
        var reservation = await BookedReservationAsync("R-D2");

        await new ReservationDepositService(_db)
            .TakeAsync(_companyId, reservation.Id, 400m, Booked, "admin");

        await new FolioService(_db).CheckInAsync(_companyId, reservation.Id);
        await new FolioService(_db).CheckOutAsync(_companyId, reservation.Id, CheckOut);

        // ‏**والالتزامُ عاد صفراً** — لا يبقى عربونٌ لنزيلٍ رحل.
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers));

        // ‏**والذمّةُ في الأستاذ ١٬١٥٠ − ٤٠٠**.
        Assert.Equal(750m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));

        // ‏**وهويّةُ هـ-١: الدفترُ المساعد = حسابُ المراقبة.** وبغيرها يُطالَب النزيلُ
        // ‏بما سدّده مقدَّماً — ويظهر في أعمار الديون مديناً بالعربون إلى الأبد.
        Assert.Equal(750m, await SubledgerAsync());
    }

    /// <summary>
    /// <b>وبلا عربونٍ لا يتغيّر شيء — فالمسارُ الجديد لا يمسّ الحجزَ العاديّ.</b>
    ///
    /// <para><b>وهذا ما يجعل التغييرَ آمناً على ما كان يعمل</b>: قيدٌ فارغٌ لكلّ مغادرةٍ
    /// يملأ الأستاذ بضجيج، <b>ويكسر هويّةَ هـ-١٢ بسطرٍ لا مقابلَ له</b>.</para>
    /// </summary>
    [Fact]
    public async Task ACheckOutWithoutADeposit_IsUnchanged()
    {
        await SetupAsync();
        var reservation = await BookedReservationAsync("R-D3");

        await new FolioService(_db).CheckInAsync(_companyId, reservation.Id);
        await new FolioService(_db).CheckOutAsync(_companyId, reservation.Id, CheckOut);

        Assert.Equal(1_150m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));
        Assert.Equal(1_150m, await SubledgerAsync());
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AdvancesFromCustomers));
    }

    /// <summary>
    /// <b>ولا يُقبض عربونٌ على حجزٍ غادر صاحبُه.</b>
    ///
    /// <para><b>فالتصفيةُ وقعت</b>، وعربونٌ بعدها يبقى في <c>2105</c> بلا فاتورةٍ يُنزَل
    /// عليها — <b>التزامٌ خالدٌ تجاه نزيلٍ رحل</b>، يُقرأ في الميزانيّة دَيناً على المنشأة
    /// ولا أحدَ يطالب به.</para>
    /// </summary>
    [Fact]
    public async Task ADeposit_IsRefusedAfterCheckOut()
    {
        await SetupAsync();
        var reservation = await BookedReservationAsync("R-D4");

        await new FolioService(_db).CheckInAsync(_companyId, reservation.Id);
        await new FolioService(_db).CheckOutAsync(_companyId, reservation.Id, CheckOut);

        await Assert.ThrowsAsync<AccountingException>(() =>
            new ReservationDepositService(_db)
                .TakeAsync(_companyId, reservation.Id, 100m, CheckOut, "admin"));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<Reservation> BookedReservationAsync(string number)
    {
        var reservation = Reservation.Create(_companyId, _roomId, _guestId, number,
            CheckIn, CheckOut, 500m);
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();
        return reservation;
    }

    private async Task<decimal> SubledgerAsync() =>
        (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _guestId)).Balance;

    private async Task<decimal> BalanceAsync(string code)
    {
        var accountId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code == code)
            .Select(a => (long?)a.Id)
            .FirstOrDefaultAsync();
        if (accountId is null) return 0m;

        return await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status == JournalEntryStatus.Posted)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountId == accountId)
            .SumAsync(l => (decimal?)(l.Debit - l.Credit)) ?? 0m;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

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
