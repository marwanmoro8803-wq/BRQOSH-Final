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

            // ‏**والقسطُ لا يبتلع الصافي.** استقطاعٌ يتجاوز ما بقي بعد التأمينات يُخرج
            // ‏صافياً سالباً — **ويرفضه `AddLine` باسمه**. وترشيحٌ يقترح ما سيُرفض يجعل
            // ‏الشاشةَ تعتذر عن حسابها هي. فيُحَدُّ بما يسع، **والباقي يُرحَّل إلى الشهر
            // ‏التالي من تلقاء نفسه** لأنّ الرصيدَ مُشتقٌّ لا مُخزَّن.
            var room = employee.GrossPay - gosi.Employee;
            var deduction = Math.Max(0m, Math.Min(due, room));

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

        foreach (var employee in eligible)
        {
            var gosi = policy.ContributionFor(employee);
            var due = await advances.DueInstallmentAsync(companyId, employee.Id, ct);
            var room = employee.GrossPay - gosi.Employee;
            var deduction = Math.Max(0m, Math.Min(due, room));

            run.AddEmployee(employee, policy, Math.Round(deduction, 2, MidpointRounding.AwayFromZero));
        }

        return run;
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
}
