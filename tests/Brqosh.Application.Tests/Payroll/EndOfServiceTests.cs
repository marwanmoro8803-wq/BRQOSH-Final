using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// (CEEF-230 · ط2-٣) <b>مكافأةُ نهاية الخدمة — نظامُ العمل، المادتان 84 و85.</b>
///
/// <para><b>ما كان:</b> حسابُ المخصّص <c>2202</c> في شجرة الحسابات <b>ولا تكتب فيه شيفرةٌ
/// قطّ</b>. فالمنشأةُ تحمل التزاماً نظامياً يتراكم كلَّ شهر <b>ولا يظهر في ميزانيّتها
/// بريالٍ واحد</b> — وقوائمُ سنواتٍ مضت تُظهر ربحاً أكبرَ من الحقّ، وأوّلُ استحقاقٍ
/// يُفاجئ المالكَ بمبلغٍ لم يُخصّص له.</para>
/// </summary>
public class EndOfServiceTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public EndOfServiceTests()
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

    // ── الصيغة ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>نصفُ شهرٍ للخمس الأولى، وشهرٌ لما بعدها (المادة 84).</b>
    ///
    /// <para>وبأجرِ 10,000: ثلاثُ سنواتٍ ⟵ 15,000 · وخمسٌ ⟵ 25,000 · <b>وسبعٌ ⟵ 45,000</b>
    /// (25,000 للخمس + 20,000 للسنتين). <b>وحسابٌ يُطبّق نصفَ شهرٍ على المدّة كلِّها
    /// يُخصّص 35,000 — نقصُ عشرة آلافٍ لموظّفٍ واحد.</b></para>
    /// </summary>
    [Theory]
    [InlineData(3, 15_000)]
    [InlineData(5, 25_000)]
    [InlineData(7, 45_000)]
    [InlineData(10, 75_000)]
    public void TheEntitlement_IsHalfMonthForFiveYearsThenFull(int years, decimal expected) =>
        Assert.Equal(expected, EndOfServiceCalculator.FullEntitlement(10_000m, years));

    /// <summary>
    /// <b>والكسورُ بنسبتها</b> — فلا تُهدَر أشهرُ السنة الأخيرة.
    ///
    /// <para>سنتان ونصفٌ بأجرِ 10,000 ⟵ 12,500. <b>وتقريبٌ إلى سنتين يحرم الموظّفَ
    /// 2,500</b> ويُنقص التزامَ المنشأة بالقدر نفسِه.</para>
    /// </summary>
    [Fact]
    public void PartialYears_CountProRata() =>
        Assert.Equal(12_500m, EndOfServiceCalculator.FullEntitlement(10_000m, 2.5m));

    /// <summary>
    /// <b>وسُلَّمُ الاستقالة يغيّر المستحقّ جذرياً (المادة 85).</b>
    ///
    /// <para>دون سنتين لا شيء · ومن سنتين إلى خمسٍ الثلث · ومن خمسٍ إلى عشرٍ الثلثان ·
    /// وعشرٌ فأكثر كاملاً. <b>وحسابٌ يتجاهل السببَ يُخصّص أضعافَ الواجب لمنشأةٍ موظّفوها
    /// يستقيلون.</b></para>
    /// </summary>
    [Theory]
    [InlineData(1.5, 0)]
    [InlineData(2, 1)]
    [InlineData(4.9, 1)]
    [InlineData(5, 2)]
    [InlineData(9.9, 2)]
    [InlineData(10, 3)]
    public void TheResignationScale_FollowsArticle85(decimal years, int thirds) =>
        Assert.Equal(thirds / 3m,
            EndOfServiceCalculator.EntitlementFactor(EndOfServiceReason.Resignation, years));

    /// <summary><b>والإنهاءُ من صاحب العمل مستحقٌّ كاملٌ في كلّ مدّة.</b></summary>
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void EmployerTermination_IsAlwaysFull(decimal years) =>
        Assert.Equal(1m,
            EndOfServiceCalculator.EntitlementFactor(EndOfServiceReason.EmployerTermination, years));

    // ── التخصيص ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ: الالتزامُ يظهر في الميزانية.</b>
    ///
    /// <para>وكان صفراً أبداً مهما طالت الخدمة.</para>
    /// </summary>
    [Fact]
    public async Task TheProvision_AppearsOnTheBooks()
    {
        await SetupAsync();
        await EmployeeAsync("E-1", "1011111111", hire: new DateOnly(2023, 1, 1), wage: 10_000m);

        var result = await new EndOfServiceAccrualService(_db)
            .RunMonthlyAsync(_companyId, new DateOnly(2026, 1, 1), "admin");

        // ‏ثلاثُ سنواتٍ بأجرِ 10,000 ⟵ نصفُ شهرٍ لكلٍّ ≈ 15,000.
        //
        // ‏**و«تقريباً» مقصودة**: الخدمةُ تُقاس بالأيّام لا بالسنوات الكاملة، ومن 2023-01-01
        // ‏إلى 2026-01-01 ألفٌ وستّةٌ وتسعون يوماً = 3.0007 سنة. **والدقّةُ اليومية هي
        // ‏الصواب النظاميّ** — فموظّفٌ خدم ثلاث سنواتٍ ويوماً يستحقّ عن اليوم.
        Assert.InRange(result.Movement, 15_000m, 15_010m);
        Assert.Equal(-result.Movement, await BalanceAsync("2202"));   // دائنُ الطبيعة
        Assert.Equal(result.Movement, await BalanceAsync("5219"));
    }

    /// <summary>
    /// <b>والتخصيصُ فرقٌ لا تراكم.</b>
    ///
    /// <para>الشهرُ الثاني يُقيّد <b>الزيادةَ وحدها</b> — وتراكمٌ أعمى يُضاعف الالتزامَ
    /// كلَّ شهر.</para>
    /// </summary>
    [Fact]
    public async Task TheSecondMonth_BooksOnlyTheIncrement()
    {
        await SetupAsync();
        await EmployeeAsync("E-2", "1022222222", hire: new DateOnly(2023, 1, 1), wage: 12_000m);
        var service = new EndOfServiceAccrualService(_db);

        var first = await service.RunMonthlyAsync(_companyId, new DateOnly(2026, 1, 31), "admin");
        var second = await service.RunMonthlyAsync(_companyId, new DateOnly(2026, 2, 28), "admin");

        Assert.True(first.Movement > second.Movement, $"الأوّل {first.Movement} والثاني {second.Movement}");
        Assert.True(second.Movement > 0m);
        Assert.Equal(-(first.Movement + second.Movement), await BalanceAsync("2202"));
    }

    /// <summary>
    /// <b>وترقيةٌ تُعيد تقدير الالتزام كلِّه.</b>
    ///
    /// <para>فالأجرُ المعتبَر هو <b>الأخير</b> لا المتوسّط — <b>وهو سببُ كون التخصيص
    /// إعادةَ حسابٍ لا تراكماً</b>.</para>
    /// </summary>
    [Fact]
    public async Task ARaise_RestatesTheWholeLiability()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-3", "1033333333",
            hire: new DateOnly(2022, 1, 1), wage: 10_000m);
        var service = new EndOfServiceAccrualService(_db);

        await service.RunMonthlyAsync(_companyId, new DateOnly(2026, 1, 31), "admin");
        var before = await BalanceAsync("2202");

        employee.SetPay(basic: 20_000m, housing: 0m, transport: 0m);
        await _db.SaveChangesAsync();

        var after = await service.RunMonthlyAsync(_companyId, new DateOnly(2026, 2, 28), "admin");

        // ‏الأجرُ تضاعف، فالالتزامُ يتضاعف — والحركةُ تقارب الرصيدَ السابق.
        Assert.True(after.Movement > -before * 0.9m,
            $"الحركة {after.Movement} والرصيد السابق {-before}");
    }

    /// <summary>
    /// <b>وتصحيحٌ داخل الشهر نفسِه يُقيَّد.</b>
    ///
    /// <para>وهذا ما كان يمنعه حارسُ «مرّةً في الشهر» الذي أزلتُه: مَن رفع راتباً ثمّ أعاد
    /// التشغيل <b>لا يرى أثراً ويظنّ الالتزامَ محدَّثاً وهو قديم</b>. <b>والطفرةُ هي التي
    /// أخرجت ذلك</b> — نجَت من نزع الحارس، فسألتُ: وماذا يفعل إذاً؟</para>
    /// </summary>
    [Fact]
    public async Task ACorrectionWithinTheSameMonth_IsBooked()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-6", "1066666666",
            hire: new DateOnly(2022, 1, 1), wage: 10_000m);
        var service = new EndOfServiceAccrualService(_db);
        var asOf = new DateOnly(2026, 5, 31);

        await service.RunMonthlyAsync(_companyId, asOf, "admin");

        employee.SetPay(basic: 14_000m, housing: 0m, transport: 0m);
        await _db.SaveChangesAsync();

        var correction = await service.RunMonthlyAsync(_companyId, asOf, "admin");

        Assert.True(correction.Movement > 0m,
            "تصحيحٌ داخل الشهر لم يُقيَّد — والالتزامُ يبقى بأجرٍ قديم.");
        Assert.NotNull(correction.JournalEntryId);
    }

    /// <summary>
    /// <b>وتشغيلٌ ثانٍ بلا تغييرٍ لا يُقيّد شيئاً.</b>
    ///
    /// <para><b>والمُطاوَعةُ هنا بالبناء لا بحارس</b>: التخصيصُ فرقٌ، والفرقُ صفرٌ حين
    /// لا يتغيّر شيء.</para>
    /// </summary>
    [Fact]
    public async Task RunningTwiceInOneMonth_ChangesNothing()
    {
        await SetupAsync();
        await EmployeeAsync("E-4", "1044444444", hire: new DateOnly(2024, 1, 1), wage: 8_000m);
        var service = new EndOfServiceAccrualService(_db);

        var first = await service.RunMonthlyAsync(_companyId, new DateOnly(2026, 3, 31), "admin");
        var again = await service.RunMonthlyAsync(_companyId, new DateOnly(2026, 3, 31), "admin");

        Assert.True(first.Movement > 0m);
        Assert.Equal(0m, again.Movement);
        Assert.Null(again.JournalEntryId);
    }

    /// <summary>
    /// <b>ومَن انتهت خدمتُه يبقى مخصَّصاً حتى تُصفَّى مستحقّاتُه.</b>
    ///
    /// <para><b>وإسقاطُه فورَ الإيقاف يعكس التزاماً ما زال قائماً ولم يُدفع</b> — فيظهر
    /// ربحٌ من موظّفٍ لم يقبض حقَّه بعد.</para>
    /// </summary>
    [Fact]
    public async Task ATerminatedEmployee_StaysProvisionedUntilSettled()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-5", "1055555555",
            hire: new DateOnly(2022, 1, 1), wage: 10_000m);
        var service = new EndOfServiceAccrualService(_db);

        await service.RunMonthlyAsync(_companyId, new DateOnly(2026, 1, 31), "admin");
        var before = await BalanceAsync("2202");

        employee.Terminate(new DateOnly(2026, 2, 15));
        await _db.SaveChangesAsync();

        await service.RunMonthlyAsync(_companyId, new DateOnly(2026, 3, 31), "admin");

        // ‏الرصيدُ لم يُعكس — بل ثبت عند استحقاقه يومَ الانتهاء.
        Assert.True(await BalanceAsync("2202") <= before,
            "عُكس مخصّصُ موظّفٍ انتهت خدمتُه ولم تُصفَّ مستحقّاتُه.");
    }

    // ── (CEEF-232 · ر-١) التصفية — الطرفُ الذي كان مفقوداً ──────────────────

    /// <summary>
    /// <b>القياسُ الحاكم (ر-١): التصفيةُ تُبرئ المخصّص وتُنشئ المستحقّ.</b>
    ///
    /// <para><b>وكان المستودعُ لا يملك مساراً واحداً يُنقص <c>2202</c>:</b> الخدمةُ الوحيدةُ
    /// <c>RunMonthlyAsync</c> تبني ولا تُبرئ. <b>فالالتزامُ ينمو بلا حدّ</b> ويحمل مستحقّاتِ
    /// مَن غادروا منذ سنوات، <b>ومستحقُّ الموظّف يُدفع خارجَ الدفاتر أو لا يُدفع</b>.</para>
    ///
    /// <para>أربعُ سنواتٍ بأجرِ 10,000 وإنهاءٌ من صاحب العمل ⟵ <b>المستحقُّ 20,000</b>،
    /// <b>والمخصّصُ يُبرَأ بها كاملةً</b> ويصير الرصيدُ صفراً.</para>
    /// </summary>
    [Fact]
    public async Task Settlement_RelievesTheProvision_AndRaisesThePayable()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-9", "1099999999",
            hire: new DateOnly(2022, 1, 1), wage: 10_000m);

        await new EndOfServiceAccrualService(_db)
            .RunMonthlyAsync(_companyId, new DateOnly(2026, 1, 1), "admin");

        employee.Terminate(new DateOnly(2026, 1, 1));
        await _db.SaveChangesAsync();

        var result = await new EndOfServiceSettlementService(_db).SettleAsync(
            _companyId, employee.Id, EndOfServiceReason.EmployerTermination,
            new DateOnly(2026, 1, 5), "admin");

        Assert.Equal(20_000m, result.Benefit);
        Assert.Equal(0m, result.Adjustment);

        // ‏**والمخصّصُ أُبرئ** — والرصيدُ دائنُ الطبيعة فالصفرُ إبراءٌ تامّ.
        Assert.Equal(0m, await BalanceAsync("2202"));

        // ‏**والمستحقُّ صار على «رواتب مستحقة»** — يخرج بسند صرفٍ له دورتُه،
        // ‏لا من الصندوق بباب ثانٍ.
        Assert.Equal(-20_000m, await BalanceAsync("2108"));
    }

    /// <summary>
    /// <b>والاستقالةُ تُصفَّى بسُلَّمها — والفائضُ يعود إلى المصروف لا إلى الأرباح.</b>
    ///
    /// <para><b>وهنا يظهر لماذا يُسأل عن السبب عند التصفية وحدَها:</b> التخصيصُ يجهل كيف
    /// ستنتهي الخدمة فيحتاط بالكامل (20,000)، <b>والمستقيلُ بعد أربع سنواتٍ يستحقّ الثلث</b>
    /// (المادة 85) — <b>6,666.67</b>. والفرقُ 13,333.33 <b>مخصّصٌ زائدٌ يُردّ إلى المصروف</b>.</para>
    ///
    /// <para><b>وإدخالُه في الإيرادات يُضخّم أرباحَ سنةٍ باستقالةِ موظّف</b> — ويُوزَّع
    /// ويُزكّى على ربحٍ لم يقع.</para>
    /// </summary>
    [Fact]
    public async Task AResignation_SettlesOnItsScale_AndReturnsTheExcessToExpense()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-10", "1010101010",
            hire: new DateOnly(2022, 1, 1), wage: 10_000m);

        await new EndOfServiceAccrualService(_db)
            .RunMonthlyAsync(_companyId, new DateOnly(2026, 1, 1), "admin");
        var expenseBefore = await BalanceAsync("5219");

        employee.Terminate(new DateOnly(2026, 1, 1));
        await _db.SaveChangesAsync();

        var result = await new EndOfServiceSettlementService(_db).SettleAsync(
            _companyId, employee.Id, EndOfServiceReason.Resignation,
            new DateOnly(2026, 1, 5), "admin");

        Assert.Equal(6_666.67m, result.Benefit);
        Assert.Equal(13_333.33m, result.Adjustment);

        Assert.Equal(0m, await BalanceAsync("2202"));
        Assert.Equal(-6_666.67m, await BalanceAsync("2108"));

        // ‏**والفائضُ خُصم من المصروف نفسِه** — لا من حسابِ إيراد.
        Assert.Equal(expenseBefore - 13_333.33m, await BalanceAsync("5219"));
    }

    /// <summary>
    /// <b>والتخصيصُ التالي لا يُعيد بناءَ ما أُبرئ — وهذا شرطُ الإغلاق لا تتمّتُه.</b>
    ///
    /// <para><b>وبغيره تُبطل الخدمةُ الشهريّةُ التصفيةَ في الشهر الذي يليها</b>: ترى
    /// المخصّصَ ناقصاً فتُعيد بناءه، <b>فيُقيَّد مصروفٌ ثانٍ لمستحقٍّ دُفع</b> ويعود
    /// الالتزامُ الذي أُبرئ للتوّ — <b>ويبقى الحسابُ ينمو كما كان، والتصفيةُ زينة</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheNextAccrual_DoesNotReprovisionASettledEmployee()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-11", "1011111111",
            hire: new DateOnly(2022, 1, 1), wage: 10_000m);

        var accrual = new EndOfServiceAccrualService(_db);
        await accrual.RunMonthlyAsync(_companyId, new DateOnly(2026, 1, 1), "admin");

        employee.Terminate(new DateOnly(2026, 1, 1));
        await _db.SaveChangesAsync();

        await new EndOfServiceSettlementService(_db).SettleAsync(
            _companyId, employee.Id, EndOfServiceReason.EmployerTermination,
            new DateOnly(2026, 1, 5), "admin");

        await accrual.RunMonthlyAsync(_companyId, new DateOnly(2026, 2, 28), "admin");

        Assert.Equal(0m, await BalanceAsync("2202"));
    }

    /// <summary>
    /// <b>ولا تُصفّى خدمةٌ قائمة، ولا تُصفّى مرّتان.</b>
    ///
    /// <para><b>والأولى تُسقط التزاماً ما زال ينمو</b>، <b>والثانيةُ تُنشئ مستحقّاً ثانياً
    /// لحقٍّ واحد</b> وتُدين المخصّصَ بضعف ما فيه — فيصير الحسابُ مديناً، وهو التزامٌ
    /// بطبيعته.</para>
    /// </summary>
    [Fact]
    public async Task Settlement_RefusesAnActiveEmployee_AndRefusesToRepeat()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-12", "1012121212",
            hire: new DateOnly(2022, 1, 1), wage: 10_000m);
        var service = new EndOfServiceSettlementService(_db);

        await Assert.ThrowsAsync<AccountingException>(() => service.SettleAsync(
            _companyId, employee.Id, EndOfServiceReason.EmployerTermination,
            new DateOnly(2026, 1, 5), "admin"));

        employee.Terminate(new DateOnly(2026, 1, 1));
        await _db.SaveChangesAsync();

        await service.SettleAsync(_companyId, employee.Id,
            EndOfServiceReason.EmployerTermination, new DateOnly(2026, 1, 5), "admin");

        await Assert.ThrowsAsync<AccountingException>(() => service.SettleAsync(
            _companyId, employee.Id, EndOfServiceReason.EmployerTermination,
            new DateOnly(2026, 1, 6), "admin"));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<Employee> EmployeeAsync(string code, string nationalId, DateOnly hire, decimal wage)
    {
        var employee = Employee.Create(_companyId, code, "موظّف", nationalId,
            EmployeeNationality.Saudi, "محاسب", hire);
        employee.SetPay(basic: wage, housing: 0m, transport: 0m);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        return employee;
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
            .InitializeAsync("منشأة المكافأة", "310123456700066", BusinessProfile.General);
        _companyId = company.Id;
    }
}
