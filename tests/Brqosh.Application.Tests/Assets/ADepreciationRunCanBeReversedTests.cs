using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Assets;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٩٢ · <c>treasury-payroll-assets#10</c>) <b>تشغيلُ الإهلاك يُعكس — آخرُه وحدَه، ولأصلٍ لم يُستبعَد.</b>
///
/// <para>كان لا يُعكس: العكسُ العامُّ يرفض قيداً له مستند (<c>Depreciation:…</c>)، <b>والمجمَّعُ على سجلّ الأصل
/// (<c>AccumulatedDepreciation</c>) لا ينقص بعكس القيد</b> — فتشغيلٌ بتاريخٍ خطأٍ أو بعمرٍ خطأٍ يبقى، وعكسُ قيده باليد (لو أمكن)
/// يترك القيمةَ الدفتريّةَ على السجلّ أقلَّ من الأستاذ.</para>
/// </summary>
public class ADepreciationRunCanBeReversedTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public ADepreciationRunCanBeReversedTests()
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
    /// <b>القياسُ الحاكم: عكسُ آخر تشغيلٍ يُعيد المجمَّعَ على السجلّ والأستاذ صفراً، ويُهلَك الشهرُ من جديد</b> — أصلٌ بـ١٢٬٠٠٠ على ١٢ شهراً
    /// أُهلك يناير…مارس (٣٬٠٠٠) ثمّ عُكس، فتشغيلُ مارس ثانيةً يُحمّل ٣٬٠٠٠ لا صفراً.
    /// </summary>
    [Fact]
    public async Task ReversingTheLatestRun_RestoresTheRecord_AndTheMonthsAreDepreciatedAgain()
    {
        var asset = await AssetAsync();
        await RunAsync(new DateOnly(2026, 3, 31));
        Assert.Equal(3_000m, (await ReloadAsync(asset)).AccumulatedDepreciation);

        var reversed = await new DepreciationService(_db).ReverseRunAsync(
            _companyId, new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 1), "admin");

        Assert.Equal(1, reversed);
        Assert.Equal(0m, (await ReloadAsync(asset)).AccumulatedDepreciation);
        Assert.Equal(0m, await BalanceAsync("5211"));

        Assert.Equal(1, await RunAsync(new DateOnly(2026, 3, 31)));
        Assert.Equal(3_000m, (await ReloadAsync(asset)).AccumulatedDepreciation);
    }

    /// <summary>
    /// <b>وتشغيلٌ سابقٌ لا يُعكس ولاحقُه قائم</b> — فعكسُه يُخرج الشهورَ من وسط السلسلة، ويستدركها التشغيلُ التالي فوق اللاحق.
    /// </summary>
    [Fact]
    public async Task AnEarlierRun_IsRefused_WhileALaterOneStands()
    {
        var asset = await AssetAsync();
        await RunAsync(new DateOnly(2026, 2, 28));
        await RunAsync(new DateOnly(2026, 3, 31));

        await Assert.ThrowsAsync<AccountingException>(() =>
            new DepreciationService(_db).ReverseRunAsync(_companyId, new DateOnly(2026, 2, 28), new DateOnly(2026, 4, 1), "admin"));
        Assert.Equal(3_000m, (await ReloadAsync(asset)).AccumulatedDepreciation);
    }

    /// <summary>
    /// <b>وإهلاكُ أصلٍ استُبعد لا يُعكس</b> — فالاستبعادُ أقفل المجمَّعَ كلَّه، وعكسُ الإهلاك بعده يُعيد مصروفاً على أصلٍ خرج من الدفاتر.
    /// </summary>
    [Fact]
    public async Task ADisposedAssetsDepreciation_IsNotReversed()
    {
        var asset = await AssetAsync();
        await RunAsync(new DateOnly(2026, 3, 31));
        await new FixedAssetDisposalPostingService(_db).PostDisposalAsync(
            asset, saleProceeds: 8_000m, new DateOnly(2026, 4, 15), "admin");

        await Assert.ThrowsAsync<AccountingException>(() =>
            new DepreciationService(_db).ReverseRunAsync(_companyId, new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 16), "admin"));
    }

    /// <summary><b>ولا تشغيلَ قائماً بذلك التاريخ ⟵ رفضٌ باسمه</b> — لا نجاحٌ صامتٌ يوهم أنّ شيئاً عُكس.</summary>
    [Fact]
    public async Task NoRunOnThatDate_IsRefused()
    {
        await AssetAsync();
        await RunAsync(new DateOnly(2026, 3, 31));

        await Assert.ThrowsAsync<AccountingException>(() =>
            new DepreciationService(_db).ReverseRunAsync(_companyId, new DateOnly(2026, 2, 28), new DateOnly(2026, 4, 1), "admin"));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<FixedAsset> AssetAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700077", BusinessProfile.General);
        _companyId = company.Id;
        var assetAcct = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("12") && !a.Code.StartsWith("1207") && !a.IsGroup)
            .Select(a => a.Id).FirstAsync();
        var depAcct = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("1207"))
            .Select(a => a.Id).FirstAsync();

        var asset = FixedAsset.Create(_companyId, "FA-H92", "معدات", new DateOnly(2026, 1, 10), 12_000m, 0m, 12, assetAcct, depAcct);
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        return asset;
    }

    private Task<int> RunAsync(DateOnly runDate) =>
        new DepreciationService(_db).RunMonthlyAsync(_companyId, runDate, "admin");

    private async Task<FixedAsset> ReloadAsync(FixedAsset asset)
    {
        _db.ChangeTracker.Clear();
        return await _db.FixedAssets.AsNoTracking().SingleAsync(a => a.Id == asset.Id);
    }

    private async Task<decimal> BalanceAsync(string code) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == code)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Debit - l.Credit);
}
