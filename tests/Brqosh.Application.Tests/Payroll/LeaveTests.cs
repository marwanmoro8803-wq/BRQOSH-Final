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
/// (CEEF-233 · ز) <b>الإجازاتُ والغياب — البندُ الذي لم يكن له كيانٌ ولا خدمةٌ ولا شاشة.</b>
///
/// <para><b>ما كان:</b> المنشأةُ تمنح إجازاتٍ وتُسجّلها في جدولٍ خارجيّ. <b>ورصيدُ
/// الإجازات دَينٌ نقديٌّ بنصّ المادة 111</b> — يُدفع عند انتهاء الخدمة عمّا لم يُستعمل —
/// <b>ولا يظهر في الميزانيّة بريال</b>. فتُقفل السنةُ على ربحٍ أكبرَ من الحقّ،
/// <b>وتُوزَّع أرباحٌ وتُدفع زكاةٌ على التزامٍ قائم</b>.</para>
///
/// <para><b>والأثرُ بالأرقام:</b> مئةُ موظّفٍ بأجرِ عشرةِ آلافٍ ورصيدِ خمسةَ عشرَ يوماً ⟵
/// <b>خمسُمئة ألفِ ريالٍ التزامٌ غيرُ مُعلَن</b> — لا يُكتشف إلّا حين يغادر أوّلُهم.</para>
///
/// <para><b>وهويّةُ هـ-٢٦:</b> <c>الرصيد = المستحقُّ المتراكم − المُستهلَك من السنويّة</c>.
/// <b>وهويّةُ هـ-٢٧:</b> <c>رصيدُ 2203 = مجموعُ (رصيدِ أيّامِ كلّ موظّفٍ × أجرِه اليوميّ)</c>.</para>
/// </summary>
public class LeaveTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    private static readonly DateOnly Hire = new(2022, 1, 1);
    private static readonly DateOnly AsOf = new(2026, 1, 1);

    public LeaveTests()
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
    /// <b>القياسُ الحاكم (هـ-٢٦): الرصيدُ = المتراكمُ − المُستهلَك.</b>
    ///
    /// <para>أربعُ سنواتِ خدمةٍ ⟵ <b>أربعةٌ وثمانون يوماً</b> مستحقّة. وإجازةٌ سنويّةٌ
    /// عشرةُ أيّامٍ معتمدة ⟵ <b>الرصيدُ أربعةٌ وسبعون</b>.</para>
    /// </summary>
    [Fact]
    public async Task TheBalance_IsAccrualMinusApprovedAnnualLeave()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-1", "1011111111", 9_000m);

        await ApprovedLeaveAsync(employee.Id, LeaveType.Annual,
            new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 10));

        var balance = await new LeaveBalanceQuery(_db).ForEmployeeAsync(_companyId, employee.Id, AsOf);

        Assert.Equal(84m, Math.Round(balance.AccruedDays));
        Assert.Equal(10m, balance.TakenDays);
        Assert.Equal(74m, Math.Round(balance.BalanceDays));
    }

    /// <summary>
    /// <b>والمرضيّةُ لا تُخصم من الرصيد السنويّ — حقٌّ لا يأكل حقّاً.</b>
    ///
    /// <para><b>وخصمُها يُخرج الموظّفَ بلا إجازةٍ سنويّةٍ لأنّه مرض</b> — وهما حقّان
    /// مستقلّان بنصّ النظام (المادتان 109 و117).</para>
    /// </summary>
    [Fact]
    public async Task SickLeave_DoesNotConsumeTheAnnualBalance()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-2", "1022222222", 9_000m);

        await ApprovedLeaveAsync(employee.Id, LeaveType.Sick,
            new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 20));

        var balance = await new LeaveBalanceQuery(_db).ForEmployeeAsync(_companyId, employee.Id, AsOf);

        Assert.Equal(0m, balance.TakenDays);
        Assert.Equal(balance.AccruedDays, balance.BalanceDays);
    }

    /// <summary>
    /// <b>(هـ-٢٧) والالتزامُ يبلغ الأستاذ — وكان لا يبلغه أبداً.</b>
    ///
    /// <para>رصيدٌ 84 يوماً بأجرٍ 9,000 (اليوميُّ 300) ⟵ <b>25,200 مخصّصاً على 2203</b>.
    /// <b>وكان الحسابُ نفسُه غيرَ موجودٍ في الشجرة.</b></para>
    /// </summary>
    [Fact]
    public async Task TheProvision_ReachesTheLedger()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-3", "1033333333", 9_000m);

        var result = await new LeaveAccrualService(_db).RunMonthlyAsync(_companyId, AsOf, "admin");

        var balance = await new LeaveBalanceQuery(_db).ForEmployeeAsync(_companyId, employee.Id, AsOf);
        var expected = LeaveEntitlementCalculator.LeaveCompensation(9_000m, balance.BalanceDays);

        Assert.Equal(expected, result.Movement);
        Assert.Equal(-expected, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.LeaveProvision));
        Assert.Equal(expected, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.LeaveExpense));
    }

    /// <summary>
    /// <b>والتخصيصُ فرقٌ لا تراكم — فتشغيلٌ ثانٍ بلا تغييرٍ لا يُقيّد شيئاً.</b>
    ///
    /// <para><b>وتراكمٌ أعمى يُضاعف الالتزامَ كلَّ شهر</b> — ويُظهر منشأةً مدينةً بأضعاف
    /// ما عليها.</para>
    /// </summary>
    [Fact]
    public async Task RunningTheAccrualTwice_PostsNothingTheSecondTime()
    {
        await SetupAsync();
        await EmployeeAsync("E-4", "1044444444", 9_000m);

        var service = new LeaveAccrualService(_db);
        var first = await service.RunMonthlyAsync(_companyId, AsOf, "admin");
        var second = await service.RunMonthlyAsync(_companyId, AsOf, "admin");

        Assert.True(first.Movement > 0m);
        Assert.Equal(0m, second.Movement);
        Assert.Null(second.JournalEntryId);
    }

    /// <summary>
    /// <b>(المادة 111) وبدلُ الإجازة يُدفع مع التصفية — وهذا هو الشقُّ الذي كان مفقوداً.</b>
    ///
    /// <para><b>وتصفيةٌ تُبرئ مخصّصَ المكافأة وتترك مخصّصَ الإجازات قائماً</b> تُبقي
    /// التزاماً خالداً تجاه موظّفٍ رحل، <b>وتدفع له ناقصاً فيُطالِب</b>.</para>
    /// </summary>
    [Fact]
    public async Task Settlement_PaysTheUnusedLeave_AndClearsItsProvision()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-5", "1055555555", 9_000m);

        await new LeaveAccrualService(_db).RunMonthlyAsync(_companyId, AsOf, "admin");
        await new EndOfServiceAccrualService(_db).RunMonthlyAsync(_companyId, AsOf, "admin");

        employee.Terminate(AsOf);
        await _db.SaveChangesAsync();

        var result = await new EndOfServiceSettlementService(_db).SettleAsync(
            _companyId, employee.Id, EndOfServiceReason.EmployerTermination, AsOf, "admin");

        // ‏أربعُ سنواتٍ ⟵ 84 يوماً · 84 × 300 = 25,200.
        Assert.Equal(84m, Math.Round(result.UnusedLeaveDays));
        Assert.Equal(25_200m, result.LeavePay);

        // ‏**ومخصّصُ الإجازات أُبرئ** — فلا يبقى التزامٌ تجاه مَن رحل.
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.LeaveProvision));

        // ‏**والمستحقُّ يشمل المكافأةَ والبدل معاً** على «رواتب مستحقة».
        Assert.Equal(-(result.Benefit + result.LeavePay), await BalanceAsync("2108"));
    }

    /// <summary>
    /// <b>والغيابُ بلا أجرٍ يُنقص المصروف — لا الصافيَ وحدَه.</b>
    ///
    /// <para><b>وهو الفرقُ عن خصم السُّلفة</b>: السُّلفةُ سدادُ دَينٍ والأجرُ استُحقّ كاملاً؛
    /// <b>والغيابُ أجرٌ لم يُستحقّ — فالعملُ لم يقع</b>. <b>وخصمُه من الصافي وحدَه يُبقي
    /// مصروفَ الرواتب مُضخَّماً بأجرِ أيّامٍ لم تُعمَل.</b></para>
    ///
    /// <para>عشرةُ أيّامٍ بلا أجرٍ من أجرِ 9,000 ⟵ <b>خصمُ 3,000</b>، فالمصروفُ 6,000.</para>
    /// </summary>
    [Fact]
    public async Task UnpaidLeave_ReducesTheWageExpense_NotOnlyTheNet()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-6", "1066666666", 9_000m);

        await ApprovedLeaveAsync(employee.Id, LeaveType.Unpaid,
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 10));

        var run = await new PayrollRunBuilder(_db)
            .BuildAsync(_companyId, 2026, 3, new DateOnly(2026, 3, 31));

        var line = run.Lines.Single();
        Assert.Equal(3_000m, line.AbsenceDeduction);
        Assert.Equal(9_000m, line.FullGrossPay);
        Assert.Equal(6_000m, line.GrossPay);
    }

    /// <summary>
    /// <b>وإجازةٌ تعبر حدَّ الشهر تُخصم بالتقاطع لا بمدّتها كلِّها.</b>
    ///
    /// <para>من ٢٥ مارس إلى ٥ أبريل: <b>سبعةُ أيّامٍ من مارس وخمسةٌ من أبريل</b>.
    /// <b>وخصمُ الاثني عشر من مارس يُصفّر أجراً لم يُغَب كلُّه</b>، وخصمُها في الشهرين
    /// كاملةً يخصم أربعةً وعشرين عن اثني عشر.</para>
    /// </summary>
    [Fact]
    public async Task LeaveSpanningTwoMonths_IsDeductedByOverlap()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-7", "1077777777", 9_000m);

        await ApprovedLeaveAsync(employee.Id, LeaveType.Unpaid,
            new DateOnly(2026, 3, 25), new DateOnly(2026, 4, 5));

        var march = await new PayrollRunBuilder(_db)
            .BuildAsync(_companyId, 2026, 3, new DateOnly(2026, 3, 31));
        var april = await new PayrollRunBuilder(_db)
            .BuildAsync(_companyId, 2026, 4, new DateOnly(2026, 4, 30));

        Assert.Equal(7m * 300m, march.Lines.Single().AbsenceDeduction);
        Assert.Equal(5m * 300m, april.Lines.Single().AbsenceDeduction);
    }

    /// <summary>
    /// <b>ولا تُعتمد سنويّةٌ تتجاوز الرصيد، ولا تتداخل إجازتان.</b>
    ///
    /// <para><b>وبغير الأوّل يُمنح ما لم يُستحقّ</b> فيصير الرصيدُ سالباً ويُطالَب الموظّفُ
    /// عند المغادرة بردّ أيّامٍ أُذن له فيها. <b>وبغير الثاني يُخصم يومٌ مرّتين.</b></para>
    /// </summary>
    [Fact]
    public async Task Approval_RefusesOverdraft_AndOverlap()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-8", "1088888888", 9_000m);
        var service = new LeaveRequestService(_db);

        // ‏رصيدُه نحو 84 يوماً — وطلبُ 200 يتجاوزه.
        var greedy = await service.SubmitAsync(_companyId, employee.Id, LeaveType.Annual,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 8, 19));
        await Assert.ThrowsAsync<AccountingException>(() =>
            service.ApproveAsync(_companyId, greedy.Id, AsOf, "admin"));

        await Assert.ThrowsAsync<AccountingException>(() =>
            service.SubmitAsync(_companyId, employee.Id, LeaveType.Annual,
                new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5)));
    }

    /// <summary>
    /// <b>وتصفيةٌ بلا تخصيصٍ سابق لا تجعل الالتزامَ مديناً.</b>
    ///
    /// <para><b>وحسابُ <c>2203</c> مجمَّعٌ لا مفرَّدٌ بالموظّف</b>، وقد لا يكون التخصيصُ
    /// الشهريُّ شُغِّل أصلاً — <b>ومنشأةٌ اشترت النظامَ أمسَ وصفّت موظّفاً اليوم</b>.
    /// <b>وإدانةُ المخصّص بالبدل كاملاً تجعل التزاماً يصير مديناً</b>: رصيدٌ مدينٌ على
    /// حساب التزامٍ يُقرأ في الميزانيّة <b>أصلاً موجباً لا وجودَ له</b>.</para>
    ///
    /// <para><b>وما زاد عن المخصَّص مصروفُ الفترة</b> — أجرُ إجازةٍ لم يُحتَط لها،
    /// يقع في السنة التي انكشف فيها.</para>
    /// </summary>
    [Fact]
    public async Task SettlingWithoutAnyAccrual_ChargesExpense_AndNeverDebitsTheLiability()
    {
        await SetupAsync();
        var employee = await EmployeeAsync("E-9", "1099999999", 9_000m);

        employee.Terminate(AsOf);
        await _db.SaveChangesAsync();

        var result = await new EndOfServiceSettlementService(_db).SettleAsync(
            _companyId, employee.Id, EndOfServiceReason.EmployerTermination, AsOf, "admin");

        Assert.Equal(25_200m, result.LeavePay);

        // ‏**والمخصّصُ لم يُمَسّ** — فلم يكن فيه شيءٌ يُبرَأ.
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.LeaveProvision));

        // ‏**والبدلُ كلُّه مصروفُ الفترة.**
        Assert.Equal(25_200m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.LeaveExpense));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<LeaveRequest> ApprovedLeaveAsync(
        long employeeId, LeaveType type, DateOnly from, DateOnly to)
    {
        var request = LeaveRequest.Create(_companyId, employeeId,
            $"LV-{Guid.NewGuid():N}"[..12], type, from, to);
        request.Approve(from, "admin");
        _db.LeaveRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    private async Task<Employee> EmployeeAsync(string code, string nationalId, decimal wage)
    {
        var employee = Employee.Create(_companyId, code, $"موظّف {code}", nationalId,
            EmployeeNationality.Saudi, "محاسب", Hire);
        employee.SetPay(basic: wage, housing: 0m, transport: 0m);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        return employee;
    }

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2020, 1, 1), new DateOnly(2030, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الإجازات", "310123456700077", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
    }
}
