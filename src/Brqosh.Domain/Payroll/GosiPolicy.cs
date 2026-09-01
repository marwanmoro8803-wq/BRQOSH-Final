using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Payroll;

/// <summary>اشتراكُ شهرٍ واحدٍ لموظّفٍ واحد — حصّةُ الموظّف وحصّةُ صاحب العمل.</summary>
public readonly record struct GosiContribution(decimal Employee, decimal Employer)
{
    public decimal Total => Employee + Employer;
}

/// <summary>
/// (CEEF-230 · ط2-٢) <b>نِسَبُ التأمينات الاجتماعية — مُخزَّنةٌ لكلّ منشأةٍ لا مثبَّتةٌ في الشيفرة.</b>
///
/// <para><b>ما كان:</b> حصّتا التأمينات <b>رقمان يُكتبان باليد</b> في شبكة الإدخال، بلا
/// نسبةٍ ولا سقفِ أجرٍ ولا تفرقةٍ بين سعوديٍّ ووافد. فمنشأةٌ بعشرين موظّفاً تحسبها في ورقةٍ
/// خارجية كلَّ شهر — <b>وأوّلُ خطأِ نسخٍ يمرّ إلى الأستاذ بلا حارس</b>.</para>
///
/// <para><b>ولماذا مُخزَّنةٌ لا ثابتة:</b> النِّسَبُ يُعدّلها المُنظِّم — وقد تغيّرت فعلاً
/// لمن التحق بعد تاريخٍ معيّن. <b>وثابتٌ في الشيفرة يجعل كلَّ تعديلٍ نظاميٍّ إصداراً
/// جديداً</b>، ويترك مَن لا يُحدّث يحسب بنسبةٍ ملغاة. فالقيمُ هنا <b>افتراضاتٌ مبدئية</b>
/// يملكها صاحبُ المنشأة ويُصحّحها.</para>
///
/// <para><b>والأجرُ الخاضع أساسيٌّ + سكن</b> دون بقيّة البدلات، <b>وبسقف</b> — وهما
/// القاعدتان اللتان يُخطئ فيهما الحسابُ اليدويّ أكثرَ من غيرهما.</para>
/// </summary>
public class GosiPolicy
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }

    /// <summary>حصّةُ الموظّف السعوديّ — معاشٌ وساند.</summary>
    public decimal SaudiEmployeePercent { get; private set; } = 9.75m;

    /// <summary>حصّةُ صاحب العمل عن السعوديّ — معاشٌ وأخطارٌ مهنيةٌ وساند.</summary>
    public decimal SaudiEmployerPercent { get; private set; } = 11.75m;

    /// <summary>حصّةُ الموظّف الوافد — <b>صفر</b>: لا يُشترك في المعاش ولا الساند.</summary>
    public decimal NonSaudiEmployeePercent { get; private set; } = 0m;

    /// <summary>حصّةُ صاحب العمل عن الوافد — الأخطارُ المهنية وحدها.</summary>
    public decimal NonSaudiEmployerPercent { get; private set; } = 2m;

    /// <summary>سقفُ الأجر الخاضع للاشتراك شهرياً.</summary>
    public decimal ContributoryWageCap { get; private set; } = 45_000m;

    /// <summary>حدُّ الأجر الأدنى الخاضع — وما دونه يُشترك بما هو.</summary>
    public decimal ContributoryWageFloor { get; private set; } = 0m;

    private GosiPolicy() { }

    public static GosiPolicy CreateDefault(long companyId) => new() { CompanyId = companyId };

    public void SetRates(decimal saudiEmployee, decimal saudiEmployer,
        decimal nonSaudiEmployee, decimal nonSaudiEmployer)
    {
        foreach (var rate in new[] { saudiEmployee, saudiEmployer, nonSaudiEmployee, nonSaudiEmployer })
            if (rate is < 0m or > 100m)
                throw new AccountingException($"نسبةُ التأمينات بين 0 و100 — ووردَت {rate:N2}.");

        SaudiEmployeePercent = saudiEmployee;
        SaudiEmployerPercent = saudiEmployer;
        NonSaudiEmployeePercent = nonSaudiEmployee;
        NonSaudiEmployerPercent = nonSaudiEmployer;
    }

    public void SetContributoryWageBounds(decimal floor, decimal cap)
    {
        if (floor < 0m) throw new AccountingException("حدُّ الأجر الأدنى لا يكون سالباً.");
        if (cap <= 0m) throw new AccountingException("سقفُ الأجر يجب أن يكون موجباً.");
        if (floor > cap) throw new AccountingException("الحدُّ الأدنى فوق السقف.");

        ContributoryWageFloor = floor;
        ContributoryWageCap = cap;
    }

    /// <summary>
    /// <b>الأجرُ الخاضع: الأساسيُّ + بدلُ السكن، محصوراً بين الحدّ والسقف.</b>
    ///
    /// <para><b>وبدلُ النقل وغيرُه خارجه</b> — وهو موضعُ الخطأ الشائع في الحساب اليدويّ:
    /// طيُّ البدلات كلِّها في «الراتب» يُنتج اشتراكاً أعلى ممّا يستحقّ، <b>ويدفع صاحبُ
    /// المنشأة فرقاً لا يسترجعه</b>.</para>
    /// </summary>
    public decimal ContributoryWage(Employee employee) =>
        Math.Clamp(employee.BasicSalary + employee.HousingAllowance,
            ContributoryWageFloor, ContributoryWageCap);

    /// <summary>اشتراكُ موظّفٍ لشهرٍ واحد.</summary>
    public GosiContribution ContributionFor(Employee employee)
    {
        var wage = ContributoryWage(employee);

        var (employeeRate, employerRate) = employee.Nationality == EmployeeNationality.Saudi
            ? (SaudiEmployeePercent, SaudiEmployerPercent)
            : (NonSaudiEmployeePercent, NonSaudiEmployerPercent);

        return new GosiContribution(
            Math.Round(wage * employeeRate / 100m, 2, MidpointRounding.AwayFromZero),
            Math.Round(wage * employerRate / 100m, 2, MidpointRounding.AwayFromZero));
    }
}
