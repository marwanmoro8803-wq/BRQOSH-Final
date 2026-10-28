using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

/// <summary>
/// رصيدُ إجازاتِ موظّف — <b>والمستحقُّ والمُستهلَكُ يُعرضان معه</b>.
/// </summary>
/// <remarks>
/// <b>ورقمُ الرصيد وحدَه لا يكفي للمنازعة</b>: الموظّفُ يسأل «من أين؟»، والجوابُ طرفان.
/// </remarks>
public record LeaveBalance(
    long EmployeeId, decimal AccruedDays, decimal TakenDays, decimal UnpaidDays)
{
    /// <summary>الرصيدُ المتاح — <b>ولا يُعرض سالباً</b>.</summary>
    public decimal BalanceDays => Math.Round(AccruedDays - TakenDays, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// <b>رصيدُ الإجازات — يُشتقّ من المستندات لا من عدّاد.</b>
///
/// <para><b>وهو اصطلاحُ هذا المستودع</b> (‏<c>ط1-٤</c>‏ و<c>هـ-١٧</c>‏): <b>عدّادٌ مخزَّنٌ
/// على الموظّف ينحرف عند أوّل إلغاءِ طلبٍ أو تعديلِ تاريخ</b>، ولا يُكتشف انحرافُه إلا عند
/// التصفية — <b>حين يصير فرقاً نقديّاً يُنازَع فيه</b>. والاشتقاقُ لا ينحرف.</para>
///
/// <para><b>والمُستهلَكُ من السنويّة وحدَها</b>: المرضيّةُ والوضعُ والزواجُ حقوقٌ مستقلّةٌ
/// بنصّ النظام — <b>وخصمُها من الرصيد السنويّ يأكل حقّاً بحقّ</b>، فيخرج الموظّفُ بلا
/// إجازةٍ سنويّةٍ لأنّه مرض.</para>
///
/// <para><b>وهويّةُ هـ-٢٦:</b> <c>الرصيد = المستحقُّ المتراكم − المُستهلَك من السنويّة</c>.</para>
/// </summary>
public class LeaveBalanceQuery(BrqoshDbContext db)
{
    public async Task<LeaveBalance> ForEmployeeAsync(
        long companyId, long employeeId, DateOnly asOf, CancellationToken ct = default)
    {
        var employee = await db.Employees
            .SingleOrDefaultAsync(e => e.CompanyId == companyId && e.Id == employeeId, ct)
            ?? throw new AccountingException("الموظف غير موجود.");

        var requests = await ApprovedUpToAsync(companyId, asOf, employeeId, ct);

        return Build(employee, requests, asOf);
    }

    /// <summary>أرصدةُ موظّفي المنشأة — <b>باستعلامين لا باستعلامٍ لكلّ موظّف</b>.</summary>
    /// <remarks>
    /// <b>ومنشأةٌ بألفِ موظّفٍ تُخرج ألفَ رحلةٍ إلى القاعدة</b> إن سُئل كلٌّ على حدة —
    /// وهي شاشةٌ تُفتح كلَّ صباح.
    /// </remarks>
    public async Task<IReadOnlyList<LeaveBalance>> ForAllAsync(
        long companyId, DateOnly asOf, CancellationToken ct = default)
    {
        var employees = await db.Employees
            .Where(e => e.CompanyId == companyId)
            .ToListAsync(ct);

        var requests = await ApprovedUpToAsync(companyId, asOf, employeeId: null, ct);
        var byEmployee = requests.GroupBy(r => r.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return [.. employees.Select(e => Build(
            e,
            byEmployee.TryGetValue(e.Id, out var rows) ? rows : [],
            asOf))];
    }

    /// <summary>
    /// الطلباتُ المعتمدةُ التي <b>بدأت</b> حتى التاريخ.
    /// </summary>
    /// <remarks>
    /// <b>والمعيارُ تاريخُ البداية لا النهاية</b>: إجازةٌ بدأت أمسَ وتنتهي الشهرَ القادم
    /// <b>قد استُهلكت فعلاً</b> — والموظّفُ غائبٌ الآن. <b>واشتراطُ انتهائها يُظهر رصيداً
    /// لا وجودَ له</b> فيُمنح إجازةً ثانيةً وهو في الأولى.
    /// </remarks>
    private async Task<List<LeaveRequest>> ApprovedUpToAsync(
        long companyId, DateOnly asOf, long? employeeId, CancellationToken ct) =>
        await db.LeaveRequests
            .Where(r => r.CompanyId == companyId
                        && r.Status == LeaveStatus.Approved
                        && r.FromDate <= asOf
                        && (employeeId == null || r.EmployeeId == employeeId))
            .ToListAsync(ct);

    private static LeaveBalance Build(
        Domain.Payroll.Employee employee, List<LeaveRequest> requests, DateOnly asOf)
    {
        var accrued = LeaveEntitlementCalculator.AccruedDaysFor(employee, asOf);

        var taken = requests
            .Where(r => LeaveEntitlementCalculator.ConsumesAnnualBalance(r.Type))
            .Sum(r => (decimal)r.Days);

        var unpaid = requests
            .Where(r => !LeaveEntitlementCalculator.IsPaid(r.Type))
            .Sum(r => (decimal)r.Days);

        return new LeaveBalance(employee.Id, accrued, taken, unpaid);
    }
}
