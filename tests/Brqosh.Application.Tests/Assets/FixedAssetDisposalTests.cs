using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Assets;

public class FixedAssetDisposalTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _assetAcctId;
    private long _depAcctId;

    public FixedAssetDisposalTests()
    {
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

    private async Task<FixedAsset> SetupAssetAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        _assetAcctId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("12") && !a.Code.StartsWith("1207") && !a.IsGroup)
            .Select(a => a.Id).FirstAsync();
        _depAcctId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("1207"))
            .Select(a => a.Id).FirstAsync();

        var asset = FixedAsset.Create(_companyId, "FA-DSP", "معدات", new DateOnly(2026, 7, 1),
            10_000m, 10_000m, 12, _assetAcctId, _depAcctId);
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        await new FixedAssetAcquisitionPostingService(_db).PostAcquisitionAsync(asset);
        return asset;
    }

    [Fact]
    public async Task Disposal_PostsBalancedEntry_WithGain()
    {
        var asset = await SetupAssetAsync();

        var entry = await new FixedAssetDisposalPostingService(_db).PostDisposalAsync(
            asset, 15_000m, new DateOnly(2026, 7, 15), "admin");

        Assert.NotNull(asset.DisposalJournalEntryId);
        Assert.False(asset.IsActive);
        Assert.True(entry.IsBalanced);
        Assert.Equal(15_000m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CashOnHand).Debit);
        Assert.Equal(5_000m, entry.Lines.Single(l => l.AccountCode == "4106").Credit);
        Assert.Equal(10_000m, entry.Lines.Single(l => l.AccountId == _assetAcctId).Credit);
    }
}
