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

    // ── (جاهزيّةُ الإطلاق · ح-١٣٨) النظامُ الجديدُ (٣ يوليو ٢٠٢٤) — جدولُ نسبة المعاش ──────────────────────────────
    //
    // ‏المعاشُ ٩٪ لكلّ طرفٍ عند بدء النظام، ويرتفع نصفَ نقطةٍ لكلّ طرفٍ كلَّ يوليو من ٢٠٢٥ حتى ١١٪ في ٢٠٢٨؛ والساندُ ٠٫٧٥٪ لكلّ
    // ‏طرف، والأخطارُ المهنيّةُ ٢٪ على صاحب العمل — كما هما للقديم. فالبدايةُ ٩٫٧٥٪ و١١٫٧٥٪ كالقديم، ويفترقان من يوليو ٢٠٢٥.
    // ‏(المصادر: إعلانُ المؤسّسة كما نقلته أرقام ٢٠٢٥-٠٦-٣٠، وجداولُ مزوّدي الرواتب؛ والقيمُ افتراضاتٌ يملكها صاحبُ المنشأة.)

    /// <summary>‏حصّةُ موظّف النظام الجديد عند بدئه — معاشٌ وساند.</summary>
    public decimal NewLawEmployeePercent { get; private set; } = 9.75m;

    /// <summary>‏حصّةُ صاحب العمل عن موظّف النظام الجديد عند بدئه — معاشٌ وساندٌ وأخطارٌ مهنيّة.</summary>
    public decimal NewLawEmployerPercent { get; private set; } = 11.75m;

    /// <summary>‏زيادةُ نسبة المعاش السنويّة لكلّ طرف.</summary>
    public decimal NewLawAnnualStepPercent { get; private set; } = 0.5m;

    /// <summary>‏تاريخُ أوّل زيادة — وتتلوها زياداتٌ في التاريخ نفسِه من كلّ سنة.</summary>
    public DateOnly NewLawFirstStep { get; private set; } = new(2025, 7, 1);

    /// <summary>‏عددُ الزيادات — ثمّ تقف النسبة.</summary>
    public int NewLawSteps { get; private set; } = 4;

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

    /// <summary>‏(ح-١٣٨) جدولُ النظام الجديد — يُصحّحه صاحبُ المنشأة كما يُصحّح النسبَ القديمة.</summary>
    public void SetNewLawSchedule(decimal employeePercent, decimal employerPercent, decimal annualStepPercent,
        DateOnly firstStep, int steps)
    {
        foreach (var rate in new[] { employeePercent, employerPercent })
            if (rate is < 0m or > 30m)
                throw new AccountingException($"نسبةُ التأمينات بين 0 و30 — ووردَت {rate:N2}.");
        if (annualStepPercent is < 0m or > 5m)
            throw new AccountingException("الزيادةُ السنويّةُ بين 0 و5 نقاط.");
        if (steps is < 1 or > 20)
            throw new AccountingException("عددُ الزيادات بين 1 و20.");

        NewLawEmployeePercent = employeePercent;
        NewLawEmployerPercent = employerPercent;
        NewLawAnnualStepPercent = annualStepPercent;
        NewLawFirstStep = firstStep;
        NewLawSteps = steps;
    }

    /// <summary>
    /// ‏نسبتا النظام الجديد في <paramref name="month"/> — <b>قرارٌ واحدٌ</b> يحسب به الاشتراكُ وتعرضه شاشةُ النسب، فلا تختلفان.
    /// </summary>
    public (decimal Employee, decimal Employer) NewLawPercentOn(DateOnly month)
    {
        var raise = NewLawAnnualStepPercent * NewLawStepsReached(month);
        return (NewLawEmployeePercent + raise, NewLawEmployerPercent + raise);
    }

    /// <summary>‏كم زيادةً وقعت حتى <paramref name="month"/> — من أوّلها، وتقف عند عددها.</summary>
    internal int NewLawStepsReached(DateOnly month)
    {
        var reached = 0;
        while (reached < NewLawSteps && NewLawFirstStep.AddYears(reached) <= month)
            reached++;
        return reached;
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

    /// <summary>
    /// اشتراكُ موظّفٍ لشهرٍ واحد — <b>والشهرُ إلزاميّ</b> (ح-١٣٨): نسبةُ النظام الجديد تتغيّر كلَّ يوليو، فاشتراكٌ بلا شهرٍ
    /// لا يُعرف بأيّ نسبة.
    /// </summary>
    public GosiContribution ContributionFor(Employee employee, DateOnly month)
    {
        var wage = ContributoryWage(employee);

        var (employeeRate, employerRate) = employee.Nationality != EmployeeNationality.Saudi
            ? (NonSaudiEmployeePercent, NonSaudiEmployerPercent)
            : employee.GosiRegime == GosiRegime.NewLaw2024
                ? NewLawPercentOn(month)
                : (SaudiEmployeePercent, SaudiEmployerPercent);

        return new GosiContribution(
            Math.Round(wage * employeeRate / 100m, 2, MidpointRounding.AwayFromZero),
            Math.Round(wage * employerRate / 100m, 2, MidpointRounding.AwayFromZero));
    }
}
