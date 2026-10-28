using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// إشعاراتُ المدة ونوع الاشتراك: نافذةُ الإنذار تليق بالخطة، والرسالةُ تسمّي
/// الخطةَ وبقيّةَ الأيام وتاريخَ الانتهاء — بالعربية والإنجليزية. والدائمُ لا يُزعَج.
/// </summary>
public class LicenseNoticeTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(LicensePlan.Trial7, 2)]
    [InlineData(LicensePlan.Annual, 30)]
    public void Warning_windows_fit_the_plan(LicensePlan plan, int expected) =>
        Assert.Equal(expected, LicenseNotices.WarningDays(plan));

    [Fact]
    public void Lifetime_never_warns() => Assert.Null(LicenseNotices.WarningDays(LicensePlan.Lifetime));

    private static LicenseStatus Active(LicensePlan plan, int daysLeft) =>
        new(LicenseKind.Licensed, plan, Now, Now.AddDays(daysLeft), daysLeft, "code", null);

    [Theory]
    [InlineData(LicensePlan.Trial7, 2, true)]
    [InlineData(LicensePlan.Trial7, 3, false)]
    [InlineData(LicensePlan.Trial7, 0, true)]
    [InlineData(LicensePlan.Annual, 30, true)]
    [InlineData(LicensePlan.Annual, 31, false)]
    [InlineData(LicensePlan.Lifetime, 0, false)]
    public void ShouldWarn_follows_the_plan_window(LicensePlan plan, int daysLeft, bool expected) =>
        Assert.Equal(expected, LicenseNotices.ShouldWarn(Active(plan, daysLeft), Now));

    [Fact]
    public void The_trial_message_names_the_plan_and_the_days_left()
    {
        var message = LicenseNotices.StartupMessageAr(Active(LicensePlan.Trial7, 2), Now);
        var english = LicenseNotices.StartupMessageEn(Active(LicensePlan.Trial7, 2), Now);

        Assert.NotNull(message);
        Assert.Contains("تجريبي (7 أيام)", message, StringComparison.Ordinal);
        Assert.Contains("يومَين", message, StringComparison.Ordinal);
        Assert.NotNull(english);
        Assert.Contains("Trial (7 days)", english, StringComparison.Ordinal);
        Assert.Contains("in 2 days", english, StringComparison.Ordinal);
    }

    [Fact]
    public void The_annual_message_names_the_plan_the_days_and_the_expiry_date()
    {
        var status = Active(LicensePlan.Annual, 10);
        var message = LicenseNotices.StartupMessageAr(status, Now);
        var english = LicenseNotices.StartupMessageEn(status, Now);

        Assert.NotNull(message);
        Assert.Contains("سنوي", message, StringComparison.Ordinal);
        Assert.Contains("10", message, StringComparison.Ordinal);
        Assert.Contains(status.ExpiresUtc!.Value.ToString("yyyy-MM-dd"), message, StringComparison.Ordinal);
        Assert.NotNull(english);
        Assert.Contains("Annual", english, StringComparison.Ordinal);
        Assert.Contains("in 10 days", english, StringComparison.Ordinal);
    }

    [Fact]
    public void No_notice_when_the_plan_is_not_due()
    {
        Assert.Null(LicenseNotices.StartupMessageAr(Active(LicensePlan.Annual, 31), Now));
        Assert.Null(LicenseNotices.StartupMessageAr(Active(LicensePlan.Lifetime, 0), Now));
        Assert.Null(LicenseNotices.StartupMessageEn(Active(LicensePlan.Trial7, 3), Now));
    }

    [Fact]
    public void Trial_ending_today_is_phrased_today()
    {
        var message = LicenseNotices.StartupMessageAr(Active(LicensePlan.Trial7, 0), Now);
        var english = LicenseNotices.StartupMessageEn(Active(LicensePlan.Trial7, 0), Now);

        Assert.NotNull(message);
        Assert.Contains("اليوم", message, StringComparison.Ordinal);
        Assert.NotNull(english);
        Assert.Contains("today", english, StringComparison.Ordinal);
    }
}
