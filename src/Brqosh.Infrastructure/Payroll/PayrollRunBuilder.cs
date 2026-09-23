using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Payroll;

/// <summary>سطرُ معاينةٍ لموظّفٍ مرشَّحٍ للدورة — <b>محسوبٌ لا مكتوب</b>.</summary>
public record PayrollCandidate(
    long EmployeeId,
    string Code,
    string NameAr,
    EmployeeNationality Nationality,
    decimal BasicSalary,
    decimal HousingAllowance,
    decimal TransportAllowance,
    decimal OtherAllowance,
    decimal GosiEmployee,
    decimal GosiEmployer,
    decimal AdvanceDeduction)
{
    public decimal GrossPay => BasicSalary + HousingAllowance + TransportAllowance + OtherAllowance;
    public decimal NetPay => GrossPay - GosiEmployee - AdvanceDeduction;
}

/// <summary>
/// (CEEF-231 · ط2-٧) <b>دورةُ الرواتب تُبنى من سجلّ الموظفين — لا تُكتب بالأصابع.</b>
///
/// <para><b>وما كان:</b> الشاشةُ الوحيدةُ التي تُنشئ دورةَ رواتب كانت جدولاً فارغاً
/// <b>يُكتب فيه اسمُ الموظّف نصّاً حرّاً</b>، وتُكتب حصّتا التأمينات بالأصابع. فكلُّ ما
/// بُني في <c>ط2</c> — سجلُّ الموظفين، ونِسَبُ التأمينات، ومكافأةُ نهاية الخدمة، وملفُّ
/// حماية الأجور، وقسيمةُ الراتب، والسُّلَف — <b>كان قائماً في النطاق ولا يصل إليه
/// المستخدم من طريقه الحقيقيّ</b>.</para>
///
/// <para><b>وأثرُه ليس تجميلياً:</b> سطرٌ بلا <c>EmployeeId</c> <b>يسقط من ملفّ حماية
/// الأجور</b> — و<c>WageProtectionFileBuilder</c> يتخطّى كلَّ سطرٍ لا موظّفَ له.
/// <b>فالمنشأةُ تُصدِّر ملفاً فارغاً لواجبٍ إلزاميٍّ نظاماً</b>، وقسيمةُ الراتب تخرج بلا
/// هويّةٍ ولا آيبان، <b>والسُّلفةُ لا تُستقطَع أبداً</b> لأنّ اشتقاقَها يمرّ بالمعرّف.</para>
///
/// <para><b>ولماذا آلةٌ في البنية لا منطقٌ في الشاشة:</b> منطقٌ في <c>xaml.cs</c> لا
/// يُقاس — والدرسُ نفسُه أخرج <c>ط6-١</c>. <b>فما يُبنى هنا يُقاس هنا</b>، والشاشةُ
/// تعرضه لا تحسبه.</para>
/// </summary>
public class PayrollRunBuilder(BrqoshDbContext db)
{
    /// <summary>
    /// الموظفون المرشَّحون لدورةٍ في تاريخِ صرفٍ بعينه — <b>وتأميناتُهم وأقساطُهم محسوبة</b>.
    /// </summary>
    /// <remarks>
    /// <b>ومَن ليس على رأس العمل في يوم الصرف لا يُرشَّح</b>: موظّفٌ انتهت خدمتُه في مارس
    /// لا يُصرف له راتبُ أبريل. <b>والترشيحُ هنا راحةٌ لا حراسة</b> — الحارسُ في
    /// <see cref="PayrollRun.AddEmployee"/> نفسِه، فلا يُلتفّ عليه بشاشةٍ أخرى.
    /// </remarks>
    public async Task<IReadOnlyList<PayrollCandidate>> CandidatesAsync(
        long companyId, DateOnly payDate, CancellationToken ct = default)
    {
        var policy = await PolicyAsync(companyId, ct);

        var employees = await db.Employees
            .Where(e => e.CompanyId == companyId && e.IsActive)
            .OrderBy(e => e.Code)
            .ToListAsync(ct);

        var advances = new EmployeeAdvanceService(db);
        var result = new List<PayrollCandidate>();

        foreach (var employee in employees.Where(e => e.IsEmployedOn(payDate)))
        {
            var gosi = policy.ContributionFor(employee);
            var due = await advances.DueInstallmentAsync(companyId, employee.Id, ct);

            // ‏**والمعاينةُ بالقاعدة نفسِها** — فلا تقترح الشاشةُ ما لن يُستقطع. (ولا غيابَ
            // ‏في المعاينة: يُقرأ عند البناء، فالمعاينةُ على الأجر الكامل.)
            var deduction = AdvanceDeduction(due, employee.GrossPay, gosi.Employee);

            result.Add(new PayrollCandidate(
                employee.Id, employee.Code, employee.NameAr, employee.Nationality,
                employee.BasicSalary, employee.HousingAllowance, employee.TransportAllowance,
                employee.OtherAllowance, gosi.Employee, gosi.Employer,
                Math.Round(deduction, 2, MidpointRounding.AwayFromZero)));
        }

        return result;
    }

    /// <summary>
    /// يبني دورةَ رواتبَ مسودّةً من سجلّ الموظفين — <b>وكلُّ سطرٍ يحمل معرّفَ صاحبه</b>.
    /// </summary>
    /// <param name="employeeIds">
    /// مَن يُدرَج. <b>و<c>null</c> تعني كلَّ مَن على رأس العمل</b> في تاريخ الصرف.
    /// </param>
    /// <remarks>
    /// <b>ولا تُحفَظ هنا.</b> البناءُ منفصلٌ عن الحفظ لأنّ الشاشةَ تعرض المعاينةَ قبل
    /// الالتزام، <b>ولأنّ الحفظَ والترحيلَ لهما مسارُهما</b> (<c>PayrollPostingService</c>).
    /// </remarks>
    public async Task<PayrollRun> BuildAsync(long companyId, int year, int month, DateOnly payDate,
        IReadOnlyCollection<long>? employeeIds = null, CancellationToken ct = default)
    {
        var policy = await PolicyAsync(companyId, ct);

        var query = db.Employees.Where(e => e.CompanyId == companyId && e.IsActive);
        if (employeeIds is { Count: > 0 })
            query = query.Where(e => employeeIds.Contains(e.Id));

        var employees = await query.OrderBy(e => e.Code).ToListAsync(ct);

        // ‏**ومعرّفٌ مطلوبٌ لا يقابله موظّفٌ يُرفض باسمه.** صمتُ التخطّي يُخرج دورةً
        // ‏ناقصةً تبدو كاملة، **وراتبٌ لا يُصرف لا يُكتشف إلا من صاحبه**.
        if (employeeIds is { Count: > 0 })
        {
            var missing = employeeIds.Except(employees.Select(e => e.Id)).ToList();
            if (missing.Count > 0)
                throw new AccountingException(
                    $"موظفون مطلوبون غير موجودين أو غير نشطين: {string.Join("، ", missing)}.");
        }

        var eligible = employees.Where(e => e.IsEmployedOn(payDate)).ToList();
        if (eligible.Count == 0)
            throw new AccountingException(
                $"لا يوجد موظفٌ على رأس العمل في {payDate:yyyy-MM-dd} — ولا تُبنى دورةٌ بلا سطور.");

        var run = PayrollRun.CreateDraft(companyId, year, month, payDate);
        var advances = new EmployeeAdvanceService(db);

        // (CEEF-233 · ز-٦) ‏**وأيّامُ الغياب بلا أجرٍ تُقرأ هنا لا تُدخَل يدوياً.**
        //
        // ‏**وخصمٌ يُترك للمستخدم يُنسى**: الموظّفُ غاب شهراً بلا أجر ويقبض راتبَه كاملاً
        // ‏لأنّ أحداً لم يكتب الخصم. **والسجلُّ موجودٌ ومعتمَد** — فالقراءةُ منه أوثقُ من
        // ‏ذاكرةِ محاسب، **وهي اللحظةُ نفسُها التي تُقرأ فيها أقساطُ السُّلَف**.
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var unpaidDays = await UnpaidLeaveDaysAsync(companyId, monthStart, monthEnd, ct);

        foreach (var employee in eligible)
        {
            var gosi = policy.ContributionFor(employee);
            var due = await advances.DueInstallmentAsync(companyId, employee.Id, ct);

            var absence = unpaidDays.TryGetValue(employee.Id, out var days)
                ? LeaveEntitlementCalculator.LeaveCompensation(employee.GrossPay, days)
                : 0m;

            // ‏**ولا يتجاوز الخصمُ أجرَ الشهر** — فغيابُ الشهر كلِّه يُصفّر الأجر ولا يُسالبه.
            absence = Math.Min(absence, employee.GrossPay);

            // ‏(ح-٤٩) ‏**والقسطُ من الأجر المستحقّ بعد الغياب** — وكان يُحسب قبله، فشهرُ غيابٍ
            // ‏وعليه سُلفةٌ يُخرج صافياً سالباً **ويُسقط الدورةَ كلَّها**.
            var deduction = AdvanceDeduction(due, employee.GrossPay - absence, gosi.Employee);

            run.AddEmployee(employee, policy,
                Math.Round(deduction, 2, MidpointRounding.AwayFromZero),
                Math.Round(absence, 2, MidpointRounding.AwayFromZero));
        }

        return run;
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-٤٩ · <c>treasury-payroll-assets#6</c>) <b>قسطُ السُّلفة لا يأخذ أكثرَ
    /// من نصف أجر الشهر — ولا يُسالب الصافي.</b> مالكٌ واحدٌ للقاعدة تستعمله المعاينةُ والبناء.
    ///
    /// <para>‏<b>وكان السقفُ «ما بقي بعد التأمينات» وحدَه</b> — فسُلفةٌ بقسطٍ كبيرٍ تأخذ الأجرَ كلَّه
    /// (٧٬٢٢٠ من ٨٬٠٠٠ والصافي صفر)، <b>وتعليقُ <c>EmployeeAdvance.InstallmentAmount</c> يَعِد بحدّ
    /// النصف ولا حارسَ في الشجرة</b>. والباقي يُرحَّل إلى الشهر التالي من تلقاء نفسه لأنّ الرصيدَ
    /// مُشتقٌّ لا مُخزَّن.</para>
    ///
    /// <para>‏<b>والحدُّ عند الاستقطاع لا عند الإنشاء</b>: الأجرُ يتغيّر بعد السُّلفة (زيادةٌ · خفضٌ ·
    /// غيابٌ بلا أجر)، <b>وحارسُ الإنشاء لا يرى إلا أجرَ يومِه</b>. والنصفُ يُقطع إلى الهللة لا يُقرَّب
    /// — فلا يزيد عليه بكسر.</para>
    ///
    /// <para>‏<b>والحدُّ النظاميُّ الأدقّ قرارُ المالك لا الكود</b>: نظامُ العمل يحدّ استقطاعَ القرض
    /// بعُشر الأجر إلّا برضا العامل، ومجموعَ الاستقطاعات بنصفه. والرضا لا يُمثَّل في النظام، <b>فالنصفُ
    /// هنا حدُّ المعقوليّة الموثَّق</b> — لا قراءةٌ نهائيّةٌ للمادّة.</para>
    /// </summary>
    internal static decimal AdvanceDeduction(decimal dueInstallment, decimal wageDue, decimal gosiEmployee)
    {
        var half = Math.Floor(wageDue * 50m) / 100m;
        var net = wageDue - gosiEmployee;
        return Math.Max(0m, Math.Min(dueInstallment, Math.Min(half, net)));
    }

    /// <summary>
    /// نِسَبُ المنشأة — <b>وتُنشأ بالافتراضيّ إن غابت</b>.
    /// </summary>
    /// <remarks>
    /// <b>وقاعدةٌ رُقّيت من نسخةٍ أقدمَ لا تحمل صفّاً</b> — والبذّارُ يُنشئه للمنشأة
    /// الجديدة وحدَها (<c>DatabaseInitializer</c>). <b>وسقوطٌ بـ<c>Single</c> يمنع الرواتبَ
    /// كلَّها</b> عن منشأةٍ قائمة، والافتراضيُّ هو نصُّ النظام نفسِه.
    /// </remarks>
    private async Task<GosiPolicy> PolicyAsync(long companyId, CancellationToken ct)
    {
        var policy = await db.GosiPolicies
            .SingleOrDefaultAsync(p => p.CompanyId == companyId, ct);

        if (policy is not null) return policy;

        policy = GosiPolicy.CreateDefault(companyId);
        db.GosiPolicies.Add(policy);
        await db.SaveChangesAsync(ct);
        return policy;
    }

    /// <summary>
    /// (CEEF-233 · ز-٦) أيّامُ الغياب بلا أجرٍ <b>الواقعةُ داخل الشهر</b> لكلّ موظّف.
    /// </summary>
    /// <remarks>
    /// <b>والتقاطعُ يُحسب لا مدّةُ الطلب كلُّها</b>: إجازةٌ بلا أجرٍ تمتدّ من ٢٥ مارس إلى
    /// ٥ أبريل <b>تُخصم سبعةَ أيّامٍ من مارس وخمسةً من أبريل</b> — وخصمُ الاثني عشر من
    /// مارس يُصفّر أجراً لم يُغَب كلُّه، <b>وخصمُها مرّتين يُخصم أربعةً وعشرين عن اثني عشر</b>.
    /// </remarks>
    private async Task<Dictionary<long, decimal>> UnpaidLeaveDaysAsync(
        long companyId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var requests = await db.LeaveRequests
            .Where(r => r.CompanyId == companyId
                        && r.Status == LeaveStatus.Approved
                        && r.Type == LeaveType.Unpaid
                        && r.FromDate <= to && from <= r.ToDate)
            .ToListAsync(ct);

        var days = new Dictionary<long, decimal>();
        foreach (var r in requests)
        {
            var start = r.FromDate > from ? r.FromDate : from;
            var end = r.ToDate < to ? r.ToDate : to;
            var overlap = end.DayNumber - start.DayNumber + 1;
            if (overlap <= 0) continue;

            days[r.EmployeeId] = days.GetValueOrDefault(r.EmployeeId) + overlap;
        }

        return days;
    }
}
