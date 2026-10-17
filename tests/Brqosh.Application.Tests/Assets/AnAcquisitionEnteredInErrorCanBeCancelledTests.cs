using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Assets;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥٧ · <c>treasury-payroll-assets#10</c>) <b>اقتناءٌ أُدخل خطأً يُلغى بعكس قيده — ولا يُلغى ما مُسّ بعده.</b>
///
/// <para><b>ما كان:</b> قيدُ الاقتناء مرتبطٌ بمستندٍ فيرفض العكسُ المباشرُ عكسَه، ولا مسارَ في الأصل نفسِه — فأصلٌ بتكلفةٍ خطأٍ
/// يبقى في السجلّ ويُهلَك كلَّ شهرٍ أبداً.</para>
/// </summary>
public class AnAcquisitionEnteredInErrorCanBeCancelledTests : IDisposable
{
    private static readonly DateOnly Acquired = new(2026, 7, 1);

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _assetAcctId;
    private long _depAcctId;

    public AnAcquisitionEnteredInErrorCanBeCancelledTests()
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

    [Fact]
    public async Task AFreshAcquisition_IsReversed_AndTheAssetLeavesTheRegister()
    {
        await SetupAsync();
        var asset = await AcquireAsync("FA-1");

        await CancelAsync(asset);

        var acquisition = await _db.JournalEntries.SingleAsync(e => e.Id == asset.AcquisitionJournalEntryId);
        Assert.Equal(JournalEntryStatus.Reversed, acquisition.Status);
        Assert.Equal(0m, await AssetAccountBalanceAsync());

        _db.ChangeTracker.Clear();
        var reloaded = await _db.FixedAssets.SingleAsync(a => a.Id == asset.Id);
        Assert.False(reloaded.IsActive);
        Assert.Equal(Acquired.AddDays(3), reloaded.CancelledOn);
        Assert.Null(reloaded.DisposalJournalEntryId);

        // ‏ولا يُهلَك بعد إلغائه.
        Assert.Equal(0, await new DepreciationService(_db).RunMonthlyAsync(_companyId, Acquired.AddMonths(2), "admin"));
    }

    /// <summary><b>المُهلَكُ يُرفض حتى يُعكس إهلاكُه</b> — وإلا بقي مجمّعٌ ومصروفٌ على أصلٍ لا وجود له.</summary>
    [Fact]
    public async Task ADepreciatedAsset_IsRefused_UntilItsDepreciationIsReversed()
    {
        await SetupAsync();
        var asset = await AcquireAsync("FA-2");
        var run = Acquired.AddMonths(1);
        await new DepreciationService(_db).RunMonthlyAsync(_companyId, run, "admin");

        var ex = await Assert.ThrowsAsync<AccountingException>(() => CancelAsync(asset));
        Assert.Contains("أُهلك", ex.Message, StringComparison.Ordinal);

        await new DepreciationService(_db).ReverseRunAsync(_companyId, run, run, "admin");
        await CancelAsync(asset);
        Assert.Equal(0m, await AssetAccountBalanceAsync());
    }

    /// <summary><b>والمُرسمَلُ عليه يُرفض</b> — عكسُ الاقتناء وحدَه يترك التحسينَ على حسابٍ بلا أصل.</summary>
    [Fact]
    public async Task ACapitalizedAsset_IsRefused()
    {
        await SetupAsync();
        var asset = await AcquireAsync("FA-3");
        asset.Capitalize(1_000m);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() => CancelAsync(asset));
        Assert.Contains("رُسمِل", ex.Message, StringComparison.Ordinal);
        Assert.Equal(JournalEntryStatus.Posted,
            (await _db.JournalEntries.SingleAsync(e => e.Id == asset.AcquisitionJournalEntryId)).Status);
    }

    [Fact]
    public async Task ADisposedAsset_IsRefused()
    {
        await SetupAsync();
        var asset = await AcquireAsync("FA-4");
        asset.MarkDisposed();
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<AccountingException>(() => CancelAsync(asset));
    }

    [Fact]
    public async Task ACancelledAcquisition_IsNotCancelledTwice()
    {
        await SetupAsync();
        var asset = await AcquireAsync("FA-5");
        await CancelAsync(asset);
        var entries = await _db.JournalEntries.CountAsync();

        await Assert.ThrowsAsync<AccountingException>(() => CancelAsync(asset));
        Assert.Equal(entries, await _db.JournalEntries.CountAsync());
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private Task CancelAsync(FixedAsset asset) =>
        new FixedAssetCancellationService(_db).CancelAcquisitionAsync(_companyId, asset.Id, Acquired.AddDays(3), "admin");

    private async Task<FixedAsset> AcquireAsync(string code)
    {
        var asset = FixedAsset.Create(_companyId, code, "سيارة", Acquired, 12_000m, 0m, 12, _assetAcctId, _depAcctId);
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        await new FixedAssetAcquisitionPostingService(_db).PostAcquisitionAsync(asset);
        return asset;
    }

    private async Task<decimal> AssetAccountBalanceAsync()
    {
        var lines = await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountId == _assetAcctId)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync();
        return lines.Sum(l => l.Debit - l.Credit);
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700157", BusinessProfile.Retail);
        _companyId = company.Id;
        _assetAcctId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("12") && !a.Code.StartsWith("1207") && !a.IsGroup)
            .Select(a => a.Id).FirstAsync();
        _depAcctId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("1207"))
            .Select(a => a.Id).FirstAsync();
    }
}
