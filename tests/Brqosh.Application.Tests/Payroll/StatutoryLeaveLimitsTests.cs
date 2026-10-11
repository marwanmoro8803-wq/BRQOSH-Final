using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Payroll;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١١٧ · ‏`treasury-payroll-assets#9`) ‏<b>الإجازاتُ المدفوعةُ بنصٍّ لا تتجاوز مدّتَها النظاميّة.</b>
///
/// ‏<para>كانت إجازةُ «زواج» بستّين يوماً تُقدَّم وتُعتمد وتُصرف كاملةً، والحجُّ يُمنح مرّتين ولمن التحق أمس — فالدورةُ لا تخصم
/// ‏من هذه الأنواع شيئاً. والحدودُ بنظام العمل بعد تعديلات المرسوم م/٤٤ النافذة في ٢٠٢٥-٠٢-١٩: الزواجُ خمسةُ أيّام والوفاةُ خمسةٌ
/// ‏(المادة 113)، والحجُّ خمسةَ عشرَ يوماً مرّةً في الخدمة بعد سنتين متّصلتين (المادة 114)، والوضعُ اثنا عشرَ أسبوعاً (المادة 151).</para>
/// </summary>
public class StatutoryLeaveLimitsTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    private static readonly DateOnly Hire = new(2022, 1, 1);

    public StatutoryLeaveLimitsTests()
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

    [Theory]
    [InlineData(LeaveType.Marriage, 5)]
    [InlineData(LeaveType.Bereavement, 5)]
    [InlineData(LeaveType.Hajj, 15)]
    [InlineData(LeaveType.Maternity, 84)]
    [InlineData(LeaveType.Birth, 3)]
    public async Task A_paid_statutory_leave_is_accepted_up_to_its_limit_and_refused_beyond_it(LeaveType type, int limit)
    {
        var employee = await EmployeeAsync();
        var service = new LeaveRequestService(_db);
        var from = new DateOnly(2026, 3, 1);

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            service.SubmitAsync(_companyId, employee.Id, type, from, from.AddDays(limit)));
        Assert.Contains(limit.ToString(System.Globalization.CultureInfo.InvariantCulture), error.Message);

        var atLimit = await service.SubmitAsync(_companyId, employee.Id, type, from, from.AddDays(limit - 1));
        Assert.Equal(limit, atLimit.Days);
    }

    /// <summary>‏والاعتمادُ يفحص الحدَّ ثانيةً — فطلبٌ أُدخل قبل الحارس لا يُعتمد بمدّته.</summary>
    [Fact]
    public async Task Approval_refuses_a_pending_request_that_exceeds_the_limit()
    {
        var employee = await EmployeeAsync();
        var legacy = LeaveRequest.Create(_companyId, employee.Id, "LV-LEGACY", LeaveType.Marriage,
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 30));
        _db.LeaveRequests.Add(legacy);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<AccountingException>(() =>
            new LeaveRequestService(_db).ApproveAsync(_companyId, legacy.Id, new DateOnly(2026, 2, 1), "admin"));
    }

    [Fact]
    public async Task Hajj_is_granted_once_in_the_service()
    {
        var employee = await EmployeeAsync();
        var service = new LeaveRequestService(_db);
        await service.SubmitAsync(_companyId, employee.Id, LeaveType.Hajj, new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 10));

        await Assert.ThrowsAsync<AccountingException>(() =>
            service.SubmitAsync(_companyId, employee.Id, LeaveType.Hajj, new DateOnly(2026, 5, 20), new DateOnly(2026, 5, 29)));
    }

    [Fact]
    public async Task Hajj_needs_two_years_of_service()
    {
        var employee = await EmployeeAsync();
        var service = new LeaveRequestService(_db);

        await Assert.ThrowsAsync<AccountingException>(() =>
            service.SubmitAsync(_companyId, employee.Id, LeaveType.Hajj, new DateOnly(2023, 6, 20), new DateOnly(2023, 6, 29)));

        var eligible = await service.SubmitAsync(_companyId, employee.Id, LeaveType.Hajj,
            new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 10));
        Assert.Equal(10, eligible.Days);
    }

    /// <summary>شاهدُ ضبط: الأنواعُ بلا مدّةٍ نصّيّةٍ لا يحدّها هذا الحارس (والسنويّةُ يحدّها رصيدُها عند الاعتماد).</summary>
    [Theory]
    [InlineData(LeaveType.Unpaid)]
    [InlineData(LeaveType.Sick)]
    [InlineData(LeaveType.Exam)]
    public async Task Leave_types_without_a_statutory_duration_are_not_capped(LeaveType type)
    {
        var employee = await EmployeeAsync();

        var request = await new LeaveRequestService(_db).SubmitAsync(_companyId, employee.Id, type,
            new DateOnly(2026, 3, 1), new DateOnly(2026, 5, 29));

        Assert.Equal(90, request.Days);
    }

    private async Task<Employee> EmployeeAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الحدود", "310123456700077", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var employee = Employee.Create(_companyId, "E-1", "موظّف", "1011111111",
            EmployeeNationality.Saudi, "محاسب", Hire);
        employee.SetPay(basic: 9_000m, housing: 0m, transport: 0m);
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        return employee;
    }
}
