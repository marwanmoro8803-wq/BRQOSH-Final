using Brqosh.Application.Tests.TestSupport;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٧ · ‏`security-licensing-tenancy#1`) <b>الصلاحيةُ تُقاس على الوقت الفعّال، والجلسةُ الطويلةُ لا تتجاوزها.</b>
///
/// <para>‏كان <c>RsaLicenseValidator</c> يقيس بـ<c>DateTime.UtcNow</c> عند الإقلاع وحدَه، و<c>ClockRollbackDetector</c> يُبلِّغ الرجوعَ
/// ‏ولا يدخل الحكم: <b>فإرجاعُ الساعة يُمدّد الترخيص، وتطبيقٌ لا يُغلق يعمل بترخيصٍ انتهى</b> — وت-١٢ معلَّمٌ مغلقاً. والفعّالُ أبعدُ
/// ‏الساعة وأحدثِ وقتٍ رآه التطبيق، والجلسةُ تُفحص كلَّ ربع ساعة. <b>ولا إقفال</b> (قرارُ ت-١٢): المنتهي الأصيلُ قراءةٌ فقط (ح-١٢٠).</para>
/// </summary>
[Collection("SequentialEnv")]
public class LicenseExpiryFollowsTheEffectiveClockTests
{
    private const string Company = "منشأة الساعة";
    private const string Machine = "MACHINE-1";
    private static readonly DateTime Now = new(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_effective_clock_is_the_later_of_the_device_clock_and_the_high_water_mark()
    {
        Assert.Equal(Now.AddDays(3), LicenseClock.EffectiveNowUtc(Now, Now.AddDays(3)));
        Assert.Equal(Now, LicenseClock.EffectiveNowUtc(Now, Now.AddDays(-3)));
        Assert.Equal(Now, LicenseClock.EffectiveNowUtc(Now, null));
    }

    /// <summary>
    /// ‏ترخيصٌ ينتهي غداً صالحٌ على ساعةٍ أُرجعت، <b>ومنتهٍ على الوقت الفعّال</b> — فيُفتح للقراءة لا يُمدَّد.
    /// </summary>
    [Fact]
    public void A_rolled_back_clock_no_longer_extends_the_licence()
    {
        using var keys = RsaTestKeys.UseForValidation();
        var license = RsaTestKeys.Sign(Company, Machine, days: 1);
        var highWater = DateTime.UtcNow.AddDays(3);

        Assert.True(RsaLicenseValidator.TryValidate(Company, Machine, license, out _));
        Assert.Equal(LicenseVerdict.Expired,
            RsaLicenseValidator.Validate(Company, Machine, license, out _, nowUtc: highWater));
        Assert.True(RsaLicenseValidator.IsGenuineButExpired(Company, Machine, license, nowUtc: highWater));
    }

    [Theory]
    [InlineData(-1, false, true)]   // ‏انتهى قبل ساعة، والجلسةُ تكتب ⟵ تدخل القراءة
    [InlineData(1, false, false)]   // ‏ينتهي بعد ساعة ⟵ تبقى
    [InlineData(-1, true, false)]   // ‏في القراءة أصلاً ⟵ لا تُدخَل ثانيةً
    public void A_running_session_enters_read_only_once_the_licence_expires(int hoursUntilExpiry, bool alreadyReadOnly, bool expected) =>
        Assert.Equal(expected, LicenseSessionGuard.ShouldEnterReadOnly(Now.AddHours(hoursUntilExpiry), Now, alreadyReadOnly));

    /// <summary>شاهدُ ضبط: جلسةٌ بلا ترخيصٍ صالحٍ عند بدئها (تجربةُ التطوير) لا يُغيّرها الحارس.</summary>
    [Fact]
    public void A_session_without_a_valid_licence_at_start_is_left_alone() =>
        Assert.False(LicenseSessionGuard.ShouldEnterReadOnly(null, Now, alreadyReadOnly: false));
}
