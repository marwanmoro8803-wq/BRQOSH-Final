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
/// (جاهزيّةُ الإطلاق · <c>treasury-payroll-assets#11</c> · قرارُ المالك 2026-10-20) <b>الإهلاكُ يبدأ من يوم الاقتناء لا من أوّل شهره.</b>
///
/// <para><b>ما كان:</b> اتّفاقيّةُ الشهر الكامل — أصلٌ يُقتنى في العاشر يُحمَّل شهرَه كلَّه. واختار المالكُ <b>النسبةَ بالأيّام</b>:
/// شهرُ الاقتناء يُحمَّل بأيّامه الباقية، والنقصُ يأخذه الشهرُ الأخيرُ فيبلغ المجموعُ القابلَ للإهلاك بالضبط.</para>
///
/// <para>الأرقام: ١٢٬٠٠٠ على ١٢ شهراً = ١٬٠٠٠ شهريّاً؛ والاقتناءُ في ١٠ يناير يُبقي ٢٢ يوماً من ٣١ ⟵ ٧٠٩٫٦٨.</para>
/// </summary>
public class ADepreciationStartsOnItsDayTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public ADepreciationStartsOnItsDayTests()
    {
        TestActor.Accountant();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task TheAcquisitionMonth_IsChargedForItsRemainingDays()
    {
        var asset = await AssetAsync(new DateOnly(2026, 1, 10));

        await RunAsync(new DateOnly(2026, 1, 31));

        Assert.Equal(709.68m, (await ReloadAsync(asset)).AccumulatedDepreciation);
    }

    [Fact]
    public async Task AnAssetAcquiredOnTheFirst_StillGetsAFullMonth()
    {
        var asset = await AssetAsync(new DateOnly(2026, 2, 1));

        await RunAsync(new DateOnly(2026, 2, 28));

        Assert.Equal(1_000m, (await ReloadAsync(asset)).AccumulatedDepreciation);
    }

    [Fact]
    public async Task AFollowingMonth_IsFull()
    {
        var asset = await AssetAsync(new DateOnly(2026, 1, 10));

        await RunAsync(new DateOnly(2026, 1, 31));
        await RunAsync(new DateOnly(2026, 2, 28));

        var february = (await EntriesAsync(asset)).Single(e => e.EntryDate == new DateOnly(2026, 2, 28));
        Assert.Equal(1_000m, february.Lines.Sum(l => l.Debit));
    }

    /// <summary>والاستدراكُ الذي يشمل شهرَ الاقتناء يُنسّبه كذلك — ثلاثةُ أشهرٍ أوّلُها بأيّامه.</summary>
    [Fact]
    public async Task ACatchUpThatIncludesTheAcquisitionMonth_ProratesIt()
    {
        var asset = await AssetAsync(new DateOnly(2026, 1, 10));

        await RunAsync(new DateOnly(2026, 3, 31));

        Assert.Equal(2_709.68m, (await ReloadAsync(asset)).AccumulatedDepreciation);
    }

    /// <summary><b>والنقصُ لا يضيع</b>: بعد الأشهر الاثني عشر يأخذ الشهرُ الثالثَ عشرَ الباقيَ، فيبلغ المجموعُ ١٢٬٠٠٠ بالضبط.</summary>
    [Fact]
    public async Task TheShortfall_IsChargedInTheLastMonth_SoTheWholeBaseIsDepreciated()
    {
        var asset = await AssetAsync(new DateOnly(2026, 1, 10));

        await RunAsync(new DateOnly(2026, 12, 31));
        Assert.Equal(11_709.68m, (await ReloadAsync(asset)).AccumulatedDepreciation);

        await RunAsync(new DateOnly(2027, 1, 31));
        Assert.Equal(12_000m, (await ReloadAsync(asset)).AccumulatedDepreciation);
        var last = (await EntriesAsync(asset)).Single(e => e.EntryDate == new DateOnly(2027, 1, 31));
        Assert.Equal(290.32m, last.Lines.Sum(l => l.Debit));
    }

    private async Task<FixedAsset> AssetAsync(DateOnly acquired)
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
        var assetAccount = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1203");
        var accumAccount = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1207");

        var asset = FixedAsset.Create(_companyId, "FA-DAY", "معدات", acquired, 12_000m, 0m, 12, assetAccount.Id, accumAccount.Id);
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
