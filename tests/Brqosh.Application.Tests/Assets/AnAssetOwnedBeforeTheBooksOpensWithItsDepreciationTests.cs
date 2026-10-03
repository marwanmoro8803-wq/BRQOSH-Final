using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Assets;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٠٠ · <c>treasury-payroll-assets#12</c>) <b>الأصلُ القائمُ قبل بدء التشغيل يُدخَل برصيده الافتتاحيّ</b> — تكلفتُه ومجمّعُ
/// إهلاكه على الأرباح المبقاة لا على الصندوق، <b>ولا يُهلَك ما أُهلك قبل الدفاتر ثانيةً</b>.
///
/// <para>سيارةٌ اقتُنيت في يناير ٢٠٢٣ بـ١٢٬٠٠٠ على ستّين شهراً (٢٠٠ للشهر)، أُهلك منها ٧٬٢٠٠ حتى ديسمبر ٢٠٢٥، والدفاترُ تبدأ في يناير ٢٠٢٦.
/// وكانت لا تُدخَل إلا اقتناءً: الصندوقُ دائنٌ بـ١٢٬٠٠٠ بتاريخ ٢٠٢٣، والتشغيلُ الأوّلُ يستدرك ٣٨ شهراً فيُحمّل سنةَ التشغيل ٧٬٦٠٠.</para>
/// </summary>
public class AnAssetOwnedBeforeTheBooksOpensWithItsDepreciationTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _assetAcct;
    private long _depAcct;

    public AnAssetOwnedBeforeTheBooksOpensWithItsDepreciationTests()
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

    /// <summary>‏مدين الأصل بتكلفته، ودائن المجمّع بما أُهلك، والصافي على 3103 — <b>ولا سطرَ للصندوق</b> — بتاريخ أوّل الدفاتر.</summary>
    [Fact]
    public async Task TheOpeningBalance_CarriesCostAndDepreciationAgainstRetainedEarnings_NotCash()
    {
        await SetupAsync();

        var entry = await new FixedAssetAcquisitionPostingService(_db).PostOpeningAsync(Opening("FA-OB1"));

        Assert.Equal(new DateOnly(2026, 1, 1), entry.EntryDate);
        Assert.Equal(12_000m, entry.Lines.Single(l => l.AccountId == _assetAcct).Debit);
        Assert.Equal(7_200m, entry.Lines.Single(l => l.AccountId == _depAcct).Credit);
        Assert.Equal(4_800m, entry.Lines
            .Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.RetainedEarnings).Credit);
        Assert.DoesNotContain(entry.Lines, l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CashOnHand);

        var row = await _db.FixedAssets.AsNoTracking()
            .SingleAsync(a => a.CompanyId == _companyId && a.Code == "FA-OB1");
        Assert.Equal(7_200m, row.AccumulatedDepreciation);
        Assert.Equal(new DateOnly(2025, 12, 31), row.DepreciatedThrough);
        Assert.Equal(entry.Id, row.AcquisitionJournalEntryId);
    }

    /// <summary>‏التشغيلُ الأوّلُ بعد الرصيد يُهلك يناير وفبراير وحدَهما (٤٠٠) — لا ٣٨ شهراً منذ الاقتناء.</summary>
    [Fact]
    public async Task TheFirstRunAfterTheOpening_DepreciatesOnlyTheMonthsSince()
    {
        await SetupAsync();
        await new FixedAssetAcquisitionPostingService(_db).PostOpeningAsync(Opening("FA-OB2"));

        await new DepreciationService(_db).RunMonthlyAsync(_companyId, new DateOnly(2026, 2, 28), "admin");

        var charged = await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.SourceDocument == "Depreciation:FA-OB2")
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountId == _depAcct)
            .SumAsync(l => l.Credit - l.Debit);
        Assert.Equal(400m, charged);
        var row = await _db.FixedAssets.AsNoTracking()
            .SingleAsync(a => a.CompanyId == _companyId && a.Code == "FA-OB2");
        Assert.Equal(7_600m, row.AccumulatedDepreciation);
    }

    /// <summary>‏مجمّعٌ يتجاوز القابلَ للإهلاك يُرفض، ورصيدٌ افتتاحيٌّ في شهر الاقتناء نفسِه يُرفض — فذاك اقتناءٌ في الدفاتر.</summary>
    [Fact]
    public void AnOpeningBeyondWhatCanBeDepreciated_OrInItsOwnMonth_IsRefused()
    {
        Assert.Throws<AccountingException>(() => FixedAsset.CreateOpening(
            1L, "FA-X", "أصل", new DateOnly(2023, 1, 10), 12_000m, 1_000m, 60, 1L, 2L,
            11_500m, new DateOnly(2026, 1, 1)));
        Assert.Throws<AccountingException>(() => FixedAsset.CreateOpening(
            1L, "FA-Y", "أصل", new DateOnly(2026, 1, 10), 12_000m, 0m, 60, 1L, 2L,
            0m, new DateOnly(2026, 1, 25)));
    }

    /// <summary>‏الرصيدُ الافتتاحيُّ لا يُرحَّل مرّتين، ولا يُرحَّل اقتناءٌ فوقه — ويبقى قيدٌ واحد.</summary>
    [Fact]
    public async Task AnOpeningIsNotPostedTwice_NorAnAcquisitionOverIt()
    {
        await SetupAsync();
        var asset = Opening("FA-OB3");
        var service = new FixedAssetAcquisitionPostingService(_db);
        await service.PostOpeningAsync(asset);

        var again = await Assert.ThrowsAsync<AccountingException>(() => service.PostOpeningAsync(asset));
        Assert.Contains("FA-OB3", again.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<AccountingException>(() => service.PostAcquisitionAsync(asset));
        Assert.Equal(1, await _db.JournalEntries
            .CountAsync(e => e.CompanyId == _companyId && e.SourceDocument == "FixedAsset:FA-OB3"));
    }

    /// <summary>‏أصلٌ يُقتنى في الدفاتر لا يُرحَّل رصيداً افتتاحيّاً — يُرفض باسمه ولا يبقى.</summary>
    [Fact]
    public async Task ANewAcquisition_IsNotPostedAsAnOpening()
    {
        await SetupAsync();
        var asset = FixedAsset.Create(_companyId, "FA-OB4", "حاسب", new DateOnly(2026, 2, 1),
            3_000m, 0m, 36, _assetAcct, _depAcct);

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new FixedAssetAcquisitionPostingService(_db).PostOpeningAsync(asset));

        Assert.Contains("FA-OB4", ex.Message, StringComparison.Ordinal);
        Assert.False(await _db.FixedAssets.AsNoTracking()
            .AnyAsync(a => a.CompanyId == _companyId && a.Code == "FA-OB4"));
    }

    /// <summary>‏قاعدةٌ أقدمُ بلا العمود يُضيفه المُرقِّعُ عند الإقلاع — وإلّا سقط كلُّ استعلامٍ عن الأصول.</summary>
    [Fact]
    public async Task TheUpdater_AddsTheColumnToAnOlderDatabase()
    {
        await SetupAsync();
        await _db.Database.ExecuteSqlRawAsync("ALTER TABLE fixed_assets DROP COLUMN \"DepreciatedThrough\"");
        Assert.False(await HasColumnAsync("fixed_assets", "DepreciatedThrough"));

        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        Assert.True(await HasColumnAsync("fixed_assets", "DepreciatedThrough"));
    }

    /// <summary>
    /// ‏النافذةُ تعرض الرصيدَ الافتتاحيَّ وترحّله — بنيويّاً: معالجاتُها تبلغ دفاترَ الإنتاج عبر <c>AppServices</c> ولا تُشغَّل في اختبار،
    /// على سُنّة ح-٦٦ وح-٩٦ وح-٩٧ وح-٩٩.
    /// </summary>
    [Fact]
    public void TheDialog_OffersTheOpeningBalance()
    {
        var pages = Path.Combine(RepoRoot(), "src", "Brqosh.Desktop", "Views", "Pages");
        var xaml = File.ReadAllText(Path.Combine(pages, "FixedAssetDialog.xaml"));
        var code = File.ReadAllText(Path.Combine(pages, "FixedAssetDialog.xaml.cs"));

        Assert.Contains("x:Name=\"OpeningCheck\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OpeningDateBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"AccumulatedBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FixedAsset.CreateOpening(", code, StringComparison.Ordinal);
        Assert.Contains(".PostOpeningAsync(openingAsset)", code, StringComparison.Ordinal);
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

    private FixedAsset Opening(string code) =>
        FixedAsset.CreateOpening(_companyId, code, "سيارة " + code, new DateOnly(2023, 1, 10),
            12_000m, 0m, 60, _assetAcct, _depAcct, 7_200m, new DateOnly(2026, 1, 1));

    private async Task<bool> HasColumnAsync(string table, string column)
    {
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
        return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
    }

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