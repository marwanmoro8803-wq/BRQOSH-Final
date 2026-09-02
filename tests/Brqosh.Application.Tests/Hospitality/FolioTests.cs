using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Hospitality;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Hospitality;

/// <summary>
/// (CEEF-230 · ط3-٣ب) <b>الإقامة — الليالي تُقيَّد ليلةً بليلة، والحساب يُصفّى مرّةً.</b>
///
/// <para><b>وإيرادُ الغرفة يُستحقّ بمرور الليلة لا بالوصول</b>: نزيلٌ يصل في ٢٨ ديسمبر
/// لعشر ليالٍ، <b>وقيدُ إيرادِ العشر يومَ وصوله يُدخل سبعَ ليالٍ من إيراد يناير في
/// ديسمبر</b> — فتُقفل سنةٌ بإيرادٍ ليس لها. وفي فندقٍ ممتلئٍ آخرَ السنة الفرقُ ليس
/// بنداً بل قائمةً كاملة.</para>
///
/// <para><b>وهويّةُ هـ-١٢:</b> مجموعُ رسوم الحساب = إجماليُّ فاتورة التصفية.</para>
/// </summary>
public class FolioTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _guestId;
    private long _roomId;

    public FolioTests()
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

    // ── الليالي ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: الوصولُ الأوّلَ والمغادرةُ الثالثَ ليلتان لا ثلاث.</b>
    ///
    /// <para><b>وهو أشيعُ خطأٍ في هذا القطاع</b>: حسابُ الفرق شاملاً يُضيف ليلةً كاملةً
    /// إلى <b>كلّ</b> فاتورةٍ في الفندق. ويراها النزيلُ عند الحساب فيُنازع، فيَخصمها
    /// الموظّفُ يدوياً — <b>فيختلّ الإيرادُ عن الإشغال ولا يُطابَق أحدُهما بالآخر أبداً</b>.</para>
    /// </summary>
    [Theory]
    [InlineData("2026-03-01", "2026-03-03", 2)]
    [InlineData("2026-03-01", "2026-03-02", 1)]
    [InlineData("2026-12-28", "2027-01-07", 10)]
    public void TheNightCount_ExcludesTheCheckOutDay(string checkIn, string checkOut, int expected)
    {
        var reservation = Reservation.Create(1L, 1L, 1L, "R-1",
            DateOnly.Parse(checkIn), DateOnly.Parse(checkOut), 500m);

        Assert.Equal(expected, reservation.Nights);
    }

    /// <summary>
    /// <b>وحجزٌ يصل يومَ مغادرة سابقه لا يتداخل معه.</b>
    ///
    /// <para><b>وهذا الحدُّ يُخالف عقدَ الإيجار قصداً</b>: في <c>ط3-١</c> يومُ الانتهاء
    /// <b>داخلٌ</b> — المستأجرُ يسكن فيه؛ وهنا <b>خارج</b> — الغرفةُ تُخلى صباحاً وتُسلَّم
    /// مساءً لغيره. <b>وتوحيدُ الحدَّين يُنتج عطلاً في أحد القطاعين</b>: إمّا تأجيرُ
    /// المؤجَّر، وإمّا تعطيلُ غرفةٍ ليلةً كاملةً في فندقٍ بمئة غرفة.</para>
    /// </summary>
    [Fact]
    public void AReservationArrivingOnTheDayTheLastOneLeaves_DoesNotOverlap()
    {
        var staying = Reservation.Create(1L, 1L, 1L, "R-2",
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 3), 500m);

        Assert.False(staying.OverlapsWith(new DateOnly(2026, 3, 3), new DateOnly(2026, 3, 5)));
        Assert.True(staying.OverlapsWith(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 5)));
    }

    /// <summary>
    /// <b>والدفعةُ تُقيّد ما مضى من الليالي لا ما بقي.</b>
    ///
    /// <para>نزيلٌ نزل الأوّلَ ويغادر الخامس، والتشغيلةُ في الثالث: <b>ثلاثُ ليالٍ</b>
    /// (الأوّل والثاني والثالث). <b>وتقييدُ الخمس يُدخل إيرادَ ليلتين لم تُقدَّما بعد</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheAccrual_PostsOnlyTheNightsThatHavePassed()
    {
        await SetupAsync();
        var reservation = await CheckedInAsync("R-3", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5));

        var result = await new FolioService(_db).AccrueNightsAsync(_companyId, new DateOnly(2026, 3, 3));

        Assert.True(result.NightsPosted == 3,
            $"قُيّد {result.NightsPosted}. وتعذّر: {string.Join(" · ", result.Refusals)}");
        Assert.Equal(1_500m, (await FolioAsync(reservation.Id)).Total);
    }

    /// <summary>
    /// <b>وتشغيلٌ مرّتين لا يُقيّد الليلةَ مرّتين.</b>
    ///
    /// <para>والحارسُ الأخير <b>قيدُ تفرّدٍ في القاعدة</b> على (الحساب، ليلةِ الغرفة) —
    /// لا فحصٌ في الذاكرة يسبقه تشغيلٌ آخر. <b>وفندقٌ يُشغّل الدفعةَ صباحاً ومساءً
    /// يُضاعف إيرادَه في الدفاتر</b>.</para>
    /// </summary>
    [Fact]
    public async Task RunningTheAccrualTwice_DoesNotChargeTheNightTwice()
    {
        await SetupAsync();
        var reservation = await CheckedInAsync("R-4", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5));
        var service = new FolioService(_db);

        var first = await service.AccrueNightsAsync(_companyId, new DateOnly(2026, 3, 3));
        var second = await service.AccrueNightsAsync(_companyId, new DateOnly(2026, 3, 3));

        Assert.Equal(3, first.NightsPosted);
        Assert.Equal(0, second.NightsPosted);
        Assert.Equal(1_500m, (await FolioAsync(reservation.Id)).Total);

        // ‏**وتشغيلةٌ لا عملَ لها تصمت** — لا تعتذر. وثلاثةُ حرّاسٍ هنا: تخطّي المقيَّد
        // ‏في الدفعة، ورفضُ `GuestFolio.AddRoomNight`، وقيدُ التفرّد في القاعدة.
        // ‏**ونزعُ الأوّل وحدَه لا يُخطئ الأرقام** — يُبقيها على حالها ويُخرج تعذُّراً
        // ‏لكلّ حجزٍ في كلّ تشغيلة. **وتشغيلةٌ تعتذر كلَّ صباحٍ عن لا شيءٍ تُعلَّم أن
        // ‏تُهمَل**، فيُهمَل معها التعذُّرُ الحقيقيّ يومَ يقع. فالصمتُ مقيسٌ لا مفترَض.
        Assert.Empty(second.Refusals);
    }

    // ── التصفية ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>وهويّةُ هـ-١٢: مجموعُ الرسوم = إجماليُّ الفاتورة.</b>
    ///
    /// <para><b>ورسمٌ يسقط بين الحساب والفاتورة إيرادٌ قُدِّم ولم يُطالَب به</b> — والنزيلُ
    /// غادر ولا سبيلَ إلى تحصيله.</para>
    /// </summary>
    [Fact]
    public async Task TheSettlementInvoice_CarriesEveryChargeOnTheFolio()
    {
        await SetupAsync();
        var reservation = await CheckedInAsync("R-5", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 4));
        var service = new FolioService(_db);

        var folio = await FolioAsync(reservation.Id);
        await service.AddChargeAsync(_companyId, folio.Id, new DateOnly(2026, 3, 2),
            FolioChargeKind.Restaurant, "عشاء", 220m);
        await service.AddChargeAsync(_companyId, folio.Id, new DateOnly(2026, 3, 2),
            FolioChargeKind.Minibar, "ثلاجة", 45m);

        var invoiceId = await service.CheckOutAsync(_companyId, reservation.Id, new DateOnly(2026, 3, 4));

        var invoice = await _db.SalesInvoices.AsNoTracking()
            .SingleAsync(i => i.Id == invoiceId);

        // ‏ثلاثُ ليالٍ بخمسمئة + 220 + 45 = 1,765.
        Assert.Equal(1_765m, invoice.Subtotal);
        Assert.Equal(5, invoice.Lines.Count);
    }

    /// <summary>
    /// <b>والمغادرةُ تُقيّد ما بقي من الليالي قبل التصفية.</b>
    ///
    /// <para><b>ومغادرةٌ قبل تشغيل الدفعة تُصفّي حساباً ناقصاً ليالٍ</b> — وإيرادٌ يضيع
    /// بلا أثرٍ يدلّ عليه، فالحسابُ أُغلق والغرفةُ أُخليت.</para>
    /// </summary>
    [Fact]
    public async Task CheckingOutBeforeTheBatchRuns_StillBillsEveryNight()
    {
        await SetupAsync();
        var reservation = await CheckedInAsync("R-6", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 4));

        var invoiceId = await new FolioService(_db)
            .CheckOutAsync(_companyId, reservation.Id, new DateOnly(2026, 3, 4));

        var invoice = await _db.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
        Assert.Equal(1_500m, invoice.Subtotal);
    }

    /// <summary>
    /// <b>وحسابٌ مُصفّى لا يُضاف إليه.</b>
    ///
    /// <para>والنزيلُ غادر وفاتورتُه صدرت — <b>ورسمٌ يُضاف بعدها يبقى بلا مستند</b>،
    /// فينفصل مجموعُ الحساب عن الفاتورة وتنكسر <c>هـ-١٢</c>.</para>
    /// </summary>
    [Fact]
    public async Task ASettledFolio_RefusesFurtherCharges()
    {
        await SetupAsync();
        var reservation = await CheckedInAsync("R-7", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 3));
        var service = new FolioService(_db);
        var folio = await FolioAsync(reservation.Id);

        await service.CheckOutAsync(_companyId, reservation.Id, new DateOnly(2026, 3, 3));

        await Assert.ThrowsAsync<AccountingException>(
            () => service.AddChargeAsync(_companyId, folio.Id, new DateOnly(2026, 3, 3),
                FolioChargeKind.Laundry, "غسيل", 60m));
    }

    /// <summary>
    /// <b>والمغادرةُ تُخلي الغرفة.</b>
    ///
    /// <para>وغرفةٌ تبقى مشغولةً بعد مغادرة نزيلها <b>تُعطَّل إلى الأبد</b> — لا تُعرض
    /// ولا تُباع، ويظهر الإشغالُ في التقارير أعلى ممّا هو.</para>
    /// </summary>
    [Fact]
    public async Task CheckingOut_ReleasesTheRoom()
    {
        await SetupAsync();
        var reservation = await CheckedInAsync("R-8", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 3));

        Assert.Equal(RoomStatus.Occupied,
            (await _db.Rooms.AsNoTracking().SingleAsync(r => r.Id == _roomId)).Status);

        await new FolioService(_db).CheckOutAsync(_companyId, reservation.Id, new DateOnly(2026, 3, 3));

        Assert.Equal(RoomStatus.Vacant,
            (await _db.Rooms.AsNoTracking().SingleAsync(r => r.Id == _roomId)).Status);
    }

    /// <summary>
    /// <b>ولا يُنزَل نزيلٌ في غرفةٍ نزيلُها فيها.</b>
    ///
    /// <para>وهو نظيرُ «تأجير المؤجَّر» في <c>ط3-١</c> — <b>ضيفان يفتحان بابَ غرفةٍ
    /// واحدةٍ بمفتاحين</b>.</para>
    /// </summary>
    [Fact]
    public async Task ARoomWithAGuestInIt_CannotTakeAnother()
    {
        await SetupAsync();
        await CheckedInAsync("R-9", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 10));

        var second = Reservation.Create(_companyId, _roomId, _guestId, "R-10",
            new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 12), 500m);
        _db.Reservations.Add(second);
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new FolioService(_db).CheckInAsync(_companyId, second.Id));

        Assert.Contains("R-9", error.Message, StringComparison.Ordinal);
    }

    /// <summary><b>وغرفةٌ في الصيانة لا يُنزَل فيها أحد.</b></summary>
    [Fact]
    public async Task ARoomUnderMaintenance_CannotBeCheckedInto()
    {
        await SetupAsync();
        var room = await _db.Rooms.SingleAsync(r => r.Id == _roomId);
        room.EnterMaintenance();
        await _db.SaveChangesAsync();

        var reservation = Reservation.Create(_companyId, _roomId, _guestId, "R-11",
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 3), 500m);
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<AccountingException>(
            () => new FolioService(_db).CheckInAsync(_companyId, reservation.Id));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<GuestFolio> FolioAsync(long reservationId) =>
        await _db.GuestFolios.SingleAsync(f => f.ReservationId == reservationId);

    private async Task<Reservation> CheckedInAsync(string number, DateOnly checkIn, DateOnly checkOut)
    {
        var reservation = Reservation.Create(_companyId, _roomId, _guestId, number,
            checkIn, checkOut, 500m);
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();

        await new FolioService(_db).CheckInAsync(_companyId, reservation.Id);
        return reservation;
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
