using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Assets;

public class DepreciationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _assetAcctId;
    private long _depAcctId;

    public DepreciationServiceTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync()
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
    }

    [Fact]
    public async Task RunMonthly_DepreciatesActiveAssetsWithBookValueAboveSalvage()
    {
        await SetupAsync();

        var asset = FixedAsset.Create(_companyId, "FA-DEP", "معدات", new DateOnly(2026, 1, 1),
            12_000m, 0m, 12, _assetAcctId, _depAcctId);
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();

        var count = await new DepreciationService(_db).RunMonthlyAsync(
            _companyId, new DateOnly(2026, 2, 28), "admin");

        Assert.Equal(1, count);
        var reloaded = await _db.FixedAssets.SingleAsync(a => a.Id == asset.Id);
        // ‏(جاهزيّةُ الإطلاق · ح-٥٨) ‏**يناير وفبراير — لا فبراير وحدَه.** الأصلُ في الخدمة منذ أوّل يناير ولم يُشغَّل
        // ‏يناير؛ وكان هذا الاختبارُ يُثبت ١٬٠٠٠ — **أي يحرس الشهرَ الضائع** (treasury-payroll-assets#11).
        Assert.Equal(2000m, reloaded.AccumulatedDepreciation);
        Assert.True(await _db.JournalEntries.AnyAsync(e => e.SourceDocument == "Depreciation:FA-DEP"));
    }

    [Fact]
    public async Task RunMonthly_Assigns_unique_journal_numbers_for_multiple_assets()
    {
        await SetupAsync();

        var asset1 = FixedAsset.Create(_companyId, "FA-DEP-1", "معدات 1", new DateOnly(2026, 1, 1),
            12_000m, 0m, 12, _assetAcctId, _depAcctId);
        var asset2 = FixedAsset.Create(_companyId, "FA-DEP-2", "معدات 2", new DateOnly(2026, 1, 1),
            24_000m, 0m, 12, _assetAcctId, _depAcctId);
        _db.FixedAssets.AddRange(asset1, asset2);
        await _db.SaveChangesAsync();

        var count = await new DepreciationService(_db).RunMonthlyAsync(
            _companyId, new DateOnly(2026, 2, 28), "admin");

        Assert.Equal(2, count);
        var numbers = await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.EntryNumber!.StartsWith("JV-2026-"))
            .Select(e => e.EntryNumber!)
            .ToListAsync();
        Assert.Equal(2, numbers.Distinct().Count());
    }
}
