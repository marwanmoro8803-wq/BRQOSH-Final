using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١١٦ · ‏`treasury-payroll-assets#9`) ‏**الإجازةُ المرضيّةُ تُصرف بتدرّج المادة 117 — وكانت تُصرف كاملةً أبداً.**
///
/// ‏<para>نصُّ المادة (هيئة الخبراء): «بأجرٍ عن الثلاثين يوماً الأولى، وبثلاثة أرباع الأجر عن الستّين يوماً التالية، ودون أجرٍ
/// ‏للثلاثين يوماً التي تلي ذلك خلال السنة الواحدة، سواءٌ أكانت متّصلةً أم متقطّعة. ويُقصد بالسنة الواحدة السنةُ التي تبدأ من تاريخ
/// ‏أوّل إجازةٍ مرضيّة». وكان <c>PayrollRunBuilder</c> يقرأ «بلا أجر» وحدَها، و<c>SickPayFactor</c> لا يستدعيه إلا الاختبار.</para>
///
/// ‏<para>أجرُ ٩٬٠٠٠ ⟵ اليومُ ٣٠٠؛ يومُ ثلاثة الأرباع يُخصم منه ٧٥، ويومُ «بلا أجر» ٣٠٠.</para>
/// </summary>
public class SickLeaveIsPaidOnArticle117TiersTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public SickLeaveIsPaidOnArticle117TiersTests()
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

    /// <summary>خمسةٌ وأربعون يوماً: أبريلُ (١…٣٠) كامل، ومايو (٣١…٤٥) خمسةَ عشرَ بثلاثة أرباع ⟵ ١٬١٢٥.</summary>
    [Fact]
    public async Task The_days_after_the_thirtieth_are_paid_at_three_quarters()
    {
        var employee = await SetupAsync();
        await ApprovedLeaveAsync(employee.Id, LeaveType.Sick, new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 15));

        var april = (await BuildAsync(2026, 4)).Lines.Single();
        var may = (await BuildAsync(2026, 5)).Lines.Single();

        Assert.Equal(0m, april.AbsenceDeduction);
        Assert.Equal(1_125m, may.AbsenceDeduction);
        Assert.Equal(7_875m, may.GrossPay);
    }

    /// <summary>متقطّعةٌ تُجمع: ٨٥ يوماً ثمّ عشرة ⟵ خمسةٌ بثلاثة أرباع (٣٧٥) وخمسةٌ بلا أجر (١٬٥٠٠).</summary>
    [Fact]
    public async Task Intermittent_sick_leave_accumulates_and_the_days_after_the_ninetieth_are_unpaid()
    {
        var employee = await SetupAsync();
        await ApprovedLeaveAsync(employee.Id, LeaveType.Sick, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 26));
        await ApprovedLeaveAsync(employee.Id, LeaveType.Sick, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10));

        var june = (await BuildAsync(2026, 6)).Lines.Single();

        Assert.Equal(1_875m, june.AbsenceDeduction);
        Assert.Equal(7_125m, june.GrossPay);
    }

    /// <summary>السنةُ من أوّل إجازةٍ مرضيّة لا السنةُ التقويميّة: يوليو ٢٠٢٦ ثمّ فبراير ٢٠٢٧ في سنةٍ واحدة.</summary>
    [Fact]
    public async Task The_year_starts_at_the_first_sick_leave_not_on_the_first_of_january()
    {
        var employee = await SetupAsync();
        await ApprovedLeaveAsync(employee.Id, LeaveType.Sick, new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 23));
        await ApprovedLeaveAsync(employee.Id, LeaveType.Sick, new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 10));

        var february = (await BuildAsync(2027, 2)).Lines.Single();

        Assert.Equal(1_875m, february.AbsenceDeduction);
    }

    /// <summary>وبعد انقضاء السنة تبدأ سنةٌ جديدةٌ من أوّل إجازةٍ تليها — فلا يُحمل الماضي كلُّه أبداً.</summary>
    [Fact]
    public async Task A_new_year_starts_with_the_first_sick_leave_after_the_previous_year_ends()
    {
        var employee = await SetupAsync();
        await ApprovedLeaveAsync(employee.Id, LeaveType.Sick, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));
        await ApprovedLeaveAsync(employee.Id, LeaveType.Sick, new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 10));

        var february = (await BuildAsync(2027, 2)).Lines.Single();

        Assert.Equal(0m, february.AbsenceDeduction);
    }

    /// <summary>مرضيّةٌ بعد التسعين وإجازةٌ بلا أجرٍ في شهرٍ واحد تُخصمان معاً — والمجموعُ لا يتجاوز أجرَ الشهر.</summary>
    [Fact]
    public async Task Sick_and_unpaid_leave_in_one_month_both_deduct_and_the_total_stops_at_the_wage()
    {
        var employee = await SetupAsync();
        await ApprovedLeaveAsync(employee.Id, LeaveType.Sick, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));
        await ApprovedLeaveAsync(employee.Id, LeaveType.Sick, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 20));
        await ApprovedLeaveAsync(employee.Id, LeaveType.Unpaid, new DateOnly(2026, 4, 21), new DateOnly(2026, 4, 30));

        var april = (await BuildAsync(2026, 4)).Lines.Single();

        Assert.Equal(9_000m, april.AbsenceDeduction);
        Assert.Equal(0m, april.GrossPay);
        Assert.Equal(0m, april.GosiEmployee);
        Assert.Equal(0m, april.NetPay);
        Assert.True(april.GosiEmployer > 0m, "حصّةُ المنشأة لا تُمسّ.");
    }

    /// <summary>
    /// ‏(ح-١١٦ · قرارُ المالك) ‏<b>والحصّةُ تُقصّ إلى المستحقّ لا تُصفَّر</b>: تسعةٌ وثمانون يوماً قبل أبريل، فأوّلُه اليومُ التسعون
    /// ‏(يُخصم ربعُه ٧٥) وتسعةٌ وعشرون بعده بلا أجر (٨٬٧٠٠) ⟵ المستحقّ ٢٢٥ وحصّةُ التأمينات ٨٧٧٫٥٠ ⟵ تُستقطع ٢٢٥ والصافي صفر.
    /// </summary>
    [Fact]
    public async Task The_employee_gosi_share_is_cut_to_the_wage_due_not_dropped()
    {
        var employee = await SetupAsync();
        await ApprovedLeaveAsync(employee.Id, LeaveType.Sick, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 30));
        await ApprovedLeaveAsync(employee.Id, LeaveType.Sick, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));

        var april = (await BuildAsync(2026, 4)).Lines.Single();

        Assert.Equal(8_775m, april.AbsenceDeduction);
        Assert.Equal(225m, april.GrossPay);
        Assert.Equal(225m, april.GosiEmployee);
        Assert.Equal(0m, april.NetPay);
    }

    /// <summary>شاهدُ ضبط: المرضيّةُ غيرُ المعتمدة لا تُخصم.</summary>
    [Fact]
    public async Task A_pending_sick_leave_does_not_deduct()
    {
        var employee = await SetupAsync();
        await ApprovedLeaveAsync(employee.Id, LeaveType.Sick, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31));
        var pending = LeaveRequest.Create(_companyId, employee.Id, "LV-PENDING", LeaveType.Sick,
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 10));
        _db.LeaveRequests.Add(pending);
        await _db.SaveChangesAsync();

        var april = (await BuildAsync(2026, 4)).Lines.Single();

        Assert.Equal(0m, april.AbsenceDeduction);
    }

    private Task<PayrollRun> BuildAsync(int year, int month) =>
        new PayrollRunBuilder(_db).BuildAsync(_companyId, year, month,
            new DateOnly(year, month, 1).AddMonths(1).AddDays(-1));

    private async Task ApprovedLeaveAsync(long employeeId, LeaveType type, DateOnly from, DateOnly to)
    {
        var request = LeaveRequest.Create(_companyId, employeeId,
            $"LV-{Guid.NewGuid():N}"[..12], type, from, to);
        request.Approve(from, "admin");
        _db.LeaveRequests.Add(request);
        await _db.SaveChangesAsync();
    }

    private async Task<Employee> SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة المرضيّة", "310123456700077", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var employee = Employee.Create(_companyId, "E-1", "موظّف", "1011111111",
            EmployeeNationality.Saudi, "محاسب", new DateOnly(2022, 1, 1));
        employee.SetPay(basic: 9_000m, housing: 0m, transport: 0m);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        return employee;
    }
}
