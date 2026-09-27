using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٧٨ · <c>found#1</c>) <b>قيدٌ لا يدخل الدفترَ مُرحَّلاً في فترةٍ مقفلة — أيّاً كان كاتبُه وأيّاً كان التاريخُ
/// الذي فحصه.</b>
///
/// <para><b>ما كان:</b> سبعٌ وثلاثون خدمةً تكتب القيودَ رأساً، وحارسُ الفترة يفحص التاريخَ الذي تُمرّره كلٌّ منها — لا تاريخَ القيد
/// الذي يُكتب. <b>والدفترُ نفسُه كان يقبل</b>: قيدٌ مُرحَّلٌ بتاريخ شهرٍ مقفلٍ يُحفظ بلا سؤال. فالقياسُ هنا <b>على الدفتر لا على
/// خدمة</b> — كاتبٌ مباشرٌ يمثّل السبعَ والثلاثين ومَن يأتي بعدها.</para>
///
/// <para><b>والشواهدُ الضابطة</b> تمنع أن يصير الحارسُ عطلاً آخر: الشهرُ المفتوحُ يقبل، والمسوّدةُ تُحفظ، <b>وعكسُ قيدِ شهرٍ مقفلٍ
/// بقيدٍ في شهرٍ مفتوحٍ يمضي</b> — فالأصلُ يُعلَّم «معكوساً» لا «مُرحَّلاً».</para>
/// </summary>
public class ALedgerEntryNeverLandsInAClosedPeriodTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;

    public ALedgerEntryNeverLandsInAClosedPeriodTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الدفتر", "310123456700055", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Database.CloseConnection();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary><b>القياسُ الحاكم:</b> قيدٌ مُرحَّلٌ بتاريخ شهرٍ مقفلٍ يُرفض عند الحفظ — ولا يبقى منه أثر.</summary>
    [Fact]
    public async Task APostedEntry_DatedInAClosedMonth_IsRefusedAtSave()
    {
        await ClosePeriodAsync(2026, 1);
        await EntryAsync(new DateOnly(2026, 1, 15), "Direct:1");

        var ex = await Assert.ThrowsAsync<AccountingException>(() => _db.SaveChangesAsync());
        Assert.Contains("2026-01", ex.Message, StringComparison.Ordinal);

        _db.ChangeTracker.Clear();
        Assert.False(await _db.JournalEntries.AnyAsync(e => e.SourceDocument == "Direct:1"));
    }

    /// <summary>
    /// <b>وسنةٌ أقفلها قيدُ الإقفال السنويّ</b> — وشهرُها الساكنُ بلا صفّ فترة — ترفض كذلك، بالقاعدة التي في
    /// <c>EnsurePeriodOpenAsync</c> نفسِها. <b>وقيدُ الإقفال نفسُه يُحفظ</b>: السنةُ لم تكن مقفلةً قبله.
    /// </summary>
    [Fact]
    public async Task APostedEntry_InAYearClosedByItsClosingEntry_IsRefusedAtSave()
    {
        await EntryAsync(new DateOnly(2025, 12, 31), YearEndCloseService.SourceDocumentFor(2025));
        await _db.SaveChangesAsync();

        await EntryAsync(new DateOnly(2025, 3, 10), "Direct:2");

        await Assert.ThrowsAsync<AccountingException>(() => _db.SaveChangesAsync());
    }

    /// <summary><b>ومسودّةٌ رُحِّلت لاحقاً</b> — لا تُضاف مُرحَّلةً بل تتحوّل — تُرفض كذلك.</summary>
    [Fact]
    public async Task PostingADraftOfAClosedMonthLater_IsRefusedAtSave()
    {
        var draft = await EntryAsync(new DateOnly(2026, 1, 20), "Direct:3", post: false);
        await _db.SaveChangesAsync();
        await ClosePeriodAsync(2026, 1);

        draft.Post("JE-Direct:3");

        await Assert.ThrowsAsync<AccountingException>(() => _db.SaveChangesAsync());
    }

    /// <summary><b>شاهدُ ضبط:</b> الشهرُ المفتوحُ يقبل — فالحارسُ لا يرفض كلَّ شيء.</summary>
    [Fact]
    public async Task APostedEntry_InAnOpenMonth_IsSaved()
    {
        await ClosePeriodAsync(2026, 1);
        await EntryAsync(new DateOnly(2026, 2, 10), "Direct:4");

        await _db.SaveChangesAsync();

        Assert.True(await _db.JournalEntries.AnyAsync(e => e.SourceDocument == "Direct:4"));
    }

    /// <summary><b>شاهدُ ضبط:</b> المسوّدةُ لم تدخل الدفتر — فتُحفظ ولو أُرّخت في شهرٍ مقفل.</summary>
    [Fact]
    public async Task ADraft_DatedInAClosedMonth_IsSaved()
    {
        await ClosePeriodAsync(2026, 1);
        await EntryAsync(new DateOnly(2026, 1, 25), "Direct:5", post: false);

        await _db.SaveChangesAsync();

        Assert.True(await _db.JournalEntries.AnyAsync(e => e.SourceDocument == "Direct:5"));
    }

    /// <summary>
    /// <b>شاهدُ ضبط — التصحيحُ المشروعُ يمضي:</b> قيدٌ يدويٌّ من شهرٍ أُقفل يُعكس بقيدٍ في شهرٍ مفتوح؛ والأصلُ يُعلَّم «معكوساً».
    /// (‏<b>يدويٌّ بلا مستندٍ مصدر</b> — فخدمةُ العكس ترفض عكسَ قيدِ مستندِ نظامٍ مباشرةً، وهذا حارسٌ آخرُ لا يُقاس هنا.)
    /// </summary>
    [Fact]
    public async Task ReversingAnEntryOfAClosedMonth_IntoAnOpenMonth_IsSaved()
    {
        var original = await EntryAsync(new DateOnly(2026, 1, 5), source: null, number: "JE-Manual-6");
        await _db.SaveChangesAsync();
        await ClosePeriodAsync(2026, 1);

        var reversal = await new JournalReversalService(_db)
            .ReverseAsync(_companyId, original.Id, new DateOnly(2026, 2, 1), "admin");

        Assert.Equal(JournalEntryStatus.Reversed, original.Status);
        Assert.Equal(JournalEntryStatus.Posted, reversal.Status);
    }

    /// <summary>
    /// <b>والحارسُ على كلّ سياقٍ يصنعه المنتَج</b> — المصنعُ (سطحُ المكتب والأدوات) ومسارُ DI (الواجهةُ البرمجيّة) —
    /// كحارس سجلّ التدقيق. <b>ودرسُ ط4-٣</b>: إصلاحُ أحد مسارَي شيءٍ واحدٍ يترك العطلَ حيّاً في الآخر.
    /// </summary>
    [Fact]
    public void TheGuard_IsOnEveryContextTheProductMakes()
    {
        Assert.Contains(Interceptors(_db), i => i is ClosedPeriodPostingInterceptor);

        using var provider = new ServiceCollection()
            .AddBrqoshInfrastructure("Data Source=:memory:", usePostgres: false)
            .BuildServiceProvider();
        using var scope = provider.CreateScope();
        var fromDi = scope.ServiceProvider.GetRequiredService<BrqoshDbContext>();
        Assert.Contains(Interceptors(fromDi), i => i is ClosedPeriodPostingInterceptor);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private static IEnumerable<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor> Interceptors(BrqoshDbContext db) =>
        db.GetService<IDbContextOptions>().Extensions.OfType<CoreOptionsExtension>().Single().Interceptors ?? [];

    private async Task<JournalEntry> EntryAsync(DateOnly date, string? source, bool post = true, string? number = null)
    {
        var cash = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var bank = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == _companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashAtBanks);

        var entry = JournalEntry.CreateDraft(_companyId, date, "قيدٌ مباشر", source);
        entry.AddLine(cash, 100m, 0m);
        entry.AddLine(bank, 0m, 100m);
        if (post)
            entry.Post(number ?? "JE-" + source);
        _db.JournalEntries.Add(entry);
        return entry;
    }

    private async Task ClosePeriodAsync(int year, int month)
    {
        var code = $"{year:D4}-{month:D2}";
        var period = await _db.FiscalPeriods.FirstOrDefaultAsync(p => p.CompanyId == _companyId && p.Code == code);
        if (period is null)
        {
            period = FiscalPeriod.Create(_companyId, year, month);
            _db.FiscalPeriods.Add(period);
        }

        period.Close();
        await _db.SaveChangesAsync();
    }
}
