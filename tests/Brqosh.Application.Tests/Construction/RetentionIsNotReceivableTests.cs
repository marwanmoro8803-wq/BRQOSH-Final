using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Construction;

/// <summary>
/// (CEEF-232 · ج-١) <b>المحتجزُ ليس ذمّةً مدينة — ودفترُ العميل كان يعدّه كذلك.</b>
///
/// <para><b>ما كان:</b> مستخلصُ الأعمال يُرحَّل <b>على حسابين</b>: صافي المستحقّ وضريبتُه
/// على الذمم (<c>1104</c>)، <b>والمحتجزُ على حسابٍ آخر</b> (<c>1114</c>) — وهو الصواب،
/// فالمحتجزُ لا يُطالَب به قبل انقضاء مدّة الضمان. <b>ودفترُ العملاء المساعد كان يجمع
/// <c>GrandTotal</c></b> — الصافي والمحتجز والضريبة معاً.</para>
///
/// <para><b>فتنكسر هويّةُ هـ-١ بمقدار المحتجز في كلّ مستخلص</b>: مقاولٌ يُخرج مئةَ مليونٍ
/// بنسبة احتجازٍ عشرة بالمئة <b>يرى في دفتر عملائه عشرةَ ملايينَ ذمّةً لا وجودَ لها في
/// الأستاذ</b> — تُقرأ سيولةً قادمة، وتظهر في أعمار الديون فيُطارَد بها عميلٌ لا يدين
/// بها، <b>وتُقفل السنةُ على أصلٍ مُعلَنٍ مرّتين</b>: مرّةً في <c>1114</c> ومرّةً في دفتر
/// العملاء.</para>
///
/// <para><b>والطرفُ الثاني شرطٌ لا تتمّة:</b> إفراجُ المحتجز يُحوّله <c>1114 ⟵ 1104</c>
/// بقيدٍ لا مستندَ له. <b>فاستبعادُه من الدفتر عند الفوترة بلا إدخاله عند الإفراج يكسر
/// الهويّةَ في الاتجاه المقابل</b> — ويصير العميلُ مدينَ الأستاذ بريءَ الدفتر، وهو أسوأ:
/// <b>مالٌ مستحقٌّ لا يُطالِب به أحد</b>.</para>
///
/// <para><b>وهويّةُ هـ-١ هنا:</b> دفترُ العملاء المساعد = رصيدُ <c>1104</c> في الأستاذ —
/// <b>بعد الفوترة وبعد الإفراج معاً</b>.</para>
/// </summary>
public class RetentionIsNotReceivableTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _customerId;
    private long _projectId;

    private static readonly DateOnly Day = new(2026, 3, 1);

    public RetentionIsNotReceivableTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("مقاولات", "310123456700099", BusinessProfile.Construction)
            .GetAwaiter().GetResult().Id;

        var customer = Customer.Create(_companyId, "جهة مالكة", "311111111100003");
        _db.Customers.Add(customer);
        _db.SaveChanges();
        _customerId = customer.Id;
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم (هـ-١): الدفترُ المساعد = حسابُ المراقبة بعد الفوترة.</b>
    ///
    /// <para>مستخلصٌ بمليونٍ ونسبةِ احتجازٍ عشرة بالمئة: <b>الذممُ ٩٠٠٬٠٠٠ + ١٥٠٬٠٠٠
    /// ضريبةً</b>، <b>والمحتجزُ ١٠٠٬٠٠٠ على حسابه</b>. <b>وكان الدفترُ يقول ١٬١٥٠٬٠٠٠.</b></para>
    ///
    /// <para><b>والضريبةُ على الأعمال كلِّها لا على الصافي</b> — فالاحتجازُ ضمانُ تنفيذٍ لا
    /// خصمٌ من الثمن، <b>والتوريدُ وقع بمليون</b>.</para>
    /// </summary>
    [Fact]
    public async Task AfterBilling_TheSubledgerEqualsTheControlAccount()
    {
        await BillAsync(work: 1_000_000m, retentionPercent: 10m);

        Assert.Equal(1_050_000m, await SubledgerAsync());
        Assert.Equal(1_050_000m, await ControlAccountAsync());
        Assert.Equal(100_000m, await AccountAsync(SaudiChartOfAccounts.SystemCodes.RetentionReceivable));
    }

    /// <summary>
    /// <b>وبعد الإفراج يعود المحتجزُ ذمّةً — في الدفتر كما في الأستاذ.</b>
    ///
    /// <para><b>وهذا هو الطرفُ الذي يُنسى</b>: يُستبعد المحتجزُ عند الفوترة فيصحّ الرقم،
    /// <b>ثمّ يُفرَج عنه فلا يدخل الدفترَ</b> — فيبقى العميلُ في كشفه مسدِّداً وعليه مئةُ
    /// ألف، <b>ولا يُطالَب بها لأنّ المطالبة تُبنى على الدفتر لا على الأستاذ</b>.</para>
    /// </summary>
    [Fact]
    public async Task AfterRelease_TheReleasedRetention_BecomesReceivableInBothBooks()
    {
        await BillAsync(work: 1_000_000m, retentionPercent: 10m);

        await new RetentionReleaseService(_db).ReleaseAsync(
            _companyId, _projectId, RetentionReleaseType.CustomerRelease,
            60_000m, Day.AddMonths(12), "إفراج أوّل", "admin");

        Assert.Equal(1_110_000m, await ControlAccountAsync());
        Assert.Equal(1_110_000m, await SubledgerAsync());
        Assert.Equal(40_000m, await AccountAsync(SaudiChartOfAccounts.SystemCodes.RetentionReceivable));
    }

    /// <summary>
    /// <b>وكشفُ الحساب يتبع الرصيد — وإلّا اختلف رأسُه عن سطوره.</b>
    ///
    /// <para><b>وهو أوّلُ ما يفحصه العميل</b>: يجمع أعمدةَ الكشف فلا تُساوي الرصيدَ
    /// المعلَن، <b>فيفقد الثقةَ في الرقمين معاً</b> — ويصير كلُّ تحصيلٍ تفاوضاً.</para>
    /// </summary>
    [Fact]
    public async Task TheStatement_AgreesWithTheBalance()
    {
        await BillAsync(work: 1_000_000m, retentionPercent: 10m);

        var statement = await new CustomerBalanceQuery(_db).StatementAsync(_companyId, _customerId);

        Assert.Equal(await SubledgerAsync(), statement[^1].RunningBalance);
    }

    /// <summary>
    /// <b>ودفترُ الجميع كدفتر الواحد.</b>
    ///
    /// <para><b>ومساران يُحسب بهما الشيءُ نفسه دَينُ عطلٍ مؤجَّل</b>: أُصلح أحدُهما في
    /// <c>ط4-٣</c> وبقي الآخر، <b>فظهر عميلٌ مسدَّدٌ في اللوحة مدينٌ في كشفه</b>. فيُقاسان
    /// معاً منذ اليوم الأوّل.</para>
    /// </summary>
    [Fact]
    public async Task TheAllBalancesPath_AgreesWithThePerCustomerPath()
    {
        await BillAsync(work: 1_000_000m, retentionPercent: 10m);

        await new RetentionReleaseService(_db).ReleaseAsync(
            _companyId, _projectId, RetentionReleaseType.CustomerRelease,
            25_000m, Day.AddMonths(12), "إفراج", "admin");

        var all = await new CustomerBalanceQuery(_db).AllBalancesAsync(_companyId);

        Assert.Equal(await SubledgerAsync(), all[_customerId]);
    }

    /// <summary>
    /// <b>ومستخلصٌ بلا احتجازٍ لا يتغيّر شيءٌ فيه.</b>
    ///
    /// <para><b>وبغير هذا القياس يمرّ «اطرح عشرةَ بالمئة دائماً»</b> — فتُنقص ذمّةُ كلّ
    /// عقدٍ بلا ضمانٍ عُشرَها، <b>وهو عطلٌ أوسعُ من الذي أُصلح</b>.</para>
    /// </summary>
    [Fact]
    public async Task ABillingWithoutRetention_IsUnchanged()
    {
        await BillAsync(work: 500_000m, retentionPercent: 0m);

        Assert.Equal(575_000m, await SubledgerAsync());
        Assert.Equal(575_000m, await ControlAccountAsync());
    }

    /// <summary>
    /// (CEEF-232 · ج-٢) <b>وكشفُ الحساب يُظهر الإفراج سطراً — لا يبتلعه في رأسه.</b>
    ///
    /// <para><b>وهذا نصفٌ فات الإصلاحَ الأوّل:</b> الرصيدُ ضمّ المُفرَج عنه <b>والكشفُ لم
    /// يعرفه</b> — فيُطالَب العميلُ برقمٍ في الرأس <b>لا يجد له في السطور أصلاً</b>.
    /// <b>وأوّلُ ما يفعله العميل جمعُ الأعمدة</b>، فإن لم تُطابق صار كلُّ تحصيلٍ تفاوضاً.</para>
    /// </summary>
    [Fact]
    public async Task TheStatement_ShowsTheRelease_AsItsOwnLine()
    {
        await BillAsync(work: 1_000_000m, retentionPercent: 10m);
        await ReleaseAsync(60_000m);

        var statement = await new CustomerBalanceQuery(_db)
            .StatementAsync(_companyId, _customerId);

        Assert.Contains(statement, l => l.DocumentType == "إفراج محتجز" && l.Debit == 60_000m);

        // ‏**والرصيدُ الجاري في آخر سطرٍ هو رصيدُ الدفتر** — وإلّا خالف الرأسُ سطورَه.
        Assert.Equal(await SubledgerAsync(), statement[^1].RunningBalance);
    }

    /// <summary>
    /// (CEEF-232 · ج-٣) <b>وتقريرُ الأعمار: المحتجزُ لا يُطفئ الأقدم.</b>
    ///
    /// <para><b>وهو عطلٌ وُلد من الإصلاح نفسِه:</b> الرصيدُ صار يستبعد المحتجز،
    /// <b>وتقريرُ الأعمار بقي يقرأ <c>GrandTotal</c></b> — والائتمانُ عنده يُشتقّ من الفرق.
    /// <b>فالمحتجزُ يصير سداداً وهمياً يُطفئ الأقدمَ فالأقدم.</b></para>
    ///
    /// <para><b>ومجموعُ التقرير يبقى صحيحاً فتمرّ هويّةُ هـ-١٤</b> — والعطلُ في التوزيع
    /// وحدَه، <b>وهو كلُّ ما يُقرأ التقريرُ من أجله</b>: مَن تأخّر، ومنذ متى.</para>
    /// </summary>
    [Fact]
    public async Task TheAgingBuckets_DoNotExtinguishTheOldestBillingWithRetention()
    {
        // ‏**ومستخلصان لا واحد — وهذا شرطُ القياس لا زينتُه.**
        // ‏فبمستخلصٍ واحدٍ يقع «السدادُ الوهميّ» على المستند الذي وُلد منه، **فيُطفئ نفسَه
        // ‏ويعود الرقمُ صحيحاً بالمصادفة**. والعطلُ لا يظهر إلّا حين يُطفئ المحتجزُ
        // ‏**الجديدُ** مستخلصاً **قديماً** — وهو ما يقع في كلّ مشروعٍ حقيقيّ.
        await BillAsync(work: 1_000_000m, retentionPercent: 10m);                  // ‏قديم
        await BillAsync(work: 1_000_000m, retentionPercent: 10m, on: Day.AddDays(100)); // ‏حديث

        var row = (await new ReceivableAgingQuery(_db)
            .ExecuteAsync(_companyId, Day.AddDays(120))).Single();

        Assert.Equal(1_050_000m, row.Over90);   // ‏القديمُ كاملاً — **وكان ٩٥٠٬٠٠٠**
        Assert.Equal(1_050_000m, row.Current);  // ‏والحديثُ كاملاً — **وكان ١٬١٥٠٬٠٠٠**
        Assert.Equal(await SubledgerAsync(), row.Total);   // ‏هـ-١٤ — **وكانت تمرّ في الحالتين**
    }

    /// <summary>
    /// <b>والمُفرَج عنه يُعمَّر من يوم الإفراج لا من يوم المستخلص.</b>
    ///
    /// <para><b>فهو إلى ذلك اليوم ضمانٌ لا يُطالَب به</b> — وإدخالُه بتاريخ المستخلص
    /// <b>يجعله متأخّراً يومَ وُلد</b>، فيُطارَد به العميلُ في أوّل كشفٍ بعد الإفراج.</para>
    /// </summary>
    [Fact]
    public async Task AReleasedRetention_AgesFromItsReleaseDate()
    {
        await BillAsync(work: 1_000_000m, retentionPercent: 10m);

        var releaseDay = Day.AddDays(200);
        await ReleaseAsync(100_000m, on: releaseDay);

        var row = (await new ReceivableAgingQuery(_db)
            .ExecuteAsync(_companyId, releaseDay.AddDays(10))).Single();

        Assert.Equal(100_000m, row.Current);        // ‏عشرةُ أيامٍ من الإفراج
        Assert.Equal(1_050_000m, row.Over90);       // ‏والمستخلصُ في مكانه
        Assert.Equal(await SubledgerAsync(), row.Total);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task ReleaseAsync(decimal amount, DateOnly? on = null) =>
        await new RetentionReleaseService(_db).ReleaseAsync(
            _companyId, _projectId, RetentionReleaseType.CustomerRelease,
            amount, on ?? Day.AddMonths(12), "إفراج", "admin");

    private async Task<decimal> SubledgerAsync() =>
        (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId)).Balance;

    private async Task<decimal> ControlAccountAsync() =>
        await AccountAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable);

    private async Task<decimal> AccountAsync(string code)
    {
        var rows = await new TrialBalanceQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2000, 1, 1), new DateOnly(2099, 12, 31));
        var row = rows.FirstOrDefault(r => r.AccountCode == code);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2);
    }

    private async Task BillAsync(decimal work, decimal retentionPercent, DateOnly? on = null)
    {
        if (_projectId == 0)
        {
            var project = ConstructionProject.Create(
                _companyId, "PRJ-1", "مشروع", _customerId, Day, Day.AddYears(2), work * 5m);
            _db.ConstructionProjects.Add(project);
            await _db.SaveChangesAsync();
            _projectId = project.Id;
        }

        var billing = ProgressBilling.CreateDraft(
            _companyId, _projectId, _customerId, on ?? Day, "الفترة الأولى", work, retentionPercent);
        await new ProgressBillingPostingService(_db).PostAsync(billing);
    }
}
