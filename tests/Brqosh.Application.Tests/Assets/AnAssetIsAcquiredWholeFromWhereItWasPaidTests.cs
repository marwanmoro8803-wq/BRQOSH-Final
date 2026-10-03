using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Assets;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٩٩ · <c>treasury-payroll-assets#12</c>) <b>الأصلُ وقيدُ اقتنائه معاً — ومن حيث دُفع.</b>
///
/// <para>وكانت النافذةُ تحفظ الأصلَ ثمّ ترحّله في معاملةٍ ثانية — فترحيلٌ يُرفض يُبقي أصلاً بلا قيد يُهلكه التشغيلُ التالي؛ وكان
/// الدائنُ الصندوقَ أبداً، والترحيلُ الثاني لأصلٍ واحدٍ يُقيّد اقتناءً ثانياً.</para>
/// </summary>
public class AnAssetIsAcquiredWholeFromWhereItWasPaidTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _assetAcct;
    private long _depAcct;

    public AnAssetIsAcquiredWholeFromWhereItWasPaidTests()
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
    /// ‏ترحيلٌ يُرفض (فترةٌ مقفلة) <b>لا يُبقي الأصل</b> — والأصلُ الجديدُ لم يُحفظ قبله، فارتدّ معه.
    /// </summary>
    [Fact]
    public async Task APostingThatIsRefused_LeavesNoAssetBehind()
    {
        await SetupAsync();
        var july = FiscalPeriod.Create(_companyId, 2026, 7);
        _db.FiscalPeriods.Add(july);
        await _db.SaveChangesAsync();
        await new FiscalPeriodService(_db).CloseAsync(_companyId, july.Id, "admin");

        var asset = NewAsset("FA-991", new DateOnly(2026, 7, 10), 12_000m);
        await Assert.ThrowsAsync<AccountingException>(() =>
            new FixedAssetAcquisitionPostingService(_db).PostAcquisitionAsync(asset));

        Assert.False(await _db.FixedAssets.AsNoTracking()
            .AnyAsync(a => a.CompanyId == _companyId && a.Code == "FA-991"));
        Assert.False(await _db.JournalEntries.AsNoTracking()
            .AnyAsync(e => e.CompanyId == _companyId && e.SourceDocument == "FixedAsset:FA-991"));
    }

    /// <summary>‏الأصلُ الجديدُ يُحفظ مع قيده، ومرتبطاً به — نقداً من الصندوق كما كان.</summary>
    [Fact]
    public async Task ANewAsset_IsSavedWithItsEntry()
    {
        await SetupAsync();
        var asset = NewAsset("FA-992", new DateOnly(2026, 8, 5), 3_000m);

        var entry = await new FixedAssetAcquisitionPostingService(_db).PostAcquisitionAsync(asset);

        var row = await _db.FixedAssets.AsNoTracking()
            .SingleAsync(a => a.CompanyId == _companyId && a.Code == "FA-992");
        Assert.Equal(entry.Id, row.AcquisitionJournalEntryId);
        Assert.Equal(3_000m, entry.Lines.Single(l => l.AccountId == _assetAcct).Debit);
        Assert.Equal(3_000m, entry.Lines
            .Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CashOnHand).Credit);
    }

    /// <summary>‏المدفوعُ من بنكٍ بعينه يُقيَّد على أستاذ ذلك البنك — لا الصندوق ولا البنك الافتراضيّ.</summary>
    [Fact]
    public async Task PaidFromABank_CreditsThatBank_NotTheTill()
    {
        await SetupAsync();
        var banks = new BankAccountService(_db);
        await banks.CreateAsync(_companyId, "B1", "البنك الأول", "مصرف الرياض", "111",
            "SA1111111111111111111111", isDefault: true);
        var second = await banks.CreateAsync(_companyId, "B2", "البنك الثاني", "البنك الأهلي", "222",
            "SA2222222222222222222222", isDefault: false);
        var asset = NewAsset("FA-993", new DateOnly(2026, 8, 5), 8_000m);

        var entry = await new FixedAssetAcquisitionPostingService(_db)
            .PostAcquisitionAsync(asset, ReceiptMethod.Bank, second.Id);

        var credit = entry.Lines.Single(l => l.Credit > 0);
        Assert.Equal(second.GlAccountId, credit.AccountId);
        Assert.Equal(8_000m, credit.Credit);
        Assert.DoesNotContain(entry.Lines, l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CashOnHand);
    }

    /// <summary>‏بنكٌ موقوفٌ يُرفض <b>باسمه</b> — ولا يبقى أصلٌ ولا قيد.</summary>
    [Fact]
    public async Task AStoppedBank_IsRefusedByName_AndNothingIsSaved()
    {
        await SetupAsync();
        var bank = await new BankAccountService(_db).CreateAsync(_companyId, "B3", "بنك موقوف", "مصرف الإنماء", "333",
            "SA3333333333333333333333", isDefault: false);
        bank.Deactivate();
        await _db.SaveChangesAsync();
        var asset = NewAsset("FA-994", new DateOnly(2026, 8, 5), 5_000m);

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new FixedAssetAcquisitionPostingService(_db).PostAcquisitionAsync(asset, ReceiptMethod.Bank, bank.Id));

        Assert.Contains("بنك موقوف", ex.Message, StringComparison.Ordinal);
        Assert.False(await _db.FixedAssets.AsNoTracking()
            .AnyAsync(a => a.CompanyId == _companyId && a.Code == "FA-994"));
    }

    /// <summary>‏الترحيلُ الثاني لأصلٍ واحدٍ يُرفض باسم الأصل — ويبقى قيدُ اقتناءٍ واحد.</summary>
    [Fact]
    public async Task TheSameAcquisition_IsNotPostedTwice()
    {
        await SetupAsync();
        var asset = NewAsset("FA-995", new DateOnly(2026, 8, 5), 2_000m);
        var service = new FixedAssetAcquisitionPostingService(_db);
        await service.PostAcquisitionAsync(asset);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => service.PostAcquisitionAsync(asset));

        Assert.Contains("FA-995", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, await _db.JournalEntries
            .CountAsync(e => e.CompanyId == _companyId && e.SourceDocument == "FixedAsset:FA-995"));
    }

    /// <summary>
    /// ‏النافذةُ لا تحفظ الأصلَ قبل قيده، وتسأل من أين دُفع — بنيويّاً: معالجاتُها تبلغ دفاترَ الإنتاج عبر <c>AppServices</c>
    /// ولا تُشغَّل في اختبار، على سُنّة ح-٦٦ وح-٩٦ وح-٩٧.
    /// </summary>
    [Fact]
    public void TheDialog_SavesTheAssetOnlyWithItsEntry_AndAsksWhereItWasPaidFrom()
    {
        var pages = Path.Combine(RepoRoot(), "src", "Brqosh.Desktop", "Views", "Pages");
        var xaml = File.ReadAllText(Path.Combine(pages, "FixedAssetDialog.xaml"));
        var code = File.ReadAllText(Path.Combine(pages, "FixedAssetDialog.xaml.cs"));

        Assert.Contains("x:Name=\"FundingCombo\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("FixedAssets.Add(", code, StringComparison.Ordinal);
        Assert.Contains("PostAcquisitionAsync(assetNew, funding.Method, funding.BankAccountId)", code, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        _assetAcct = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("12") && !a.Code.StartsWith("1207") && !a.IsGroup)
            .Select(a => a.Id).FirstAsync();
        _depAcct = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code.StartsWith("1207"))
            .Select(a => a.Id).FirstAsync();
    }

    private FixedAsset NewAsset(string code, DateOnly date, decimal cost) =>
        FixedAsset.Create(_companyId, code, "أصل " + code, date, cost, 0m, 12, _assetAcct, _depAcct);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Brqosh.sln غير موجود فوق مجلد الاختبار.");
    }
}