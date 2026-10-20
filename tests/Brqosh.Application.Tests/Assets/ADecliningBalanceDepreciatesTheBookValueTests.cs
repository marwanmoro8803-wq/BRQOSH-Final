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
/// (جاهزيّةُ الإطلاق · <c>treasury-payroll-assets#11</c>) <b>القسطُ المتناقصُ المضاعَف — على القيمة الدفتريّة، ويتحوّل إلى الخطّ المستقيم.</b>
///
/// <para><b>ما كان:</b> الخطُّ المستقيمُ وحدَه. <b>والأرقامُ محسوبةٌ سلفاً</b>: ١٢٬٠٠٠ على ١٢ شهراً بلا خردة، مقتنى أوّلَ يناير —
/// يناير الأكبرُ من (١٢٬٠٠٠ × ٢ ÷ ١٢ = ٢٬٠٠٠) و(١٢٬٠٠٠ ÷ ١٢ = ١٬٠٠٠) = ٢٬٠٠٠؛ وفبراير ١٠٬٠٠٠ × ٢ ÷ ١٢ = ١٬٦٦٦٫٦٧؛ ويتحوّل إلى
/// الخطّ المستقيم متى قلّ الباقي من العمر عن ستّة أشهر، فيُستنفد ١٢٬٠٠٠ بالضبط آخرَ ديسمبر.</para>
/// </summary>
public class ADecliningBalanceDepreciatesTheBookValueTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public ADecliningBalanceDepreciatesTheBookValueTests()
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
    public async Task TheFirstMonths_DeclineWithTheBookValue()
    {
        var asset = await AssetAsync(DepreciationMethod.DecliningBalance);

        await RunAsync(new DateOnly(2026, 1, 31));
        await RunAsync(new DateOnly(2026, 2, 28));

        var entries = await EntriesAsync(asset);
        Assert.Equal(2_000m, entries.Single(e => e.EntryDate == new DateOnly(2026, 1, 31)).Lines.Sum(l => l.Debit));
        Assert.Equal(1_666.67m, entries.Single(e => e.EntryDate == new DateOnly(2026, 2, 28)).Lines.Sum(l => l.Debit));
    }

    [Fact]
    public async Task OverTheWholeLife_TheDepreciableAmountIsExhaustedExactly()
    {
        var asset = await AssetAsync(DepreciationMethod.DecliningBalance);

        for (var month = 1; month <= 12; month++)
            await RunAsync(new DateOnly(2026, month, DateTime.DaysInMonth(2026, month)));

        Assert.Equal(12_000m, (await ReloadAsync(asset)).AccumulatedDepreciation);
        var december = (await EntriesAsync(asset)).Single(e => e.EntryDate == new DateOnly(2026, 12, 31)).Lines.Sum(l => l.Debit);
        var november = (await EntriesAsync(asset)).Single(e => e.EntryDate == new DateOnly(2026, 11, 30)).Lines.Sum(l => l.Debit);
        Assert.True(Math.Abs(november - december) <= 0.02m, $"بعد التحوّل أقساطٌ مستقيمةٌ متساوية: {november} و{december}");
        Assert.Equal(0, await RunAsync(new DateOnly(2027, 1, 31)));
    }

    [Fact]
    public async Task ACatchUp_EqualsTheSameMonthsRunOneByOne()
    {
        var caughtUp = await AssetAsync(DepreciationMethod.DecliningBalance, "FA-CATCH");
        await RunAsync(new DateOnly(2026, 3, 31));
        var oneShot = (await ReloadAsync(caughtUp)).AccumulatedDepreciation;

        Assert.Equal(2_000m + 1_666.67m + 1_388.89m, oneShot);
    }

    [Fact]
    public async Task AReversedRun_IsDepreciatedAgain_ToTheSameAmount()
    {
        var asset = await AssetAsync(DepreciationMethod.DecliningBalance);
        await RunAsync(new DateOnly(2026, 1, 31));
        await RunAsync(new DateOnly(2026, 2, 28));

        await new DepreciationService(_db).ReverseRunAsync(_companyId, new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 1), "admin");
        Assert.Equal(2_000m, (await ReloadAsync(asset)).AccumulatedDepreciation);

        await RunAsync(new DateOnly(2026, 2, 28));
        Assert.Equal(3_666.67m, (await ReloadAsync(asset)).AccumulatedDepreciation);
    }

    [Fact]
    public async Task TheMethod_CannotChangeOnceDepreciated()
    {
        var asset = await AssetAsync(DepreciationMethod.StraightLine);
        await RunAsync(new DateOnly(2026, 1, 31));

        var reloaded = await ReloadAsync(asset);
        Assert.Throws<AccountingException>(() => reloaded.UseDepreciationMethod(DepreciationMethod.DecliningBalance));
        Assert.Equal(DepreciationMethod.StraightLine, reloaded.Method);
    }

    [Fact]
    public async Task StraightLine_IsUnchanged()
    {
        var asset = await AssetAsync(DepreciationMethod.StraightLine);

        await RunAsync(new DateOnly(2026, 2, 28));

        Assert.Equal(2_000m, (await ReloadAsync(asset)).AccumulatedDepreciation);   // ‏شهران × ١٬٠٠٠
    }

    private async Task<FixedAsset> AssetAsync(DepreciationMethod method, string code = "FA-DDB")
    {
        if (_companyId == 0)
            _companyId = (await new DatabaseInitializer(_db).InitializeAsync("شركة", "310123456700003", BusinessProfile.General)).Id;
        var assetAccount = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1203");
        var accumAccount = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1207");

        var asset = FixedAsset.Create(_companyId, code, "معدات", new DateOnly(2026, 1, 1), 12_000m, 0m, 12,
            assetAccount.Id, accumAccount.Id, method);
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        return asset;
    }

    private Task<int> RunAsync(DateOnly runDate) =>
        new DepreciationService(_db).RunMonthlyAsync(_companyId, runDate, "admin");

    private async Task<FixedAsset> ReloadAsync(FixedAsset asset)
    {
        await _db.Entry(asset).ReloadAsync();
        return asset;
    }

    private Task<List<JournalEntry>> EntriesAsync(FixedAsset asset) =>
        _db.JournalEntries.Include(e => e.Lines)
            .Where(e => e.CompanyId == _companyId && e.SourceDocument == $"Depreciation:{asset.Code}")
            .ToListAsync();
}
