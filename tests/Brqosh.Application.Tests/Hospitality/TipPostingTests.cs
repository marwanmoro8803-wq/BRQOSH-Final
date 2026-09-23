using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Hospitality;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Hospitality;

/// <summary>
/// (CEEF-230 · ط3-٣د) <b>الإكراميةُ التزامٌ في الأستاذ لا إيراد.</b>
///
/// <para><b>وهذا ما يُقاس هنا</b>: النطاقُ أثبت القسمةَ في <c>Brqosh.Domain.Tests</c>،
/// <b>وهذه تُثبت أنّ المالَ يقع في الحساب الصحيح</b> — فقسمةٌ صحيحةٌ تُقيَّد إيراداً
/// تُبقي العطلَ كما كان.</para>
///
/// <para><b>وهويّةُ هـ-١٩ على الحساب:</b> <c>رصيدُ 2116 = المحصَّلُ − المدفوع</c>.</para>
/// </summary>
public class TipPostingTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public TipPostingTests()
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
    /// <b>القياسُ الحاكم: المحصَّلُ يُقيَّد التزاماً — والإيرادُ لا يتحرّك بريال.</b>
    ///
    /// <para><b>وهذا هو العطلُ كلُّه</b>: بلا <c>2116</c> تُقيَّد الإكراميةُ إيراداً،
    /// <b>فتُدفع عنها ضريبةٌ وزكاةٌ على مالٍ ليس للمنشأة</b>.</para>
    /// </summary>
    [Fact]
    public async Task CollectedTips_LandInTheLiability_AndNeverTouchRevenue()
    {
        await SetupAsync();
        var revenueBefore = await BalanceAsync("4");

        var pool = TipPool.Open(_companyId, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 300m);
        pool.Collect(TipSource.Card, 200m);
        pool.DistributeEvenly([5L, 6L]);
        _db.TipPools.Add(pool);
        await _db.SaveChangesAsync();

        await new TipPoolService(_db).CloseAndPostAsync(
            _companyId, pool.Id, new DateOnly(2026, 6, 1));

        // ‏دائنٌ ٥٠٠ ⇒ رصيدُ الالتزام سالبٌ بالاصطلاح المدين.
        Assert.Equal(-500m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.TipsPayable));
        Assert.Equal(revenueBefore, await BalanceAsync("4"));
    }

    /// <summary>
    /// <b>والنقديُّ يدخل الصندوقَ والبطاقةُ تدخل البنك.</b>
    ///
    /// <para><b>وجمعُهما على الصندوق يُظهر فيه مالاً لم يدخله</b> — فيفترق رصيدُ الدفتر
    /// عن الدرج، ويُطارَد الفرقُ شهرياً ولا يُفهم.</para>
    /// </summary>
    [Fact]
    public async Task CashTipsHitTheDrawer_AndCardTipsHitTheBank()
    {
        await SetupAsync();
        var cashBefore = await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var bankBefore = await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks);

        var pool = TipPool.Open(_companyId, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 300m);
        pool.Collect(TipSource.Card, 200m);
        pool.DistributeEvenly([5L]);
        _db.TipPools.Add(pool);
        await _db.SaveChangesAsync();

        await new TipPoolService(_db).CloseAndPostAsync(
            _companyId, pool.Id, new DateOnly(2026, 6, 1));

        Assert.Equal(cashBefore + 300m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashOnHand));
        Assert.Equal(bankBefore + 200m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks));
    }

    /// <summary>
    /// <b>وهويّةُ هـ-١٩ على الحساب: الصرفُ يُفرغ الالتزامَ تماماً.</b>
    ///
    /// <para>وبقاءُ رصيدٍ بعد الصرف <b>مالٌ للنُّدُل لم يصلهم</b>، وسالبُه <b>دفعٌ لما
    /// لم يُحصَّل</b>.</para>
    /// </summary>
    [Fact]
    public async Task PayingOut_EmptiesTheLiabilityExactly()
    {
        await SetupAsync();

        var pool = TipPool.Open(_companyId, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 450m);
        pool.DistributeEvenly([5L, 6L, 7L]);
        _db.TipPools.Add(pool);
        await _db.SaveChangesAsync();

        var service = new TipPoolService(_db);
        await service.CloseAndPostAsync(_companyId, pool.Id, new DateOnly(2026, 6, 1));
        await service.PayOutAsync(_companyId, pool.Id, new DateOnly(2026, 6, 5));

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.TipsPayable));
    }

    /// <summary>
    /// <b>ولا يُدفع وعاءٌ لم يُقيَّد.</b>
    ///
    /// <para><b>والدفعُ قبل القيد يُنقص الالتزامَ قبل أن يُزاد</b> — فيصير الرصيدُ سالباً،
    /// وهو ما تمنعه <c>هـ-١٩</c>.</para>
    /// </summary>
    [Fact]
    public async Task PayingOutBeforePosting_IsRefused()
    {
        await SetupAsync();

        var pool = TipPool.Open(_companyId, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 100m);
        pool.Distribute(5L, 100m);
        _db.TipPools.Add(pool);
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            new TipPoolService(_db).PayOutAsync(_companyId, pool.Id, new DateOnly(2026, 6, 5)));

        Assert.Contains("أُقفل وقُيّد", error.Message);
    }

    /// <summary>
    /// <b>ووعاءٌ ببقيّةٍ لا يُقفل ولا يُقيَّد.</b>
    ///
    /// <para>والحارسُ في النطاق، <b>والكاتبُ يستدعيه قبل أن يبني قيداً</b> — فلا يُقيَّد
    /// التزامٌ بلا صاحبٍ لبعضه.</para>
    /// </summary>
    [Fact]
    public async Task PostingAPoolWithARemainder_IsRefusedAndPostsNothing()
    {
        await SetupAsync();

        var pool = TipPool.Open(_companyId, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Cash, 200m);
        pool.Distribute(5L, 150m);
        _db.TipPools.Add(pool);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<AccountingException>(() =>
            new TipPoolService(_db).CloseAndPostAsync(_companyId, pool.Id, new DateOnly(2026, 6, 1)));

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.TipsPayable));
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-٤٨ · <c>sector-modules#8</c>) <b>إكراميةُ البطاقة تدخل حسابَ البنك
    /// نفسَه — لا الأبَ <c>1102</c>.</b>
    ///
    /// <para>‏والاختبارُ المجاور يقيس «البطاقةُ تدخل البنك» <b>بالبادئة</b> <c>1102</c> — فيضمّ
    /// الأبَ وأبناءه ويمرّ على الشقّ نفسِه. <b>وهنا الحسابُ بعينه</b>.</para>
    /// </summary>
    [Fact]
    public async Task CardTips_LandInTheBankAccount_NotItsParent()
    {
        await SetupAsync();
        var bank = await new BankAccountService(_db).CreateAsync(
            _companyId, "B1", "البنك الأول", "مصرف الرياض", "1234567890",
            "SA0380000000608010167519", isDefault: true);
        var bankGl = (await _db.Accounts.SingleAsync(a => a.Id == bank.GlAccountId)).Code;

        var pool = TipPool.Open(_companyId, new DateOnly(2026, 6, 1));
        pool.Collect(TipSource.Card, 200m);
        pool.DistributeEvenly([5L]);
        _db.TipPools.Add(pool);
        await _db.SaveChangesAsync();

        await new TipPoolService(_db).CloseAndPostAsync(_companyId, pool.Id, new DateOnly(2026, 6, 1));

        Assert.Equal(200m, await ExactBalanceAsync(bankGl));
        Assert.Equal(0m, await ExactBalanceAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks));
    }

    private async Task<decimal> ExactBalanceAsync(string code) =>
        await _db.JournalEntries.AsNoTracking()
            .WhereReportable(_companyId)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == code)
            .SumAsync(l => l.Debit - l.Credit);

    private async Task<decimal> BalanceAsync(string prefix)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode.StartsWith(prefix))
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مطعم", "310123456700077", BusinessProfile.Hospitality);
        _companyId = company.Id;
    }
}
