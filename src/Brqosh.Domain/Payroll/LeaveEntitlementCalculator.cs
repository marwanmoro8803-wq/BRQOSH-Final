using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Payroll;

/// <summary>
/// نوعُ الإجازة — <b>وهو ما يُقرّر أتُدفع أم لا، وبأيّ نسبة</b>.
/// </summary>
public enum LeaveType
{
    /// <summary>سنويّةٌ مدفوعة (المادة 109) — <b>وهي وحدَها التي تُخصم من رصيدٍ متراكم</b>.</summary>
    Annual = 0,

    /// <summary>مرضيّة (المادة 117) — <b>وأجرُها متدرّج</b>: كامل ثمّ ثلاثة أرباع ثمّ بلا أجر.</summary>
    Sick = 1,

    /// <summary>بلا أجر — <b>وتُخصم من الراتب</b>، وهي الغيابُ المأذون.</summary>
    Unpaid = 2,

    /// <summary>وضعٌ (المادة 151 بعد تعديل م/٤٤ النافذ 2025-02-19) — <b>اثنا عشرَ أسبوعاً بأجر</b>، وكانت عشرة.</summary>
    Maternity = 3,

    /// <summary>زواجٌ (المادة 113) — <b>خمسةُ أيّامٍ بأجر</b>.</summary>
    Marriage = 4,

    /// <summary>وفاةُ قريب (المادة 113) — <b>خمسةُ أيّامٍ بأجر</b> للزوج والأصل والفرع، وثلاثةٌ للأخ (لا يُفرَّق هنا فالحدُّ خمسة).</summary>
    Bereavement = 5,

    /// <summary>حجٌّ (المادة 114) — <b>خمسةَ عشرَ يوماً حدّاً، مرّةً طوال الخدمة، بعد سنتين متّصلتين</b>، بأجر.</summary>
    Hajj = 6,

    /// <summary>امتحانٌ (المادة 115) — بأجر.</summary>
    Exam = 7,
}

/// <summary>
/// (CEEF-233 · ز-١) <b>الإجازاتُ والغياب — نظامُ العمل السعوديّ، المواد 109 و111 و117.</b>
///
/// <para><b>ما كان:</b> <b>لا كيانَ ولا خدمةَ ولا شاشة</b>. فالمنشأةُ تمنح إجازاتٍ وتُسجّلها
/// في جدولٍ خارجيّ — <b>ورصيدُ الإجازات التزامٌ نظاميٌّ يتراكم كلَّ يوم</b> ولا يظهر في
/// ميزانيّتها بريال. وعند انتهاء الخدمة <b>يُطالَب ببدل الإجازة (المادة 111) فيُفاجأ به</b>.</para>
///
/// <para><b>والاستحقاقُ يتراكم يوماً بيوم لا سنةً بسنة</b>: مَن خدم سبعةَ أشهرٍ استحقّ نصيبَه
/// منها. <b>وتقريبُه إلى سنةٍ كاملةٍ يحرمه أو يمنحه ما ليس له</b> — والفرقُ يُدفع نقداً عند
/// المغادرة، فهو مالٌ لا تقدير.</para>
///
/// <para><b>والنسبةُ تنقلب عند خمس سنوات</b> (المادة 109): واحدٌ وعشرون يوماً تصير ثلاثين.
/// <b>وحسابٌ يُطبّق النسبةَ الجديدة على المدّة كلِّها بأثرٍ رجعيّ يمنح تسعةَ أيّامٍ عن كلّ
/// سنةٍ سبقت الخمس</b> — وهي في منشأةٍ بمئة موظّفٍ قدماء عشراتُ الآلاف. <b>فالمدّةُ تُقسم
/// عند حدّ الخمس ويُحسب كلُّ شطرٍ بنسبته.</b></para>
///
/// <para><b>وهويّةُ هـ-٢٦:</b> <c>الرصيدُ = المستحقُّ المتراكم − المُستهلَك</c>،
/// <b>ولا يكون سالباً</b> — وسالبُه يعني منحَ إجازةٍ لم تُستحقّ.</para>
/// </summary>
public static class LeaveEntitlementCalculator
{
    /// <summary>أيّامُ الاستحقاق السنويّ قبل إتمام خمس سنوات (المادة 109).</summary>
    public const decimal DaysBeforeFiveYears = 21m;

    /// <summary>وبعدها.</summary>
    public const decimal DaysAfterFiveYears = 30m;

    /// <summary>حدُّ انقلاب النسبة — <b>خمسُ سنواتِ خدمةٍ متّصلة</b>.</summary>
    public const decimal RateChangeYears = 5m;

    /// <summary>طولُ السنة بالأيّام — <b>كسريٌّ ليستوعب الكبيسة</b>، كما في <see cref="Employee.ServiceYearsAt"/>.</summary>
    private const decimal DaysPerYear = 365.25m;

    /// <summary>
    /// <b>الأجرُ اليوميُّ = الشهريُّ ÷ 30</b> — وهو المتّبع نظاماً مهما كان عددُ أيّام الشهر.
    /// </summary>
    /// <remarks>
    /// <b>والقسمةُ على أيّام الشهر الفعليّة تُنتج أجرَين مختلفين للإجازة نفسِها</b> — يومٌ
    /// في فبراير أغلى من يومٍ في يناير، <b>وهو ما يُنازِع فيه الموظّفُ بحقّ</b>.
    /// </remarks>
    public static decimal DailyWage(decimal monthlyWage)
    {
        if (monthlyWage < 0m)
            throw new AccountingException("الأجر الشهري لا يكون سالباً.");

        return Math.Round(monthlyWage / 30m, 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// <b>المستحقُّ المتراكم من التعيين حتى تاريخ — بالأيّام.</b>
    ///
    /// <para><b>والمدّةُ تُقسم عند حدّ الخمس</b>: ما قبله بواحدٍ وعشرين وما بعده بثلاثين.
    /// <b>ولا يُطبَّق الأعلى بأثرٍ رجعيّ</b> — انظر ملاحظةَ الصنف.</para>
    /// </summary>
    public static decimal AccruedDays(DateOnly hireDate, DateOnly asOf)
    {
        if (asOf <= hireDate) return 0m;

        var served = (asOf.DayNumber - hireDate.DayNumber) / DaysPerYear;

        var firstSpan = Math.Min(served, RateChangeYears);
        var laterSpan = Math.Max(0m, served - RateChangeYears);

        var days = firstSpan * DaysBeforeFiveYears + laterSpan * DaysAfterFiveYears;
        return Math.Round(days, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>المستحقُّ المتراكم لموظّف — <b>ويتوقّف يومَ انتهاء الخدمة لا يومَ الحساب</b>.</summary>
    /// <remarks>
    /// <b>ومَن انتهت خدمتُه في مارس لا يتراكم له استحقاقٌ في أبريل</b> — وحسابٌ يتجاهل
    /// تاريخَ الانتهاء يُضخّم بدلَ الإجازة بكلّ يومٍ مرّ منذ المغادرة حتى التصفية.
    /// </remarks>
    public static decimal AccruedDaysFor(Employee employee, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(employee);

        var until = employee.TerminationDate is DateOnly t && t < asOf ? t : asOf;
        return AccruedDays(employee.HireDate, until);
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١١٧ · ‏`treasury-payroll-assets#9`) <b>أقصى مدّةٍ نظاميّةٍ لإجازةٍ مدفوعةٍ بنصّ — و<c>null</c> لما لا مدّةَ له.</b>
    ///
    /// <para>‏هذه الأنواعُ تُصرف كاملةً فلا تخصم الدورةُ منها شيئاً، <b>فطلبٌ يتجاوز مدّتَه أجرٌ لم يُستحقّ</b>؛ والزائدُ يُقدَّم طلباً
    /// ‏بلا أجرٍ أو سنويّاً. والمرضيّةُ لا حدَّ لها هنا: ما بعد مئةٍ وعشرين يوماً بلا أجرٍ بتدرّجها (<see cref="SickPayFactor"/>).
    /// ‏<b>والأرقامُ نصُّ النظام بعد تعديلات م/٤٤</b> (2025-02-19) — تُراجَع إن عُدّل.</para>
    /// </summary>
    public static int? StatutoryMaxDays(LeaveType type) => type switch
    {
        LeaveType.Marriage => 5,
        LeaveType.Bereavement => 5,
        LeaveType.Hajj => 15,
        LeaveType.Maternity => 84,
        _ => null,
    };

    /// <summary>(المادة 114) <b>سنتا خدمةٍ متّصلتان قبل إجازة الحجّ.</b></summary>
    public const int HajjMinimumServiceYears = 2;

    /// <summary>
    /// <b>نسبةُ أجرِ اليوم المرضيّ (المادة 117) — بحسب ما مضى من أيّامٍ مرضيّةٍ في السنة.</b>
    ///
    /// <para><b>والتدرّجُ نظاميّ</b>: الثلاثون الأولى بأجرٍ كامل، والستّون التالية بثلاثة
    /// أرباع، <b>وما بعد التسعين بلا أجر</b>. <b>ودفعُ الكامل عن التسعين كلِّها يُكلّف
    /// المنشأةَ ربعَ أجرِ شهرين عن كلّ موظّفٍ يمرض طويلاً</b> — ولا يُستردّ.</para>
    /// </summary>
    /// <param name="dayIndexInYear">ترتيبُ اليوم المرضيّ في السنة — <b>يبدأ من واحد</b>.</param>
    public static decimal SickPayFactor(int dayIndexInYear) => dayIndexInYear switch
    {
        <= 0 => throw new AccountingException("ترتيب اليوم المرضي يبدأ من واحد."),
        <= 30 => 1m,
        <= 90 => 0.75m,
        _ => 0m,
    };

    /// <summary>
    /// <b>أجرُ فترةٍ مرضيّةٍ كاملةٍ — يوماً بيوم لا بنسبةٍ واحدة.</b>
    /// </summary>
    /// <remarks>
    /// <b>وفترةٌ تعبر الحدَّ تُدفع بنسبتين</b>: مَن استهلك ثمانيةً وعشرين يوماً ثمّ مرض خمسةً
    /// يُدفع له يومان بالكامل وثلاثةٌ بثلاثة أرباع. <b>وتطبيقُ نسبةِ أوّل يومٍ على الفترة
    /// كلِّها يدفع زيادةً أو نقصاً في كلّ حالةٍ تعبر الحدّ.</b>
    /// </remarks>
    /// <param name="alreadyUsedInYear">ما استُهلك من أيّامٍ مرضيّةٍ في السنة قبل هذه الفترة.</param>
    public static decimal SickLeavePay(decimal monthlyWage, int days, int alreadyUsedInYear)
    {
        if (days < 0) throw new AccountingException("عدد الأيام لا يكون سالباً.");
        if (alreadyUsedInYear < 0) throw new AccountingException("المستهلك سابقاً لا يكون سالباً.");

        var daily = DailyWage(monthlyWage);
        var total = 0m;
        for (var i = 1; i <= days; i++)
            total += daily * SickPayFactor(alreadyUsedInYear + i);

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١١٦ · ‏`treasury-payroll-assets#9`) <b>أيّامُ الخصم المرضيّ في شهرٍ — موزونةً بتدرّج المادة 117.</b>
    ///
    /// <para>كلُّ يومٍ مرضيٍّ معتمدٍ يقع في الشهر يُخصم منه <c>1 − نسبةِ أجره</c>: صفرٌ في الثلاثين الأولى، وربعٌ في الستّين التالية،
    /// <b>ويومٌ كاملٌ بعد التسعين</b>. <b>وترتيبُ اليوم في «السنة الواحدة»</b> — وهي بنصّ المادة «السنةُ التي تبدأ من تاريخ أوّل إجازةٍ
    /// مرضيّة»، متّصلةً أو متقطّعة — <b>لا في السنة التقويميّة</b>؛ ومتى انقضت سنتُها بدأت سنةٌ جديدةٌ من أوّل يومٍ مرضيٍّ بعدها.</para>
    ///
    /// <para><b>والأيّامُ مجموعةٌ لا مجموعُ مُدَد</b> — فيومٌ تحمله فترتان لا يُعدّ مرّتين.</para>
    /// </summary>
    /// <param name="approvedSickPeriods">فتراتُ الموظّف المرضيّةُ المعتمدة كلُّها حتى نهاية الشهر — فالترتيبُ يحتاج ما قبله.</param>
    public static decimal SickDeductionDays(
        IEnumerable<(DateOnly From, DateOnly To)> approvedSickPeriods, DateOnly monthStart, DateOnly monthEnd)
    {
        ArgumentNullException.ThrowIfNull(approvedSickPeriods);

        var days = new SortedSet<DateOnly>();
        foreach (var (from, to) in approvedSickPeriods)
            for (var d = from; d <= to && d <= monthEnd; d = d.AddDays(1))
                days.Add(d);

        DateOnly? yearStart = null;
        var index = 0;
        var weighted = 0m;
        foreach (var day in days)
        {
            if (yearStart is null || day >= yearStart.Value.AddYears(1))
            {
                yearStart = day;
                index = 0;
            }

            index++;
            if (day >= monthStart)
                weighted += 1m - SickPayFactor(index);
        }

        return weighted;
    }

    /// <summary>
    /// <b>بدلُ الإجازة عن رصيدٍ غيرِ مستعمل (المادة 111).</b>
    ///
    /// <para><b>وهو حقٌّ نقديٌّ لا يسقط</b>: تُدفع أجرةُ الأيّام المتبقّية عند انتهاء الخدمة
    /// <b>مهما طال تركُها</b>. <b>ومنشأةٌ لا تُخصّص له تُفاجأ عند كلّ مغادرة.</b></para>
    /// </summary>
    public static decimal LeaveCompensation(decimal monthlyWage, decimal unusedDays)
    {
        if (unusedDays <= 0m) return 0m;

        return Math.Round(DailyWage(monthlyWage) * unusedDays, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>هل يُدفع أجرُ هذا النوع؟ — <b>و«بلا أجر» وحدَها لا تُدفع</b>.</summary>
    /// <remarks>
    /// <b>والمرضيّةُ مدفوعةٌ بالتدرّج لا بالكامل</b> — انظر <see cref="SickLeavePay"/>.
    /// فهي هنا «مدفوعة» بمعنى أنّها لا تُخصم خصمَ الغياب، <b>ومقدارُها يُحسب بنسبته</b>.
    /// </remarks>
    public static bool IsPaid(LeaveType type) => type != LeaveType.Unpaid;

    /// <summary>هل تُخصم هذه الإجازةُ من الرصيد السنويّ المتراكم؟</summary>
    /// <remarks>
    /// <b>والسنويّةُ وحدَها</b>: المرضيّةُ والوضعُ والزواجُ حقوقٌ مستقلّةٌ بنصّ النظام،
    /// <b>وخصمُها من الرصيد السنويّ يأكل حقّاً بحقّ</b> — فيخرج الموظّفُ بلا إجازةٍ سنويّةٍ
    /// لأنّه مرض.
    /// </remarks>
    public static bool ConsumesAnnualBalance(LeaveType type) => type == LeaveType.Annual;
}
