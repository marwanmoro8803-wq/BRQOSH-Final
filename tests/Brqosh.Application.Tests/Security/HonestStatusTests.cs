using Brqosh.Application.Setup;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (CEEF-229 · م0-١٠ و م0-١١) النظام لا يعلن ما لم يقع.
///
/// <para>العطلان من عائلة واحدة، وهي أخطر عائلة في نظام محاسبيّ: <b>إعلانُ نجاحٍ لم يحدث</b>.
/// لا رقمَ يفسد ولا استثناءَ يظهر — يثق المستخدم بما يقرأ ويكتشف الحقيقة بعد شهور.</para>
///
/// <para><b>م0-١٠:</b> وضع المحاكاة كان يُعلّم الفاتورة <c>Cleared</c>/<c>Reported</c> — وهما
/// حالتا القبول الفعليّ لدى الهيئة — بلا أي إرسال. والقالب يفرض المحاكاة عند كل تفعيل.</para>
///
/// <para><b>م0-١١:</b> قناة تسليم رمز التحقق كانت تعيد <c>Success: true</c> في الإنتاج بلا
/// تسليم — فمسار «نسيت كلمة المرور» ميتٌ ورسالتُه تقول عكس ذلك.</para>
/// </summary>
public class HonestStatusTests
{
    // ── م0-١٠ · حالة الامتثال ───────────────────────────────────────────────

    /// <summary>
    /// المحاكاة ليست قبولاً. هذا هو التمييز الذي كان غائباً، وعليه يقوم الإصلاح كلّه.
    /// </summary>
    [Fact]
    public void SimulatedLocally_IsNotAcknowledgedByAuthority()
    {
        Assert.False(ZatcaComplianceStatus.SimulatedLocally.IsAcknowledgedByAuthority());

        Assert.True(ZatcaComplianceStatus.Reported.IsAcknowledgedByAuthority());
        Assert.True(ZatcaComplianceStatus.Cleared.IsAcknowledgedByAuthority());
        Assert.True(ZatcaComplianceStatus.Accepted.IsAcknowledgedByAuthority());
    }

    /// <summary>
    /// ولا الحالات التي لم تُرسل بعد. حارسٌ للحارس: لو صنّف التابع كلّ شيء «غير مقبول»
    /// لمرّ الاختبار السابق بلا معنى.
    /// </summary>
    [Theory]
    [InlineData(ZatcaComplianceStatus.Generated)]
    [InlineData(ZatcaComplianceStatus.ReadyToReport)]
    [InlineData(ZatcaComplianceStatus.ReadyToClear)]
    [InlineData(ZatcaComplianceStatus.Rejected)]
    public void UnsentStatuses_AreNotAcknowledged(ZatcaComplianceStatus status)
    {
        Assert.False(status.IsAcknowledgedByAuthority());
    }

    /// <summary>
    /// التسمية المعروضة عربية وتقول الحقيقة صراحةً — والمطبوعة كانت تعرض الاسم البرمجيّ
    /// «Cleared» على فاتورة عربية.
    /// </summary>
    [Fact]
    public void SimulatedLocally_ReadsAsNotSentToAuthority()
    {
        var label = ZatcaComplianceStatus.SimulatedLocally.NameAr();

        Assert.Contains("محاكاة", label);
        Assert.Contains("لم يُرسل", label);
        Assert.DoesNotContain("معتمَد", label);
    }

    /// <summary>كل حالة لها تسمية عربية — لا اسم برمجيّ يتسرّب إلى المستخدم.</summary>
    [Theory]
    [InlineData(ZatcaComplianceStatus.Generated)]
    [InlineData(ZatcaComplianceStatus.ReadyToReport)]
    [InlineData(ZatcaComplianceStatus.ReadyToClear)]
    [InlineData(ZatcaComplianceStatus.Reported)]
    [InlineData(ZatcaComplianceStatus.Cleared)]
    [InlineData(ZatcaComplianceStatus.Accepted)]
    [InlineData(ZatcaComplianceStatus.Rejected)]
    [InlineData(ZatcaComplianceStatus.SimulatedLocally)]
    public void EveryStatus_HasArabicLabel(ZatcaComplianceStatus status)
    {
        var label = status.NameAr();

        Assert.NotEqual(status.ToString(), label);
        Assert.DoesNotContain(label, "abcdefghijklmnopqrstuvwxyz", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// تفعيل التكامل لا يُرجِع البيئة إلى المحاكاة. كان كل تفعيلٍ من القالب يدهس اختيار
    /// المالك، فتُعلَّم فواتيره مقبولةً بلا إرسال.
    /// </summary>
    [Fact]
    public void EnablingIntegration_DoesNotForceSimulation()
    {
        var settings = ZatcaSettings.CreateDefault(companyId: 1);
        settings.Update(isEnabled: true, ZatcaEnvironment.Production, "https://gw.example/api",
            certificatePath: "", privateKeyPath: "", apiToken: "");

        settings.ApplyTemplateDefaults(enabled: true, apiBaseUrl: "https://gw.example/api");

        Assert.Equal(ZatcaEnvironment.Production, settings.Environment);
        Assert.Equal("https://gw.example/api", settings.ApiBaseUrl);
    }

    // ── م0-١١ · قناة رمز التحقق ─────────────────────────────────────────────

    /// <summary>
    /// <b>القياس الأساسيّ.</b> بلا قناة مهيّأة في الإنتاج: فشلٌ صريح لا نجاحٌ كاذب.
    /// </summary>
    [Fact]
    public void ProductionWithoutChannel_FailsExplicitly()
    {
        var result = new UnavailableOtpNotificationChannel().SendOtp("+966500000000", "الرمز: 123456");

        Assert.False(result.Success);
        Assert.Null(result.DevHint);
        Assert.NotNull(result.Error);
        Assert.Contains("مدير النظام", result.Error);
    }

    /// <summary>
    /// خارج الإنتاج يبقى العرض داخل التطبيق عاملاً — الإصلاح لا يكسر بيئة التطوير.
    /// </summary>
    [Fact]
    public void OutsideProduction_InAppChannelStillRevealsCode()
    {
        var result = new InAppOtpNotificationChannel().SendOtp("+966500000000", "الرمز: 654321");

        Assert.True(result.Success);
        Assert.Equal(AppEnvironmentConfig.IsProduction ? null : "654321", result.DevHint);
    }

    /// <summary>
    /// حارسٌ للحارس: القناة داخل التطبيق لا تكشف الرمز في الإنتاج. هذا هو إغلاق ح-١
    /// الأصليّ، ونؤكّده هنا لئلا يُنقض سهواً وهو يقع في المسار نفسه.
    /// </summary>
    [Fact]
    public void InAppChannel_NeverRevealsCodeInProduction()
    {
        try
        {
            AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);

            var result = new InAppOtpNotificationChannel().SendOtp("+966500000000", "الرمز: 111222");

            Assert.True(result.Success);      // القناة تدّعي النجاح…
            Assert.Null(result.DevHint);      // …ولا تكشف الرمز — وهذا سبب وجود القناة العاجزة
        }
        finally
        {
            AppEnvironmentConfig.ResetForTests();
        }
    }

    /// <summary>
    /// <b>الحارس السلوكيّ الحقيقيّ:</b> ماذا يختار المصنع فعلاً في الإنتاج بلا قناة مهيّأة؟
    ///
    /// كان يعيد <see cref="InAppOtpNotificationChannel"/> <b>دائماً</b> — وهو ما جعل المسار ميتاً
    /// ومعلناً نجاحاً. وأيّ اختبارٍ يفحص القنوات فرادى كان سيمرّ رغم ذلك، لأن العطل في
    /// <b>الاختيار</b> لا في القنوات.
    /// </summary>
    [Fact]
    public void Factory_InProductionWithoutConfiguredChannel_ResolvesFailingChannel()
    {
        try
        {
            AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);
            WhatsAppNotificationChannelFactory.ResetCache();

            var channel = WhatsAppNotificationChannelFactory.Resolve();

            Assert.IsType<UnavailableOtpNotificationChannel>(channel);
            Assert.False(channel.SendOtp("+966500000000", "الرمز: 999888").Success);
        }
        finally
        {
            AppEnvironmentConfig.ResetForTests();
            WhatsAppNotificationChannelFactory.ResetCache();
        }
    }

    /// <summary>وخارج الإنتاج يختار العرض داخل التطبيق — فبيئة التطوير تبقى عاملة.</summary>
    [Fact]
    public void Factory_OutsideProduction_ResolvesInAppChannel()
    {
        try
        {
            AppEnvironmentConfig.ForceForTests(AppEnvironment.Development);
            WhatsAppNotificationChannelFactory.ResetCache();

            Assert.IsType<InAppOtpNotificationChannel>(WhatsAppNotificationChannelFactory.Resolve());
        }
        finally
        {
            AppEnvironmentConfig.ResetForTests();
            WhatsAppNotificationChannelFactory.ResetCache();
        }
    }
}
