using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Setup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Setup;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٣٣ · <c>accounting-core#7</c>) <b>منشأةٌ تنتقل من نظامٍ آخر تبدأ بأرصدتها — وكانت لا تستطيع.</b>
///
/// <para>‏ترحيلُ البيانات كان دليلَ الحسابات وأسماءَ العملاء وحدَها: <b>لا ميزانَ افتتاحيّ، ولا موردين</b>. فالمنشأةُ تُدخل
/// أرصدتَها قيداً يدويّاً طويلاً — أو تبدأ من صفرٍ وتحمل الفرقَ في جدولٍ خارج النظام.</para>
///
/// <para>‏<b>والميزانُ الافتتاحيُّ قيدٌ واحدٌ مرحَّلٌ أو لا شيء</b>: سطرٌ معيبٌ يُسقطه كلَّه ويُعرض بسببه، فنصفُ أرصدةٍ أسوأُ من
/// لا أرصدة. <b>والذممُ لا تدخل حسابَي الرقابة مباشرةً</b> — فالدفترُ المساعدُ يُبنى من المستندات، وذمّةٌ في 1104 بلا مستندٍ
/// لعميلٍ تكسر مطابقته — <b>بل الحسابَ الوسيط 3107</b> حتى تُفصَّل ذممُها مستنداتٍ لأصحابها.</para>
/// </summary>
public class GoLiveOpeningBalancesTests : IDisposable
{
    private static readonly DateOnly GoLive = new(2026, 1, 1);
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _company;

    public GoLiveOpeningBalancesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        TestActor.Administrator();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private const string Balanced = """
        AccountCode,Debit,Credit
        1101,50000,0
        1102,30000.50,0
        1104,20000,0
        2101,0,15000
        3101,0,85000.50
        """;

    /// <summary>
    /// ‏<b>القياسُ الحاكم:</b> ميزانٌ متوازن ⟵ قيدٌ واحدٌ مرحَّلٌ بتاريخ بدء التشغيل؛ والذممُ في 3107 لا في حسابَي الرقابة،
    /// فهويّتا المطابقة (العملاء والموردون) تبقيان صادقتين.
    /// </summary>
    [Fact]
    public async Task ABalancedTrialBalance_IsPostedAsOneOpeningEntry_WithReceivablesHeldInTheClearingAccount()
    {
        await StartAsync();

        var result = await Service().ImportOpeningTrialBalanceCsvAsync(_company, GoLive, Balanced);

        Assert.Equal(5, result.Imported);
        Assert.Equal(0, result.Skipped);
        var entry = Assert.Single(await _db.JournalEntries.Include(e => e.Lines)
            .Where(e => e.CompanyId == _company && e.SourceDocument == CsvDataMigrationService.OpeningTrialBalanceSource)
            .ToListAsync());
        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        Assert.Equal(GoLive, entry.EntryDate);

        Assert.Equal(50_000m, await BalanceAsync("1101"));
        Assert.Equal(30_000.50m, await BalanceAsync("1102"));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));
        Assert.Equal(5_000m, await BalanceAsync(CsvDataMigrationService.OpeningClearingCode));
        Assert.Equal(-85_000.50m, await BalanceAsync("3101"));

        Assert.True((await LedgerIdentities.ReceivablesAsync(_db, _company)).Holds);
        Assert.True((await LedgerIdentities.PayablesAsync(_db, _company)).Holds);
    }

    /// <summary>‏<b>وميزانٌ لا يتوازن لا يُرحَّل منه شيء</b> — ويُقال الفرقُ.</summary>
    [Fact]
    public async Task AnUnbalancedTrialBalance_PostsNothing()
    {
        await StartAsync();

        var result = await Service().ImportOpeningTrialBalanceCsvAsync(_company, GoLive, "1101,1000,0\n3101,0,999");

        Assert.Equal(0, result.Imported);
        Assert.Contains(result.Details, d => !d.Success && d.Error!.Contains("1.00", StringComparison.Ordinal));
        Assert.False(await AnyOpeningEntryAsync());
    }

    /// <summary>
    /// ‏<b>وكلُّ سطرٍ معيبٍ يُسقط الميزانَ كلَّه ويُعرض بسببه</b> — والسطورُ الصحيحةُ لا تُرحَّل وحدَها.
    /// </summary>
    [Theory]
    [InlineData("9999,100,0", "غير موجود")]
    [InlineData("1,100,0", "تجميعيّ")]
    [InlineData("1101,100,100", "مدينٌ أو دائن")]
    [InlineData("1101,0,0", "مدينٌ أو دائن")]
    [InlineData("1101,-100,0", "سالب")]
    [InlineData("1101,abc,0", "مبلغ")]
    [InlineData("2112,0,100", "يبدأ صفراً")]
    [InlineData("1101,100", "AccountCode,Debit,Credit")]
    public async Task ABadLine_SinksTheWholeTrialBalance_AndSaysWhy(string badLine, string reason)
    {
        await StartAsync();

        var result = await Service().ImportOpeningTrialBalanceCsvAsync(_company, GoLive, $"1102,100,0\n3101,0,100\n{badLine}");

        Assert.Equal(0, result.Imported);
        var failure = Assert.Single(result.Details, d => !d.Success);
        Assert.Equal(3, failure.LineNumber);
        Assert.Contains(reason, failure.Error, StringComparison.Ordinal);
        Assert.False(await AnyOpeningEntryAsync());
    }

    /// <summary>‏<b>والميزانُ الافتتاحيُّ مرّةً واحدة</b> — فإعادةُ الملفّ لا تُضاعف الأرصدة.</summary>
    [Fact]
    public async Task ASecondTrialBalance_IsRefused()
    {
        await StartAsync();
        await Service().ImportOpeningTrialBalanceCsvAsync(_company, GoLive, Balanced);

        var again = await Service().ImportOpeningTrialBalanceCsvAsync(_company, GoLive, Balanced);

        Assert.Equal(0, again.Imported);
        Assert.Contains(again.Details, d => !d.Success && d.Error!.Contains("مرحَّلٌ سلفاً", StringComparison.Ordinal));
        Assert.Equal(50_000m, await BalanceAsync("1101"));
    }

    /// <summary>‏<b>والموردون يُستوردون</b> كالعملاء: الاسمُ والرقمُ الضريبيّ، والمكرَّرُ يُتخطّى بسببه.</summary>
    [Fact]
    public async Task Suppliers_AreImported_AndADuplicateIsSkippedWithItsReason()
    {
        await StartAsync();

        var result = await Service().ImportSuppliersCsvAsync(_company,
            "NameAr,VatNumber\nمؤسسة التوريد,300000000000003\nشركة النقل,\nمؤسسة التوريد,300000000000003\nأ,");

        Assert.Equal(2, result.Imported);
        Assert.Equal(2, result.Skipped);
        Assert.Equal(["شركة النقل", "مؤسسة التوريد"],
            await _db.Suppliers.Where(s => s.CompanyId == _company).Select(s => s.NameAr).OrderBy(n => n).ToListAsync());
        Assert.Equal("300000000000003",
            await _db.Suppliers.Where(s => s.NameAr == "مؤسسة التوريد").Select(s => s.VatRegistrationNumber).SingleAsync());
    }

    /// <summary>
    /// ‏<b>والصفُّ الأوّلُ عناوين كما تَعِد الشاشة</b> — وكان استيرادُ العملاء والدليل يقرؤه سطراً فيُرفض ويُعدّ متعذّراً.
    /// </summary>
    [Fact]
    public async Task AHeaderRow_IsSkipped_NotReportedAsAFailure()
    {
        await StartAsync();

        var customers = await Service().ImportCustomersCsvAsync(_company, "NameAr,VatNumber\nعميل الانتقال,");

        Assert.Equal(1, customers.TotalRows);
        Assert.Equal(1, customers.Imported);
        Assert.Equal(2, Assert.Single(customers.Details).LineNumber);
    }

    private async Task StartAsync() =>
        _company = (await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الانتقال", "300000000000003", BusinessProfile.General)).Id;

    private CsvDataMigrationService Service() => new(_db);

    private Task<bool> AnyOpeningEntryAsync() =>
        _db.JournalEntries.AnyAsync(e => e.CompanyId == _company
                                         && e.SourceDocument == CsvDataMigrationService.OpeningTrialBalanceSource);

    private async Task<decimal> BalanceAsync(string code)
    {
        var rows = await new TrialBalanceQuery(_db).ExecuteAsync(_company, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));
        var row = rows.FirstOrDefault(r => r.AccountCode == code);
        return row is null ? 0m : row.TotalDebit - row.TotalCredit;
    }
}
