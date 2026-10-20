using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Assets;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-٥٨ · <c>treasury-payroll-assets#11</c>) <b>الإهلاكُ يستدرك ما فات — ولا يُهلك ما لم يُقتنَ.</b>
///
/// <para>‏<b>ما كان</b>: <c>RunMonthlyAsync</c> يُحمّل <b>شهراً واحداً</b> على كلّ أصلٍ نشط مهما كان — <b>أصلٌ يُقتنى
/// في أكتوبر يُهلَك في تشغيل سبتمبر</b>، <b>وشهرٌ لم يُشغَّل لا يُستدرَك أبداً</b>: يُزاح عمرُ الأصل ويُبالَغ في قيمته
/// الدفتريّة حتى استبعاده، فتُبالَغ خسارةُ البيع (وهو ما سجّله م3-٩ في <c>EnsureReadyForDisposal</c>).</para>
///
/// <para>‏<b>والإصلاح</b>: تشغيلُ شهرٍ يُحمّل <b>كلَّ شهرٍ مستحقٍّ</b> من آخر شهرٍ أُهلك — أو من شهر الاقتناء — حتى
/// شهر التشغيل، <b>بقيدٍ واحدٍ بتاريخ التشغيل</b> (استدراكٌ في أوّل فترةٍ مفتوحة، مذهبُ SAP وOracle).
/// <b>واتّفاقيّةُ الشهر الكامل</b> لشهر الاقتناء صارت <b>بالأيّام</b> (قرارُ المالك 2026-10-20 — <c>ADepreciationStartsOnItsDayTests</c>)؛
/// فأصولُ هذا الصنف تُقتنى في أوّل الشهر، حيث الشهرُ الأوّلُ كاملٌ بالسياستين، فيبقى القياسُ هنا قياسَ الاستدراك وحدَه.</para>
/// </summary>
public class DepreciationCatchesUpTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _assetAcctId;
    private long _depAcctId;

    public DepreciationCatchesUpTests()
    {
        TestActor.Accountant();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>‏<b>القياسُ الحاكم الأوّل:</b> أصلٌ لم يُقتنَ بعدُ لا يُهلَك.</summary>
    [Fact]
    public async Task AnAssetAcquiredAfterTheRun_IsNotDepreciated()
    {
        var asset = await AssetAsync(new DateOnly(2026, 10, 15), 12_000m);

        var count = await RunAsync(new DateOnly(2026, 9, 30));

        Assert.Equal(0, count);
        Assert.Equal(0m, (await ReloadAsync(asset)).AccumulatedDepreciation);
        Assert.Empty(await EntriesAsync(asset));
    }

    /// <summary>‏<b>القياسُ الحاكم الثاني:</b> شهرٌ لم يُشغَّل يُستدرَك في التشغيل التالي — بقيدٍ واحدٍ بتاريخه.</summary>
    [Fact]
    public async Task AMissedMonth_IsCaughtUpByTheNextRun()
    {
        var asset = await AssetAsync(new DateOnly(2026, 1, 1), 12_000m);   // ‏ألفٌ في الشهر

        await RunAsync(new DateOnly(2026, 1, 31));
        await RunAsync(new DateOnly(2026, 3, 31));                          // ‏فبراير فات

        Assert.Equal(3_000m, (await ReloadAsync(asset)).AccumulatedDepreciation);
        var march = (await EntriesAsync(asset)).Single(e => e.EntryDate == new DateOnly(2026, 3, 31));
        Assert.Equal(2_000m, march.Lines.Sum(l => l.Debit));
    }

    /// <summary>‏<b>وأوّلُ تشغيلٍ يبدأ من شهر الاقتناء</b> — لا من شهر التشغيل.</summary>
    [Fact]
    public async Task TheFirstRun_CoversEveryMonthSinceAcquisition()
    {
        var asset = await AssetAsync(new DateOnly(2026, 1, 1), 12_000m);

        await RunAsync(new DateOnly(2026, 3, 31));

        Assert.Equal(3_000m, (await ReloadAsync(asset)).AccumulatedDepreciation);
    }

    /// <summary>‏<b>والاستدراكُ لا يتجاوز القابلَ للإهلاك</b> — التكلفة ناقصَ الخردة.</summary>
    [Fact]
    public async Task TheCatchUp_StopsAtTheDepreciableBase()
    {
        var asset = await AssetAsync(new DateOnly(2026, 1, 1), 1_200m, salvage: 200m);   // ‏ألفٌ على ١٢ شهراً = ٨٣٫٣٣

        await RunAsync(new DateOnly(2027, 6, 30));   // ‏ثمانيةَ عشرَ شهراً مستحقّة = ١٬٥٠٠ لولا الحدّ

        Assert.Equal(1_000m, (await ReloadAsync(asset)).AccumulatedDepreciation);
    }

    /// <summary>‏<b>شاهدُ ضبط</b> — التشغيلُ الشهريُّ المنتظم يُحمّل قسطاً واحداً كلَّ شهر، كما كان.</summary>
    [Fact]
    public async Task ConsecutiveRuns_ChargeOneMonthEach()
    {
        var asset = await AssetAsync(new DateOnly(2026, 1, 1), 12_000m);

        await RunAsync(new DateOnly(2026, 1, 31));
        await RunAsync(new DateOnly(2026, 2, 28));

        Assert.Equal(2_000m, (await ReloadAsync(asset)).AccumulatedDepreciation);
        Assert.All(await EntriesAsync(asset), e => Assert.Equal(1_000m, e.Lines.Sum(l => l.Debit)));
    }

    /// <summary>‏<b>ولا يُحمَّل شهرٌ مرّتين</b> — لا بتكرار التشغيل ولا بتشغيل شهرٍ سابقٍ بعد لاحقه.</summary>
    [Fact]
    public async Task NoMonth_IsChargedTwice()
    {
        var asset = await AssetAsync(new DateOnly(2026, 1, 1), 12_000m);

        await RunAsync(new DateOnly(2026, 3, 31));
        var again = await RunAsync(new DateOnly(2026, 3, 31));
        var earlier = await RunAsync(new DateOnly(2026, 2, 28));

        Assert.Equal(0, again);
        Assert.Equal(0, earlier);
        Assert.Equal(3_000m, (await ReloadAsync(asset)).AccumulatedDepreciation);
    }

    /// <summary>‏<b>والحسابُ نفسُه صافٍ</b> — الأشهرُ المستحقّةُ من آخر شهرٍ أُهلك أو من شهر الاقتناء.</summary>
    [Theory]
    [InlineData("2026-01-10", null, "2026-01-31", 1)]          // ‏شهرُ الاقتناء كاملاً
    [InlineData("2026-10-15", null, "2026-09-30", 0)]          // ‏لم يُقتنَ بعد
    [InlineData("2026-01-10", "2026-01-31", "2026-03-31", 2)]  // ‏فبراير فات
    [InlineData("2026-01-10", "2026-03-31", "2026-02-28", 0)]  // ‏شهرٌ سابقٌ بعد لاحقه
    [InlineData("2025-12-31", null, "2026-01-31", 2)]          // ‏يعبر السنة
    public void MonthsDue_CountsFromTheLastDepreciatedMonth(
        string acquired, string? last, string run, int expected)
    {
        var asset = FixedAsset.Create(1, "FA", "أصل", DateOnly.Parse(acquired), 1_200m, 0m, 12, 1, 2);

        Assert.Equal(expected, asset.MonthsDue(last is null ? null : DateOnly.Parse(last), DateOnly.Parse(run)));
    }

    private async Task<FixedAsset> AssetAsync(DateOnly acquired, decimal cost, decimal salvage = 0m)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
        _assetAcctId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("12") && !a.Code.StartsWith("1207") && !a.IsGroup)
            .Select(a => a.Id).FirstAsync();
        _depAcctId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("1207"))
            .Select(a => a.Id).FirstAsync();

        var asset = FixedAsset.Create(_companyId, "FA-H58", "معدات", acquired, cost, salvage, 12, _assetAcctId, _depAcctId);
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        return asset;
    }

    private Task<int> RunAsync(DateOnly runDate) =>
        new DepreciationService(_db).RunMonthlyAsync(_companyId, runDate, "admin");

    private Task<FixedAsset> ReloadAsync(FixedAsset asset) =>
        _db.FixedAssets.SingleAsync(a => a.Id == asset.Id);

    private Task<List<JournalEntry>> EntriesAsync(FixedAsset asset) =>
        _db.JournalEntries.Include(e => e.Lines)
            .Where(e => e.CompanyId == _companyId && e.SourceDocument == $"Depreciation:{asset.Code}")
            .ToListAsync();
}
