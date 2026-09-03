using Brqosh.Domain.Accounting;
using Brqosh.Domain.Payroll;

namespace Brqosh.Domain.Tests.Payroll;

/// <summary>
/// (CEEF-233 · ز-١) <b>حسابُ الإجازات — نظامُ العمل، المواد 109 و111 و117.</b>
///
/// <para><b>ما كان: لا كيانَ ولا خدمةَ ولا شاشة.</b> فرصيدُ الإجازات — <b>وهو دَينٌ نقديٌّ
/// بنصّ المادة 111</b> — لا يظهر في الدفاتر بريال، ويُدار في جدولٍ خارجيّ.</para>
///
/// <para><b>وهذا الملفُّ يقيس الصيغةَ وحدَها</b> — والأثرُ المحاسبيُّ في حزمة التطبيق.
/// <b>فصيغةٌ صحيحةٌ تُقيَّد في حسابٍ خاطئ تُبقي العطلَ كما كان</b>، والعكسُ كذلك.</para>
/// </summary>
public class LeaveEntitlementTests
{
    private static readonly DateOnly Hire = new(2020, 1, 1);

    /// <summary>
    /// <b>القياسُ الحاكم: واحدٌ وعشرون يوماً قبل الخمس، وثلاثون بعدها (المادة 109).</b>
    ///
    /// <para><b>والمدّةُ تُقسم عند الحدّ ولا تُطبَّق النسبةُ الأعلى بأثرٍ رجعيّ</b>:
    /// مَن خدم ستَّ سنواتٍ يستحقّ <c>5×21 + 1×30 = 135</c> يوماً — <b>لا <c>6×30 = 180</c></b>.
    /// <b>والفرقُ خمسةٌ وأربعون يوماً</b>، تُدفع نقداً عند المغادرة.</para>
    /// </summary>
    [Theory]
    [InlineData(1, 21)]
    [InlineData(3, 63)]
    [InlineData(5, 105)]
    [InlineData(6, 135)]
    [InlineData(10, 255)]
    public void Accrual_IsTwentyOneBeforeFiveYears_AndThirtyAfter(int years, decimal expected)
    {
        var asOf = Hire.AddDays((int)Math.Round(years * 365.25));

        Assert.Equal(expected, Math.Round(LeaveEntitlementCalculator.AccruedDays(Hire, asOf)));
    }

    /// <summary>
    /// <b>ويتراكم يوماً بيوم لا سنةً بسنة.</b>
    ///
    /// <para>ستّةُ أشهرٍ ⟵ نحو <b>عشرةِ أيّامٍ ونصف</b>. <b>وتقريبُها إلى صفرٍ يحرم
    /// الموظّفَ</b> — ورصيدُه يُدفع نقداً عند المغادرة، <b>فهو مالٌ لا تقدير</b>.</para>
    /// </summary>
    [Fact]
    public void Accrual_IsProRata_NotWholeYears()
    {
        var half = LeaveEntitlementCalculator.AccruedDays(Hire, Hire.AddDays(183));

        Assert.InRange(half, 10m, 11m);
        Assert.NotEqual(0m, half);
    }

    /// <summary>
    /// <b>ولا استحقاقَ قبل التعيين — ولا في يومه.</b>
    /// </summary>
    [Fact]
    public void NoAccrual_BeforeHireDate()
    {
        Assert.Equal(0m, LeaveEntitlementCalculator.AccruedDays(Hire, Hire));
        Assert.Equal(0m, LeaveEntitlementCalculator.AccruedDays(Hire, Hire.AddDays(-30)));
    }

    /// <summary>
    /// <b>ويتوقّف التراكمُ يومَ انتهاء الخدمة لا يومَ الحساب.</b>
    ///
    /// <para><b>وحسابٌ يتجاهل تاريخَ الانتهاء يُضخّم بدلَ الإجازة بكلّ يومٍ مرّ منذ
    /// المغادرة حتى التصفية</b> — وقد تمرّ شهور.</para>
    /// </summary>
    [Fact]
    public void Accrual_StopsAtTermination_NotAtTheQueryDate()
    {
        var employee = Employee.Create(1, "E-1", "موظّف", "1011111111",
            EmployeeNationality.Saudi, "محاسب", Hire);
        employee.Terminate(new DateOnly(2022, 1, 1));

        var atTermination = LeaveEntitlementCalculator.AccruedDaysFor(employee, new DateOnly(2022, 1, 1));
        var muchLater = LeaveEntitlementCalculator.AccruedDaysFor(employee, new DateOnly(2026, 1, 1));

        Assert.Equal(atTermination, muchLater);
    }

    /// <summary>
    /// <b>والأجرُ اليوميُّ = الشهريُّ ÷ 30 — مهما كان عددُ أيّام الشهر.</b>
    ///
    /// <para><b>والقسمةُ على أيّام الشهر الفعليّة تُنتج أجرَين مختلفين لليوم نفسِه</b>:
    /// يومٌ في فبراير أغلى من يومٍ في يناير، <b>وهو ما يُنازِع فيه الموظّفُ بحقّ</b>.</para>
    /// </summary>
    [Fact]
    public void DailyWage_IsMonthlyOverThirty()
    {
        Assert.Equal(300m, LeaveEntitlementCalculator.DailyWage(9_000m));
        Assert.Equal(1_500m, LeaveEntitlementCalculator.LeaveCompensation(9_000m, 5m));
    }

    /// <summary>
    /// <b>وسُلَّمُ الأجر المرضيّ (المادة 117): ثلاثون كاملاً، وستّون بثلاثة أرباع، ثمّ بلا أجر.</b>
    ///
    /// <para><b>ودفعُ الكامل عن التسعين كلِّها يُكلّف المنشأةَ ربعَ أجرِ شهرين</b> عن كلّ
    /// موظّفٍ يمرض طويلاً — <b>ولا يُستردّ</b>.</para>
    /// </summary>
    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(30, 1.0)]
    [InlineData(31, 0.75)]
    [InlineData(90, 0.75)]
    [InlineData(91, 0.0)]
    [InlineData(200, 0.0)]
    public void SickPay_FollowsTheStatutoryTiers(int dayIndex, decimal factor) =>
        Assert.Equal(factor, LeaveEntitlementCalculator.SickPayFactor(dayIndex));

    /// <summary>
    /// <b>وفترةٌ تعبر الحدَّ تُدفع بنسبتين لا بنسبةٍ واحدة.</b>
    ///
    /// <para>مَن استهلك ثمانيةً وعشرين يوماً ثمّ مرض خمسةً: <b>يومان بالكامل وثلاثةٌ
    /// بثلاثة أرباع</b>. وبأجرٍ 9,000 (اليوميُّ 300): <c>2×300 + 3×225 = 1,275</c>.
    /// <b>وتطبيقُ نسبةِ أوّل يومٍ على الفترة كلِّها يدفع 1,500 — زيادةً في كلّ حالةٍ
    /// تعبر الحدّ.</b></para>
    /// </summary>
    [Fact]
    public void SickLeavePay_SplitsAcrossTheTierBoundary()
    {
        Assert.Equal(1_275m, LeaveEntitlementCalculator.SickLeavePay(9_000m, days: 5, alreadyUsedInYear: 28));
    }

    /// <summary>
    /// <b>و«بلا أجر» وحدَها لا تُدفع — والسنويّةُ وحدَها تُخصم من الرصيد.</b>
    ///
    /// <para><b>وخصمُ المرضيّة من الرصيد السنويّ يأكل حقّاً بحقّ</b>: يخرج الموظّفُ بلا
    /// إجازةٍ سنويّةٍ لأنّه مرض، <b>وهما حقّان مستقلّان بنصّ النظام</b>.</para>
    /// </summary>
    [Fact]
    public void OnlyUnpaidIsUnpaid_AndOnlyAnnualConsumesTheBalance()
    {
        Assert.False(LeaveEntitlementCalculator.IsPaid(LeaveType.Unpaid));
        foreach (var t in new[] { LeaveType.Annual, LeaveType.Sick, LeaveType.Maternity,
                                  LeaveType.Marriage, LeaveType.Bereavement, LeaveType.Hajj, LeaveType.Exam })
            Assert.True(LeaveEntitlementCalculator.IsPaid(t));

        Assert.True(LeaveEntitlementCalculator.ConsumesAnnualBalance(LeaveType.Annual));
        foreach (var t in new[] { LeaveType.Sick, LeaveType.Unpaid, LeaveType.Maternity,
                                  LeaveType.Marriage, LeaveType.Bereavement })
            Assert.False(LeaveEntitlementCalculator.ConsumesAnnualBalance(t));
    }

    /// <summary>
    /// <b>وأيّامُ الطلب تشمل الطرفين.</b>
    ///
    /// <para><b>فإجازةُ يومٍ واحدٍ يومٌ لا صفر</b>، والفرقُ المجرّد يُسقط يوماً من كلّ
    /// إجازةٍ في المنشأة — <b>وهو يومُ أجرٍ في كلّ طلب</b>.</para>
    /// </summary>
    [Fact]
    public void RequestDays_CountBothEnds()
    {
        var one = LeaveRequest.Create(1, 1, "LV-1", LeaveType.Annual,
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1));
        var five = LeaveRequest.Create(1, 1, "LV-2", LeaveType.Annual,
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5));

        Assert.Equal(1, one.Days);
        Assert.Equal(5, five.Days);
    }

    /// <summary>
    /// <b>ونطاقٌ مقلوبٌ يُرفض — وإلّا كسب الموظّفُ إجازةً بطلبها.</b>
    ///
    /// <para><b>وأيّامٌ سالبةٌ تُضاف إلى الرصيد بدل أن تُنقصه.</b></para>
    /// </summary>
    [Fact]
    public void AReversedRange_IsRefused() =>
        Assert.Throws<AccountingException>(() => LeaveRequest.Create(1, 1, "LV-3",
            LeaveType.Annual, new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 1)));
}
