using Brqosh.Application.Setup;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (CEEF-228) حرّاس التجاوزات في وضع الإنتاج.
///
/// السبب الجذريّ الذي أوجب هذا الملف: كانت هذه الحرّاس مكتوبةً وصحيحة، لكنها معلّقة على
/// <c>AppEnvironmentConfig.IsProduction</c> — والافتراض كان <c>Development</c> والمثبِّت
/// لا يضبط <c>BRQOSH_ENV</c>. فقيمةٌ افتراضية واحدة كانت تُطفئها كلها في كل تركيب حقيقيّ.
/// هذه الاختبارات تُثبّت السلوك تحت وضع الإنتاج صراحةً — لا تحت افتراضٍ قد ينقلب.
/// </summary>
[Collection("SequentialEnv")]
public class ProductionBypassGuardTests : IDisposable
{
    private readonly string? _priorUiTest = Environment.GetEnvironmentVariable("BRQOSH_UITEST");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("BRQOSH_UITEST", _priorUiTest);
        AppEnvironmentConfig.ResetForTests();
    }

    /// <summary>
    /// «نسيت كلمة المرور» يجب ألّا تعرض الرمز على الشاشة في الإنتاج — وإلا فأيّ شخص
    /// أمام الجهاز يستولي على أيّ حساب بلا هاتف ولا بريد ولا معرفة سابقة.
    /// </summary>
    [Fact]
    public void OtpCode_IsNeverRevealed_InProduction()
    {
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);

        var result = new InAppOtpNotificationChannel().SendOtp("+966500000000", "الرمز: 483920 صالح 10 دقائق");

        Assert.True(result.Success);
        Assert.Null(result.DevHint);
        Assert.DoesNotContain("483920", OtpHintFormatter.Format(new OtpSendResult("+9665****00", result.DevHint)));
    }

    /// <summary>وفي غير الإنتاج يظهر — وإلا لما أمكن تشغيل التدقيق الآلي، ولصار الاختبار بلا معنى.</summary>
    [Fact]
    public void OtpCode_IsRevealed_OutsideProduction()
    {
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Development);

        var result = new InAppOtpNotificationChannel().SendOtp("+966500000000", "الرمز: 483920 صالح 10 دقائق");

        Assert.Equal("483920", result.DevHint);
    }

    /// <summary>
    /// <c>BRQOSH_UITEST=1</c> متغيّر على مستوى المستخدم — لا يحتاج صلاحية مسؤول Windows.
    /// كان يُلغي فحص الترخيص كاملاً في أيّ وضع.
    /// </summary>
    [Fact]
    public void UiTestEnvironmentVariable_DoesNotBypassLicence_InProduction()
    {
        Environment.SetEnvironmentVariable("BRQOSH_UITEST", "1");
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);

        // بلا ترخيص موقّع مخزَّن، وفي الإنتاج، لا فترة تجريبية ولا تجاوز.
        Assert.False(LicenseStore.ValidateForCompany(
            $"شركة-غير-مرخّصة-{Guid.NewGuid():N}", DateTime.UtcNow));
    }

    /// <summary>وخارج الإنتاج يبقى التجاوز عاملاً — وإلا تعطّل مسح الواجهة الآلي.</summary>
    [Fact]
    public void UiTestEnvironmentVariable_StillBypassesLicence_OutsideProduction()
    {
        Environment.SetEnvironmentVariable("BRQOSH_UITEST", "1");
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Development);

        Assert.True(LicenseStore.ValidateForCompany(
            $"شركة-غير-مرخّصة-{Guid.NewGuid():N}", DateTime.UtcNow));
    }

    /// <summary>
    /// تجاوز RBAC بـ<c>BRQOSH_TEST</c> يجب أن يبقى ميتاً في الإنتاج مهما تسرّب المتغيّر.
    /// (يحرس <c>SecurityContext.AutomatedTestBypass</c> — CEEF-125.)
    /// </summary>
    [Fact]
    public void RbacTestBypass_IsDead_InProduction()
    {
        var priorTest = Environment.GetEnvironmentVariable("BRQOSH_TEST");
        try
        {
            Environment.SetEnvironmentVariable("BRQOSH_TEST", "1");
            AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);
            SecurityContext.CurrentRole = null;

            Assert.Throws<Brqosh.Domain.Accounting.AccountingException>(() => SecurityContext.EnsureCanPost());
        }
        finally
        {
            Environment.SetEnvironmentVariable("BRQOSH_TEST", priorTest);
            SecurityContext.CurrentRole = null;
            AppEnvironmentConfig.ResetForTests();
        }
    }
}
