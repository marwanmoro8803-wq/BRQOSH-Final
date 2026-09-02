using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.RealEstate;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.RealEstate;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.RealEstate;

/// <summary>
/// (CEEF-230 · ط3-١) <b>العقارات — قطاعٌ كان يُعرض في شاشة الإنشاء بلا كيانٍ واحدٍ خلفه.</b>
///
/// <para><b>ما كان:</b> «العقارات» أحدُ ملفّات العمل التي يختارها المالك عند التأسيس،
/// <b>ولا عقارَ في النظام ولا وحدةَ ولا عقدَ إيجار</b>. فالمنشأةُ العقارية تحصل على دفترِ
/// أستاذٍ عامّ لا أكثر، <b>وتُدير عقاراتها في جدولٍ خارجيّ</b> — وهو ما يفعله كلُّ مَن
/// اشترى برنامجاً ثمّ عاد إلى Excel.</para>
///
/// <para><b>ومسبارُ القطاعات كان يُظهر العقاراتِ خضراء</b> لأنّها تبيع وتُحصّل وتُقرّ.
/// وقيل حينها صراحةً إنّ <b>خضرتَه إشارةٌ ضعيفة</b>: يقيس الدورةَ العامّة ولا يقيس ميزةً
/// لا وجودَ لها. <b>وهذه الاختباراتُ هي ما يُحوّل تلك الخضرةَ إلى معنى.</b></para>
/// </summary>
public class LeaseTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _unitId;

    public LeaseTests()
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

    // ── تأجيرُ المؤجَّر ─────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: لا عقدان على وحدةٍ في مدّةٍ متداخلة.</b>
    ///
    /// <para>وهو أخطرُ عطلٍ في هذا القطاع — <b>عقدان موقَّعان على شقّةٍ واحدة</b>، ومستأجران
    /// يقفان على البابِ نفسِه، ونزاعٌ لا يحسمه النظام لأنّه هو مَن أنشأه.</para>
    /// </summary>
    [Fact]
    public async Task TwoLeasesOverlappingInTime_AreRefused()
    {
        await SetupAsync();
        var service = new LeaseService(_db);

        await service.ActivateAsync(Lease("L-1", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));

        var clash = Lease("L-2", new DateOnly(2026, 6, 1), new DateOnly(2027, 5, 31));
        var error = await Assert.ThrowsAsync<AccountingException>(() => service.ActivateAsync(clash));

        Assert.Contains("L-1", error.Message, StringComparison.Ordinal);
        Assert.Equal(LeaseStatus.Draft, clash.Status);
    }

    /// <summary>
    /// <b>وعقدٌ يبدأ بعد انتهاء سابقه يُقبل — والوحدةُ مؤجَّرةٌ الآن.</b>
    ///
    /// <para><b>وهذا هو القياسُ الذي يُفرّق بين تنفيذين</b>: فحصٌ بحالة الوحدة يرفض هذا
    /// العقدَ رفضاً باطلاً — <b>الوحدةُ «مؤجَّرة» ولا تعرف أنّ عقدَها ينتهي في ديسمبر</b>.
    /// فيُمنع التجديدُ المسبق، وهو أكثرُ ما تفعله الشركاتُ العقارية: توقيعُ عقد العام
    /// القادم قبل شهرين من انتهاء الحاليّ. <b>وبغير هذا القياس يمرّ التنفيذان معاً.</b></para>
    /// </summary>
    [Fact]
    public async Task ALeaseStartingAfterAnotherEnds_IsAccepted()
    {
        await SetupAsync();
        var service = new LeaseService(_db);

        await service.ActivateAsync(Lease("L-3", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        var renewal = Lease("L-4", new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31));

        await service.ActivateAsync(renewal);

        Assert.Equal(LeaseStatus.Active, renewal.Status);
        Assert.Equal(2, await _db.LeaseContracts.CountAsync(l => l.Status == LeaseStatus.Active));
    }

    /// <summary>
    /// <b>وعقدٌ يبدأ يومَ انتهاء سابقه مرفوض — ليلةً واحدةً تكفي.</b>
    ///
    /// <para><c>EndDate</c> يومٌ <b>داخلٌ</b> في العقد: المستأجرُ يسكن فيه ويُسلّم في
    /// صبيحته. فتوقيعُ عقدٍ يبدأ فيه يضع <b>مستأجرين في شقّةٍ واحدةٍ في يومٍ واحد</b>.
    /// <b>ومقارنةٌ بـ«أصغرَ من» تُمرّره</b> — وهو ما كان قبل هذا القياس.</para>
    /// </summary>
    [Fact]
    public async Task ALeaseStartingOnTheDayTheLastOneEnds_IsRefused()
    {
        await SetupAsync();
        var service = new LeaseService(_db);

        await service.ActivateAsync(Lease("L-13", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));

        await Assert.ThrowsAsync<AccountingException>(() => service.ActivateAsync(
            Lease("L-14", new DateOnly(2026, 12, 31), new DateOnly(2027, 12, 30))));
    }

    /// <summary><b>ووحدةٌ في الصيانة لا تُؤجَّر</b> — فالمستأجر يستلم ما لا يصلح للسكن.</summary>
    [Fact]
    public async Task AUnitUnderMaintenance_CannotBeLeased()
    {
        await SetupAsync();
        var unit = await _db.PropertyUnits.SingleAsync(u => u.Id == _unitId);
        unit.EnterMaintenance();
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new LeaseService(_db).ActivateAsync(
                Lease("L-5", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31))));

        Assert.Contains("الصيانة", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ووحدةٌ مؤجَّرةٌ لا تدخل الصيانة.</b>
    ///
    /// <para>المستأجرُ فيها، وإدخالُها الصيانةَ يُخفيها عن قوائم المؤجَّر <b>فيبدو العقارُ
    /// شاغراً وهو مسكون</b> — ويُعرض على مستأجرٍ آخر.</para>
    /// </summary>
    [Fact]
    public async Task ALeasedUnit_CannotEnterMaintenance()
    {
        await SetupAsync();
        await new LeaseService(_db).ActivateAsync(
            Lease("L-6", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));

        var unit = await _db.PropertyUnits.SingleAsync(u => u.Id == _unitId);

        Assert.Equal(UnitStatus.Leased, unit.Status);
        Assert.Throws<AccountingException>(unit.EnterMaintenance);
    }

    // ── الربطُ بالفوترة الدورية ─────────────────────────────────────────────

    /// <summary>
    /// <b>وتفعيلُ العقد يُنشئ خطةَ فوترةٍ تُولّد الإيجارَ شهراً بشهر.</b>
    ///
    /// <para><b>وهذا هو ما يجعل القطاعَ عاملاً لا مجرَّدَ جداول</b>: بغير الربط يبقى العقدُ
    /// صفّاً في قاعدةٍ، <b>والمحاسبُ يكتب فاتورةَ كلّ شهرٍ بيده لكلّ مستأجر</b> — ومَن
    /// نسي شهراً أضاع إيرادَه بلا أثرٍ يدلّ عليه.</para>
    /// </summary>
    [Fact]
    public async Task ActivatingALease_CreatesAPlanThatBillsTheRentMonthByMonth()
    {
        await SetupAsync();
        var lease = Lease("L-7", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        await new LeaseService(_db).ActivateAsync(lease);

        Assert.NotNull(lease.RecurringPlanId);

        var result = await new RecurringInvoiceService(_db)
            .GenerateDueAsync(_companyId, new DateOnly(2026, 3, 20));

        Assert.True(result.InvoicesCreated == 3,
            $"وُلِّد {result.InvoicesCreated}. وتعذّر: {string.Join(" · ", result.Refusals)}");

        var invoices = await _db.SalesInvoices.AsNoTracking()
            .Where(i => i.CompanyId == _companyId).OrderBy(i => i.InvoiceDate).ToListAsync();
        Assert.Equal(
            [new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1)],
            invoices.Select(i => i.InvoiceDate));
        Assert.Equal(5_000m, invoices[0].Subtotal);
    }

    /// <summary>
    /// <b>وإنهاءُ العقد يُوقف فوترتَه ويُخلي الوحدة.</b>
    ///
    /// <para><b>وإيقافُ الخطة أهمُّ من إنهاء العقد نفسِه</b>: عقدٌ يُنهى وخطتُه تعمل
    /// <b>يُفوتِر مستأجراً غادر شهراً بعد شهر</b> — ويُطالَب بمالٍ لا يستحقّ عليه، ويظهر
    /// في أعمار الديون مديناً وهو بريء.</para>
    /// </summary>
    [Fact]
    public async Task TerminatingALease_StopsTheBillingAndVacatesTheUnit()
    {
        await SetupAsync();
        var service = new LeaseService(_db);
        var lease = Lease("L-8", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        await service.ActivateAsync(lease);

        var billing = new RecurringInvoiceService(_db);
        Assert.Equal(2, (await billing.GenerateDueAsync(_companyId, new DateOnly(2026, 2, 10))).InvoicesCreated);

        await service.TerminateAsync(_companyId, lease.Id, new DateOnly(2026, 2, 15));

        Assert.Equal(0, (await billing.GenerateDueAsync(_companyId, new DateOnly(2026, 6, 1))).InvoicesCreated);
        Assert.Equal(2, await _db.SalesInvoices.CountAsync(i => i.CompanyId == _companyId));

        var unit = await _db.PropertyUnits.AsNoTracking().SingleAsync(u => u.Id == _unitId);
        Assert.Equal(UnitStatus.Vacant, unit.Status);
    }

    // ── التأمين: أمانةٌ لا إيراد ────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: التأمينُ المقبوض التزامٌ، ولا يمسّ الإيراد بريال.</b>
    ///
    /// <para>وقيدُه إيراداً <b>يُضخّم ربحَ المنشأة بمالٍ تردُّه</b>، فتُدفع عنه زكاةٌ
    /// وضريبةٌ ويُوزَّع ربحاً — <b>وهو من أشيع أخطاء المحاسبة العقارية</b>. وتركُه بلا قيدٍ
    /// أهون قليلاً وأسوأ كثيراً في الميزانية: نقدٌ بلا التزامٍ يقابله، <b>فتبدو المنشأة
    /// أغنى مما هي</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheReceivedDeposit_IsALiabilityAndNeverTouchesRevenue()
    {
        await SetupAsync();
        var lease = await ActivatedLeaseWithDepositAsync("L-9", 10_000m);

        await new SecurityDepositService(_db)
            .ReceiveAsync(_companyId, lease.Id, new DateOnly(2026, 1, 5), "admin");

        Assert.Equal(-10_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.TenantSecurityDeposits));
        Assert.Equal(10_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.SalesRevenue));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.OtherIncome));
    }

    /// <summary>
    /// <b>وعند الردّ: الالتزامُ يُغلق كاملاً، والمخصومُ وحده يصير إيراداً.</b>
    ///
    /// <para>عشرةُ آلافٍ تأميناً وألفان خصماً لتلف: <b>يخرج ثمانيةٌ نقداً، ويُعترف بألفين
    /// دخلاً، ويصفر الالتزام</b>. وإغلاقُه بالمدفوع وحده يترك ألفين معلّقين إلى الأبد —
    /// <b>التزامٌ لمستأجرٍ غادر ولا أحدَ يطالب به</b>، ويتراكم عقداً بعد عقد.</para>
    /// </summary>
    [Fact]
    public async Task ReturningTheDeposit_ClosesTheLiabilityAndOnlyTheForfeitedPartIsIncome()
    {
        await SetupAsync();
        var lease = await ActivatedLeaseWithDepositAsync("L-10", 10_000m);
        var deposits = new SecurityDepositService(_db);

        await deposits.ReceiveAsync(_companyId, lease.Id, new DateOnly(2026, 1, 5), "admin");
        await deposits.ReturnAsync(_companyId, lease.Id, new DateOnly(2026, 6, 30), 2_000m, "admin");

        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.TenantSecurityDeposits));
        Assert.Equal(2_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CashAtBanks));
        Assert.Equal(-2_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.OtherIncome));
    }

    /// <summary><b>ولا يُقبض التأمينُ مرّتين</b> — والثانيةُ التزامٌ لا يُغلقه ردٌّ واحد.</summary>
    [Fact]
    public async Task TheDeposit_IsNotReceivedTwice()
    {
        await SetupAsync();
        var lease = await ActivatedLeaseWithDepositAsync("L-11", 5_000m);
        var deposits = new SecurityDepositService(_db);

        await deposits.ReceiveAsync(_companyId, lease.Id, new DateOnly(2026, 1, 5), "admin");

        await Assert.ThrowsAsync<AccountingException>(
            () => deposits.ReceiveAsync(_companyId, lease.Id, new DateOnly(2026, 1, 6), "admin"));

        Assert.Equal(-5_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.TenantSecurityDeposits));
    }

    /// <summary>
    /// <b>وخصمٌ أكبرُ من التأمين مرفوض</b> — والزائدُ مطالبةٌ على المستأجر لا خصمٌ من أمانة.
    ///
    /// <para>وقبولُه يُخرج التزاماً <b>مديناً</b>: أصلٌ موهومٌ في الميزانية لا يقابله حقّ.</para>
    ///
    /// <para><b>ويُقاس الرفضُ بنصّه لا بوقوعه</b>: كُتب القياسُ أوّلاً بـ«يرمي استثناءً»
    /// <b>فنجَت طفرةُ نزعِ الحارس</b> — لأنّ القيدَ حينها يختلّ من تلقاء نفسه (مدينٌ خمسةُ
    /// آلافٍ ودائنٌ سبعة)، فيرمي حارسُ التوازن بدل حارس التأمين. <b>فالاختبارُ كان يمرّ
    /// بالمصادفة</b>، ولو صار الخصمُ من حسابٍ آخر يوازن القيدَ لمرّ الخطأ صامتاً. وربطُه
    /// بنصّ الرفض يجعله يقيس ما زعم أنّه يقيسه.</para>
    /// </summary>
    [Fact]
    public async Task ForfeitingMoreThanTheDeposit_IsRefused()
    {
        await SetupAsync();
        var lease = await ActivatedLeaseWithDepositAsync("L-12", 5_000m);
        var deposits = new SecurityDepositService(_db);
        await deposits.ReceiveAsync(_companyId, lease.Id, new DateOnly(2026, 1, 5), "admin");

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => deposits.ReturnAsync(_companyId, lease.Id, new DateOnly(2026, 6, 30), 7_000m, "admin"));

        Assert.Contains("المخصوم أكبر من التأمين", error.Message, StringComparison.Ordinal);
        Assert.Equal(-5_000m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.TenantSecurityDeposits));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.OtherIncome));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private LeaseContract Lease(string number, DateOnly start, DateOnly end, decimal deposit = 0m) =>
        LeaseContract.Create(_companyId, _unitId, _customerId, number, start, end,
            rentPerPeriod: 5_000m, securityDeposit: deposit);

    private async Task<LeaseContract> ActivatedLeaseWithDepositAsync(string number, decimal deposit)
    {
        var lease = Lease(number, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), deposit);
        await new LeaseService(_db).ActivateAsync(lease);
        return lease;
    }

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة العقارات", "310123456700077", BusinessProfile.RealEstate);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "مستأجر", "311111111100003");
        _db.Customers.Add(customer);

        var property = Property.Create(_companyId, "P-1", "برج الياسمين", PropertyKind.Building, "الرياض");
        _db.Properties.Add(property);
        await _db.SaveChangesAsync();

        var unit = PropertyUnit.Create(_companyId, property.Id, "101",
            areaSquareMeters: 120m, bedrooms: 3, listedAnnualRent: 60_000m);
        _db.PropertyUnits.Add(unit);
        await _db.SaveChangesAsync();

        _customerId = customer.Id;
        _unitId = unit.Id;
    }
}
