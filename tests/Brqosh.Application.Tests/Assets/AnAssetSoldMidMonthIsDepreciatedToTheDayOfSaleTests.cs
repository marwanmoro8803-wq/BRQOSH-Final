using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Assets;

/// <summary>
/// (قرارُ المالك 2026-10-20 · ملاحظةُ تدقيق Codex) <b>الاستبعادُ يُهلِك ما انقضى قبله — وشهرَ الاستبعاد بأيّامه.</b>
///
/// <para><b>ما كان:</b> الربحُ والخسارةُ يُقاسان على الدفتريّة «وقتَ آخر تشغيل إهلاك»، والإهلاكُ يُشغَّل بزرّ. فأصلٌ ١٢٬٠٠٠
/// يُباع في ٢٨ فبراير قبل تشغيل فبراير يُخرَج بدفتريّة ١٠٬٠٠٠ لا ٨٬٣٣٣٫٣٣، فتُضخَّم الخسارةُ (٢٬٠٠٠ بدل ٣٣٣٫٣٣) ويضيع مصروفُ
/// الإهلاك — <b>وهو يُحسب في قائمة الدخل خطأً</b>.</para>
///
/// <para>الأرقامُ مكتوبةٌ بيدٍ من القاعدة لا من المنفِّذ: ١٢٬٠٠٠ على ١٢ شهراً. <b>المتناقص</b> (يناير ٢٬٠٠٠، فبراير
/// ١٠٬٠٠٠×٢÷١٢ = ١٬٦٦٦٫٦٧). <b>والمستقيم</b> ١٬٠٠٠ شهرياً، وفبراير ٢٨ يوماً فنصفُه (١٤ يوماً) ٥٠٠.</para>
/// </summary>
public class AnAssetSoldMidMonthIsDepreciatedToTheDayOfSaleTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _assetAccountId;
    private long _accumAccountId;

    public AnAssetSoldMidMonthIsDepreciatedToTheDayOfSaleTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";

        // ‏والبيعُ خارجَ نطاق الضريبة (OutOfScope): فلا فاتورةَ إلكترونيّةً — المقيسُ هنا الإهلاكُ وحدَه.
        var company = new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700099", BusinessProfile.General).GetAwaiter().GetResult();
        _companyId = company.Id;
        _assetAccountId = _db.Accounts.Single(a => a.CompanyId == _companyId && a.Code == "1203").Id;
        _accumAccountId = _db.Accounts.Single(a => a.CompanyId == _companyId && a.Code == "1207").Id;
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<FixedAsset> AssetAsync(DepreciationMethod method, DateOnly acquired, decimal cost = 12_000m, int life = 12)
    {
        var asset = FixedAsset.Create(_companyId, "FA-X", "معدّات", acquired, cost, 0m, life,
            _assetAccountId, _accumAccountId, method);
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        return asset;
    }

    private Task<int> RunAsync(DateOnly on) => new DepreciationService(_db).RunMonthlyAsync(_companyId, on, "admin");

    private Task<JournalEntry> DisposeAsync(FixedAsset asset, decimal proceeds, DateOnly on) =>
        new FixedAssetDisposalPostingService(_db).PostDisposalAsync(asset, proceeds, on, "admin", VatCategory.OutOfScope);

    private async Task<decimal> DebitAsync(string code, DateOnly? on = null)
    {
        var accountId = (await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == code)).Id;
        var query = _db.JournalEntries.AsNoTracking().Include(e => e.Lines).Where(e => e.CompanyId == _companyId);
        if (on is { } date)
            query = query.Where(e => e.EntryDate == date);
        return (await query.ToListAsync()).SelectMany(e => e.Lines).Where(l => l.AccountId == accountId).Sum(l => l.Debit);
    }

    [Fact]
    public async Task ADecliningAsset_SoldBeforeTheMonthRun_IsDepreciatedForThatMonthFirst()
    {
        var asset = await AssetAsync(DepreciationMethod.DecliningBalance, new DateOnly(2026, 1, 1));
        await RunAsync(new DateOnly(2026, 1, 31));   // ‏يناير ٢٬٠٠٠ ⟵ الدفتريّة ١٠٬٠٠٠

        await DisposeAsync(asset, 8_000m, new DateOnly(2026, 2, 28));

        Assert.Equal(1_666.67m, await DebitAsync("5211", new DateOnly(2026, 2, 28)));   // ‏استدراكُ فبراير
        Assert.Equal(333.33m, await DebitAsync("5215"));                                // ‏الخسارةُ ٨٬٣٣٣٫٣٣ − ٨٬٠٠٠ لا ٢٬٠٠٠
        Assert.Equal(3_666.67m, await DebitAsync("1207"));                              // ‏المجمَّعُ المُقفَل = يناير + فبراير
    }

    [Fact]
    public async Task AStraightLineAsset_IsDepreciatedOnlyForTheElapsedDaysOfTheSaleMonth()
    {
        var asset = await AssetAsync(DepreciationMethod.StraightLine, new DateOnly(2026, 1, 1));
        await RunAsync(new DateOnly(2026, 1, 31));   // ‏١٬٠٠٠

        await DisposeAsync(asset, 10_500m, new DateOnly(2026, 2, 14));

        Assert.Equal(500m, await DebitAsync("5211", new DateOnly(2026, 2, 14)));   // ‏١٤ من ٢٨ يوماً
        Assert.Equal(1_500m, await DebitAsync("1207"));
        Assert.Equal(0m, await DebitAsync("5215"));                                // ‏دفتريّة ١٠٬٥٠٠ = العائد: لا ربحَ ولا خسارة
    }

    [Fact]
    public async Task AMonthAlreadyDepreciated_IsNotDepreciatedTwice()
    {
        var asset = await AssetAsync(DepreciationMethod.StraightLine, new DateOnly(2026, 1, 1));
        await RunAsync(new DateOnly(2026, 1, 31));
        await RunAsync(new DateOnly(2026, 2, 28));   // ‏فبراير أُهلك كاملاً

        await DisposeAsync(asset, 10_000m, new DateOnly(2026, 2, 28));

        Assert.Equal(2, await _db.JournalEntries.CountAsync(e => e.SourceDocument == "Depreciation:FA-X"));
        Assert.Equal(2_000m, await DebitAsync("1207"));
        Assert.Equal(0m, await DebitAsync("5215"));
    }

    [Fact]
    public async Task ASaleInTheAcquisitionMonth_IsDepreciatedFromTheAcquisitionDayToTheSaleDay()
    {
        // ‏٣٬١٠٠ على ١٠ أشهر = ٣١٠ شهرياً؛ اقتناءٌ ١١ يناير وبيعٌ ٢٠ يناير ⟵ ١٠ أيّامٍ من ٣١ ⟵ ١٠٠٫٠٠
        var asset = await AssetAsync(DepreciationMethod.StraightLine, new DateOnly(2026, 1, 11), cost: 3_100m, life: 10);

        await DisposeAsync(asset, 3_000m, new DateOnly(2026, 1, 20));

        Assert.Equal(100m, await DebitAsync("5211", new DateOnly(2026, 1, 20)));
        Assert.Equal(100m, await DebitAsync("1207"));
        Assert.Equal(0m, await DebitAsync("5215"));
    }

    [Fact]
    public async Task ASaleOnTheLastDayOfTheMonth_TakesTheWholeMonth()
    {
        var asset = await AssetAsync(DepreciationMethod.StraightLine, new DateOnly(2026, 1, 1));

        await DisposeAsync(asset, 10_000m, new DateOnly(2026, 2, 28));

        Assert.Equal(2_000m, await DebitAsync("1207"));   // ‏يناير وفبراير كاملَين
    }
}
