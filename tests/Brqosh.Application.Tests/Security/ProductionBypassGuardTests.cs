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
    /// كان يُلغي فحص الترخيص كاملاً في أيّ وضع. وبعد (الترخيصُ الرقميّ) صارت
    /// «الفترةُ التجريبية» سلوكَ المنتج في الإنتاج أيضاً — فالفرقُ بين الإنتاج وغيره
    /// يُقاس هنا بعد انقضاء التجربة: التجاوزُ لا يفتحها.
    /// </summary>
    [Fact]
    public void UiTestEnvironmentVariable_DoesNotBypassLicence_InProduction()
    {
        Environment.SetEnvironmentVariable("BRQOSH_UITEST", "1");
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);
        TrialPeriod.StoreOverride = new ExpiredTrialStore();

        try
        {
            // بلا ترخيص، وبعد انقضاء التجربة، لا تجاوزَ في الإنتاج.
            Assert.False(LicenseStore.ValidateForCompany(
                $"شركة-غير-مرخّصة-{Guid.NewGuid():N}", DateTime.UtcNow));
        }
        finally
        {
            TrialPeriod.StoreOverride = null;
        }
    }

    /// <summary>والفترةُ التجريبية نفسُها تعمل في الإنتاج — سلوكُ المنتج: سبعةُ أيامٍ ثمّ طلبُ ترخيص.</summary>
    [Fact]
    public void A_fresh_install_is_trialled_in_production_too()
    {
        Environment.SetEnvironmentVariable("BRQOSH_UITEST", "1");
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);
        TrialPeriod.StoreOverride = new InMemoryTrialStore();

        try
        {
            Assert.True(LicenseStore.ValidateForCompany(
                $"شركة-جديدة-{Guid.NewGuid():N}", DateTime.UtcNow));
        }
        finally
        {
            TrialPeriod.StoreOverride = null;
        }
    }

    /// <summary>وخارج الإنتاج يبقى التجاوز عاملاً — وإلا تعطّل مسح الواجهة الآلي.</summary>
    [Fact]
    public void UiTestEnvironmentVariable_StillBypassesLicence_OutsideProduction()
    {
        Environment.SetEnvironmentVariable("BRQOSH_UITEST", "1");
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Development);
        TrialPeriod.StoreOverride = new ExpiredTrialStore();

        try
        {
            Assert.True(LicenseStore.ValidateForCompany(
                $"شركة-غير-مرخّصة-{Guid.NewGuid():N}", DateTime.UtcNow));
        }
        finally
        {
            TrialPeriod.StoreOverride = null;
        }
    }

    private sealed class InMemoryTrialStore : TrialPeriod.ITrialStampStore
    {
        public TrialPeriod.TrialStampRecord? Record { get; set; }

        public TrialPeriod.TrialStampRecord? Read() => Record;

        public void Write(TrialPeriod.TrialStampRecord record) => Record = record;
    }

    private sealed class ExpiredTrialStore : TrialPeriod.ITrialStampStore
    {
        public TrialPeriod.TrialStampRecord? Read() =>
            TrialPeriod.Stamp(DateTime.UtcNow.AddDays(-8));

        public void Write(TrialPeriod.TrialStampRecord record)
        {
        }
    }

    /// (CEEF-229 · م2-١) <b>حُذف تجاوز RBAC من أصله</b> — فلم يعد الادعاء «ميتٌ في الإنتاج»
    /// بل «غير موجود في أيّ بيئة». جدول الحقيقة الكامل في
    /// <see cref="SecurityContextProductionGuardTests"/>: الحرّاس الأربعة تحت خمس حالات فاعل
    /// وثلاث بيئات. أُبقيت هذه الإشارة كي لا يُعاد التجاوز ظنّاً أن حذفه سهو.
}
