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
/// (جاهزيّةُ الإطلاق · ح-٨٢ · <c>sector-modules#4</c>) <b>ليالي الإقامة تبلغ الأستاذَ في شهرها — وكانت تُقيَّد في حساب النزيل
/// وحدَه.</b>
///
/// <para>نزيلٌ يصل ٢٩ ديسمبر ويغادر ٣ يناير: <b>ثلاثُ ليالٍ لديسمبر وليلتان ليناير</b>. وكان «تقييدُ الليالي» يُضيفها إلى حساب
/// النزيل ولا يُرحّل شيئاً — <b>فتُقفل السنةُ ناقصةً ١٬٥٠٠ ويناير يحمل ٢٬٥٠٠</b>.</para>
/// </summary>
public class InHouseNightsReachTheLedgerInTheirMonthTests : IDisposable
{
    private const string Revenue = "4101";
    private const string Accrued = "1118";

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _guestId;
    private long _roomId;

    public InHouseNightsReachTheLedgerInTheirMonthTests()
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
    /// <b>القياسُ الحاكم: ليالي ديسمبر إيرادُ ديسمبر.</b> التشغيلةُ في ٣١ ديسمبر تُقيّد ثلاثَ ليالٍ — <b>فإيرادُ ديسمبر ١٬٥٠٠ ومستحقُّها
    /// غيرُ المفوتر ١٬٥٠٠</b>، ولا فاتورةَ بعد.
    /// </summary>
    [Fact]
    public async Task TheNightsOfDecember_AreDecembersRevenue()
    {
        await SetupAsync();
        await CheckedInAsync("R-1", new DateOnly(2026, 12, 29), new DateOnly(2027, 1, 3));

        var result = await new FolioService(_db).AccrueNightsAsync(_companyId, new DateOnly(2026, 12, 31));

        Assert.True(result.NightsPosted == 3, $"قُيّد {result.NightsPosted}. وتعذّر: {string.Join(" · ", result.Refusals)}");
        Assert.Equal(1_500m, await RevenueAsync(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)));
        Assert.Equal(1_500m, await DebitBalanceAsync(Accrued, new DateOnly(2026, 12, 31)));
    }

    /// <summary>
    /// <b>والمغادرةُ تُطلق المستحقّ فلا تُحسب ليلةٌ مرّتين.</b> فاتورةُ المغادرة تحمل الليالي الخمس (٢٬٥٠٠) — <b>فيناير ١٬٠٠٠ لا
    /// ٢٬٥٠٠</b>، والمجموعُ ٢٬٥٠٠ لا ٤٬٠٠٠، والمستحقُّ صفر؛ <b>والضريبةُ في الفاتورة كما هي</b>.
    /// </summary>
    [Fact]
    public async Task CheckingOut_ReleasesTheAccrual_SoNoNightIsCountedTwice()
    {
        await SetupAsync();
        var reservation = await CheckedInAsync("R-2", new DateOnly(2026, 12, 29), new DateOnly(2027, 1, 3));
        var service = new FolioService(_db);

        await service.AccrueNightsAsync(_companyId, new DateOnly(2026, 12, 31));
        await service.CheckOutAsync(_companyId, reservation.Id, new DateOnly(2027, 1, 3));

        Assert.Equal(1_500m, await RevenueAsync(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)));
        Assert.Equal(1_000m, await RevenueAsync(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31)));
        Assert.Equal(0m, await DebitBalanceAsync(Accrued, new DateOnly(2027, 1, 31)));
        Assert.Equal(-375m, await DebitBalanceAsync(SaudiChartOfAccounts.SystemCodes.VatOutput, new DateOnly(2027, 1, 31)));
    }

    /// <summary>
    /// <b>وتشغيلةٌ متأخّرةٌ تضع كلَّ ليلةٍ في شهرها.</b> لم يُضغط الزرُّ إلّا في ٢ يناير: ليلتا ٣٠ و٣١ ديسمبر لديسمبر، وليلتا ١ و٢
    /// يناير ليناير — <b>لا الأربعُ بتاريخ التشغيلة</b>.
    /// </summary>
    [Fact]
    public async Task ALateRun_PutsEachNightInItsOwnMonth()
    {
        await SetupAsync();
        await CheckedInAsync("R-3", new DateOnly(2026, 12, 30), new DateOnly(2027, 1, 4));

        await new FolioService(_db).AccrueNightsAsync(_companyId, new DateOnly(2027, 1, 2));

        Assert.Equal(1_000m, await RevenueAsync(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)));
        Assert.Equal(1_000m, await RevenueAsync(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31)));
    }

    /// <summary>
    /// <b>وتشغيلٌ مرّتين لا يستحقّ الليلةَ مرّتين</b> — والدفعةُ تُرحّل ما أضافته للتوّ لا كلَّ ما في الحساب. <b>وفندقٌ يُشغّل الزرَّ
    /// صباحاً ومساءً يُضاعف إيرادَه في الأستاذ</b> وإن بقي حسابُ النزيل صحيحاً.
    /// </summary>
    [Fact]
    public async Task RunningTwice_AccruesEachNightOnce()
    {
        await SetupAsync();
        await CheckedInAsync("R-4", new DateOnly(2026, 12, 29), new DateOnly(2027, 1, 3));
        var service = new FolioService(_db);

        await service.AccrueNightsAsync(_companyId, new DateOnly(2026, 12, 30));
        await service.AccrueNightsAsync(_companyId, new DateOnly(2026, 12, 31));

        Assert.Equal(1_500m, await RevenueAsync(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)));
        Assert.Equal(1_500m, await DebitBalanceAsync(Accrued, new DateOnly(2026, 12, 31)));
    }

    /// <summary>
    /// <b>شاهدُ ضبط: مغادرةٌ بلا تشغيلةٍ لا تُطلق شيئاً.</b> الفاتورةُ تحمل الليالي كلَّها كما كانت — <b>والإطلاقُ من الأستاذ لا من
    /// الليالي</b>، فلا يُطلَق ما لم يُستحقّ فيصير المستحقُّ سالباً والإيرادُ ناقصاً.
    /// </summary>
    [Fact]
    public async Task ACheckOutWithoutAnyRun_ReleasesNothing()
    {
        await SetupAsync();
        var reservation = await CheckedInAsync("R-5", new DateOnly(2026, 12, 29), new DateOnly(2027, 1, 3));

        await new FolioService(_db).CheckOutAsync(_companyId, reservation.Id, new DateOnly(2027, 1, 3));

        Assert.Equal(0m, await RevenueAsync(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)));
        Assert.Equal(2_500m, await RevenueAsync(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31)));
        Assert.Equal(0m, await DebitBalanceAsync(Accrued, new DateOnly(2027, 1, 31)));
        Assert.False(await _db.JournalEntries.AnyAsync(e => e.SourceDocument != null
                                                            && e.SourceDocument.StartsWith("RoomNightAccrual")));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<decimal> RevenueAsync(DateOnly from, DateOnly to) =>
        -(await BalanceAsync(Revenue, from, to));

    private async Task<decimal> DebitBalanceAsync(string code, DateOnly asOf) =>
        await BalanceAsync(code, DateOnly.MinValue, asOf);

    private async Task<decimal> BalanceAsync(string code, DateOnly from, DateOnly to) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft
                        && e.EntryDate >= from && e.EntryDate <= to)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == code)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Debit - l.Credit);

    private async Task<Reservation> CheckedInAsync(string number, DateOnly checkIn, DateOnly checkOut)
    {
        var reservation = Reservation.Create(_companyId, _roomId, _guestId, number, checkIn, checkOut, 500m);
        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();

        await new FolioService(_db).CheckInAsync(_companyId, reservation.Id);
        return reservation;
    }

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
