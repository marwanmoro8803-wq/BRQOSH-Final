using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

/// <summary>
/// <b>قسائمُ دورةِ رواتبٍ — سطراً سطراً.</b>
///
/// <para><b>وتُبنى من الدورة لا من سجلّ الموظفين</b>: القسيمةُ وثيقةُ شهرٍ بعينه، وأجرُ
/// الموظّف قد تغيّر بعده. <b>فبناؤها من السجلّ الحاليّ يُعيد كتابة تاريخٍ مضى</b> —
/// ويُعطي الموظّفَ قسيمةَ يناير بأرقام يونيو.</para>
///
/// <para><b>ولا تُصدَر إلا من دورةٍ مرحّلة</b>: المسودّةُ تتغيّر، وقسيمةٌ سُلّمت ثمّ تغيّر
/// أصلُها <b>خلافٌ لا يُحسم</b>.</para>
/// </summary>
public class PayslipQuery(BrqoshDbContext db)
{
    public async Task<IReadOnlyList<PayslipData>> ExecuteAsync(
        long companyId, long payrollRunId, CancellationToken ct = default)
    {
        var run = await db.PayrollRuns.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == payrollRunId && r.CompanyId == companyId, ct)
            ?? throw new AccountingException("دورة الرواتب غير موجودة.");

        if (run.Status != PayrollRunStatus.Posted)
            throw new AccountingException(
                "لا تُصدَر القسائم إلا من دورةٍ مرحّلة — والمسودّةُ تتغيّر بعد التسليم.");

        var employeeIds = run.Lines.Where(l => l.EmployeeId is not null)
            .Select(l => l.EmployeeId!.Value).ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        return [.. run.Lines.Select(line =>
        {
            // ‏**وسطرٌ بلا سجلٍّ يخرج قسيمةً كذلك** — بما فيه من بيانات: أرقامُ الراتب
            // ‏كلُّها على السطر، والناقصُ هويّةٌ ومسمّى. **وحجبُ قسيمته يحرم موظّفاً
            // ‏حقّاً لأنّ سجلّه لم يُنشأ بعد**، والنقصُ يُرى في الورقة لا يُخفيها.
            var employee = line.EmployeeId is long id && employees.TryGetValue(id, out var e) ? e : null;

            return new PayslipData(
                run.PeriodCode, run.PayDate,
                line.EmployeeName,
                employee?.Code ?? "—",
                employee?.NationalId ?? "—",
                employee?.JobTitle ?? "—",
                employee?.Iban,
                line.BasicSalary, line.HousingAllowance, line.TransportAllowance,
                line.OtherAllowance, line.GosiEmployee,
                // ‏**الأرقامُ على السطر ولم تُمرَّر**،
                // ‏فتخرج القسيمةُ بصافٍِ يخالف ملفّ الأجور والقيد.
                line.AbsenceDeduction, line.AdvanceDeduction);
        })];
    }
}
