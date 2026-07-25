using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Assets;

public class FixedAssetAcquisitionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _assetAcctId;
    private long _depAcctId;

    public FixedAssetAcquisitionTests()
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

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;

        _assetAcctId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("12") && !a.Code.StartsWith("1207") && !a.IsGroup)
            .Select(a => a.Id).FirstAsync();
        _depAcctId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("1207"))
            .Select(a => a.Id).FirstAsync();
    }

    [Fact]
    public async Task Acquisition_PostsDebitAssetCreditCash()
    {
        await SetupAsync();

        var asset = FixedAsset.Create(_companyId, "FA-001", "سيارة", new DateOnly(2026, 7, 1),
            50_000m, 5_000m, 60, _assetAcctId, _depAcctId);
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();

        var entry = await new FixedAssetAcquisitionPostingService(_db).PostAcquisitionAsync(asset);

        Assert.NotNull(asset.AcquisitionJournalEntryId);
        Assert.True(entry.IsBalanced);
        Assert.Equal(50_000m, entry.Lines.Single(l => l.AccountId == _assetAcctId).Debit);
        Assert.Equal(50_000m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CashOnHand).Credit);
    }
}
