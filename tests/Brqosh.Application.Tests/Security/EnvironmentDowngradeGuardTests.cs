using Brqosh.Application.Setup;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (CEEF-229 · م0-١٢) بيئة التشغيل لا تُخفَّض بمتغيّرٍ يملكه المستخدم.
///
/// <para>الموجة الأولى جعلت الافتراض إنتاجاً — وبقي شقّ: <c>setx BRQOSH_ENV development</c>
/// <b>لا يحتاج صلاحية مسؤول Windows</b> لأنه يكتب في نطاق المستخدم، ونطاقُ المستخدم يتغلّب على
/// نطاق الجهاز الذي يكتبه المثبِّت. وبخفضٍ واحد يعود كشفُ رمز OTP على شاشة «نسيت كلمة المرور»،
/// ويُقبل SQLite بدل PostgreSQL، ويصير تجاوز الصلاحيات ممكناً — أي أن كاشيراً أمام الجهاز
/// يستطيع إعادة فتح ما أُغلق.</para>
///
/// <para>المصدر الرسميّ الآن: متغيّر بنطاق <b>الجهاز</b> أو ملفٌّ في مجلد التثبيت — وكلاهما
/// يحتاج صلاحية مسؤول. والاتجاه المسموح واحد: <b>رفعُ التشدّد لا خفضه</b>.</para>
///
/// <para><b>حدّ هذا الحارس، مُصرَّحاً به:</b> يقيس المنطق عبر <see cref="AppEnvironment"/>
/// ولا يكتب في سجلّ النظام ولا في مجلد التثبيت — فذلك يتطلّب صلاحية مسؤول ويُلوّث جهاز المطوّر.
/// التحقق الميدانيّ من أن المثبِّت يضع العلامة فعلاً هو بند تشغيليّ في قائمة الإصدار.</para>
///
/// <para><b>وحسّاسٌ للجهاز — ويُقاس على الحقيقة لا على الافتراض</b> (٢٠٢٦-١٠-٢٧): على جهازٍ
/// منصَّبٍ عليه المنتج يحمل <c>BRQOSH_ENV=production</c> في نطاق الجهاز. فالتوقّعُ المحسوبُ
/// هنا: **إن وُجد مصدرٌ رسميٌّ إنتاجيٌّ فهو النتيجةُ دائماً** (وهو ما يجب أن يقيسه الحارسُ
/// على جهازِ عميل)، وإلا فيُحترم إعلانُ العمليّة. بهذا يبقى الحارسُ صادقاً في البيئتين
/// بدل أن يمرّ على جهاز المطوّر ويسقط على جهاز المنشأة.</para>
/// </summary>
public class EnvironmentDowngradeGuardTests : IDisposable
{
    private readonly string? _previousEnv;
    private readonly string? _previousTest;

    public EnvironmentDowngradeGuardTests()
    {
        _previousEnv = Environment.GetEnvironmentVariable("BRQOSH_ENV");
        _previousTest = Environment.GetEnvironmentVariable("BRQOSH_TEST");
    }

    /// <summary>مصدرُ الجهاز كما يقرؤه المنتج: فارغ ⇒ لا مصدر؛ وإلا فقيمةٌ مُطبَّعة.</summary>
    private static AppEnvironment? MachineAuthoritative()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var raw = Environment.GetEnvironmentVariable("BRQOSH_ENV", EnvironmentVariableTarget.Machine);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return raw.Trim().ToLowerInvariant() switch
        {
            "development" or "dev" => AppEnvironment.Development,
            "test" => AppEnvironment.Test,
            _ => AppEnvironment.Production,
        };
    }

    /// <summary>النتيجةُ المتوقّعةُ لأيّ إعلانِ عمليّة: مصدرُ الجهاز الإنتاجيُّ يغلب.</summary>
    private static AppEnvironment Expected(AppEnvironment declaredWhenNoAuthority) =>
        MachineAuthoritative() is AppEnvironment.Production ? AppEnvironment.Production : declaredWhenNoAuthority;

    /// <summary>
    /// **قراءةُ الجهاز الحقيقيّة** — لا افتراضُ الاختبار. وبابُها الصريح
    /// <see cref="AppEnvironmentConfig.ForceRealForTests"/> لأن `ResetForTests` صار يعود إلى
    /// افتراض التجميعة (‏Test) كي لا يعتمد المصيرُ على ترتيب الاختبارات.
    /// </summary>
    private static AppEnvironment ResolveWith(string? env, string? test)
    {
        Environment.SetEnvironmentVariable("BRQOSH_ENV", env);
        Environment.SetEnvironmentVariable("BRQOSH_TEST", test);
        AppEnvironmentConfig.ForceRealForTests();
        return AppEnvironmentConfig.Current;
    }

    /// <summary>
    /// غياب كل شيء ⟵ إنتاج. الفشل إلى الأمان هو أساس الموجة الأولى، ونؤكّده هنا لئلا
    /// يُنقَض سهواً وأنا أعيد كتابة نفس الدالة.
    /// </summary>
    [Fact]
    public void NoSignalAtAll_ResolvesToProduction()
    {
        Assert.Equal(AppEnvironment.Production, ResolveWith(null, null));
    }

    /// <summary>
    /// الإعلان الصريح عن التطوير يبقى نافعاً على جهاز بلا مصدر رسميّ — ومتى وُجد مصدرٌ
    /// إنتاجيٌّ فهو يغلب (وهذا هو نصفُ الحارس الآخر: لا خفض).
    /// </summary>
    [Theory]
    [InlineData("development", AppEnvironment.Development)]
    [InlineData("test", AppEnvironment.Test)]
    [InlineData("production", AppEnvironment.Production)]
    [InlineData("prod", AppEnvironment.Production)]
    public void ExplicitProcessEnvironment_IsHonouredWithoutAuthoritativeSource(
        string raw, AppEnvironment expected)
    {
        Assert.Equal(Expected(expected), ResolveWith(raw, null));
    }

    /// <summary>harness الاختبارات يُعلن عن نفسه بـ<c>BRQOSH_TEST</c> — ولم يتغيّر هذا.</summary>
    [Fact]
    public void TestHarnessFlag_StillResolvesToTest()
    {
        Assert.Equal(Expected(AppEnvironment.Test), ResolveWith(null, "1"));
    }

    /// <summary>
    /// قيمة غير معروفة تُقرأ تطويراً لا إنتاجاً — فالخطأ الإملائيّ لا يمنح امتيازاً، ومن
    /// يكتب قيمةً بيده يقصد الخروج من الإنتاج.
    /// </summary>
    [Fact]
    public void UnknownValue_IsNotReadAsProduction()
    {
        Assert.Equal(Expected(AppEnvironment.Development), ResolveWith("staging", null));
    }

    /// <summary>
    /// <b>الحارس للحارس:</b> التجاوز المخصّص للاختبارات يتغلّب على كل شيء — وهذا هو الباب
    /// الوحيد المشروع، ولذلك يجب أن يبقى مقصوراً على <c>ForceForTests</c> ومرئياً.
    /// </summary>
    [Fact]
    public void TestOverride_WinsOverEverything()
    {
        try
        {
            ResolveWith("production", null);
            AppEnvironmentConfig.ForceForTests(AppEnvironment.Development);

            Assert.Equal(AppEnvironment.Development, AppEnvironmentConfig.Current);
            Assert.False(AppEnvironmentConfig.IsProduction);
        }
        finally
        {
            AppEnvironmentConfig.ResetForTests();
        }
    }

    /// <summary>
    /// إعادة الضبط تُعيد القراءة فعلاً ولا تُبقي قيمةً مخبّأة — ولولا ذلك لكانت كل
    /// الاختبارات أعلاه تقيس أوّل قراءة وحدها. (والقراءةُ هنا حقيقيّةٌ أيضاً.)
    /// </summary>
    [Fact]
    public void ResetForTests_ActuallyReReadsTheEnvironment()
    {
        Assert.Equal(Expected(AppEnvironment.Development), ResolveWith("development", null));
        Assert.Equal(AppEnvironment.Production, ResolveWith("production", null));
    }

    // ── سياسة «لا خفض» — تُقاس مباشرةً على الدالة النقيّة ────────────────────

    /// <summary>
    /// <b>القياس الأساسيّ لهذه الدفعة.</b> المصدر الرسميّ يقول إنتاج، والمستخدم يحاول الخفض
    /// بكل وسيلة متاحة له — ولا شيء يخفضه.
    /// </summary>
    [Theory]
    [InlineData("development")]
    [InlineData("dev")]
    [InlineData("test")]
    [InlineData("staging")]
    [InlineData("")]
    [InlineData(null)]
    public void AuthoritativeProduction_CannotBeDowngraded(string? userAttempt)
    {
        var decided = AppEnvironmentConfig.Decide(
            authoritative: AppEnvironment.Production, processEnv: userAttempt, testFlag: "1");

        Assert.Equal(AppEnvironment.Production, decided);
    }

    /// <summary>
    /// ولا حتى بـ<c>BRQOSH_TEST=1</c> — وهو المفتاح الذي يُطفئ طبقة الصلاحيات كاملةً.
    /// </summary>
    [Fact]
    public void AuthoritativeProduction_IgnoresTestHarnessFlag()
    {
        Assert.Equal(AppEnvironment.Production,
            AppEnvironmentConfig.Decide(AppEnvironment.Production, null, "1"));
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> لولا هذا لكان «أعِد إنتاجاً دائماً» يُمرّر كل ما سبق. المصدر
    /// الرسميّ الذي يعلن تطويراً — جهاز مطوّر مُهيّأ — يُحترم، ومتغيّر العملية يظلّ فعّالاً.
    /// </summary>
    [Theory]
    [InlineData(AppEnvironment.Development, "development", AppEnvironment.Development)]
    [InlineData(AppEnvironment.Development, "test", AppEnvironment.Test)]
    [InlineData(AppEnvironment.Development, null, AppEnvironment.Development)]
    [InlineData(null, "development", AppEnvironment.Development)]
    [InlineData(null, null, AppEnvironment.Production)]
    public void WithoutAuthoritativeProduction_ProcessEnvironmentDecides(
        AppEnvironment? authoritative, string? processEnv, AppEnvironment expected)
    {
        Assert.Equal(expected, AppEnvironmentConfig.Decide(authoritative, processEnv, null));
    }

    /// <summary>
    /// الاتجاه المسموح: <b>رفعُ</b> التشدّد. جهازٌ رسميّه تطوير يستطيع صاحبه إعلان الإنتاج.
    /// </summary>
    [Fact]
    public void RaisingStrictness_IsAllowed()
    {
        Assert.Equal(AppEnvironment.Production,
            AppEnvironmentConfig.Decide(AppEnvironment.Development, "production", null));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("BRQOSH_ENV", _previousEnv);
        Environment.SetEnvironmentVariable("BRQOSH_TEST", _previousTest);
        AppEnvironmentConfig.ResetForTests();
        GC.SuppressFinalize(this);
    }
}
