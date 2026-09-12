using Brqosh.Application.Setup;

namespace Brqosh.Api.Tests;

/// <summary>
/// (م-٤ · ن-٣٨) <b>العمليّةُ الواحدةُ كانت تحمل جوابين لسؤالٍ واحد: «هل أنا إنتاج؟»</b>
///
/// <para><c>Program.cs</c> كان يقرأ <c>app.Environment.IsProduction()</c> — أي
/// <c>ASPNETCORE_ENVIRONMENT</c>، <b>متغيّرَ عمليّةٍ يضبطه أيُّ مُطلِق</b>. بينما
/// <c>ApiSecurityContextMiddleware</c> يسأل <c>AppEnvironmentConfig</c>، وهو مُحصَّن: يقرأ
/// مصدراً رسميّاً على مستوى الجهاز، <b>ويفشل إلى الإنتاج</b> عند الغياب، <b>ولا يُخفَّض
/// بإعلان عمليّة</b>.</para>
///
/// <para><b>وشرطُ المصادقة كان يركب الأضعف:</b> مضيفٌ أعلن المُنصِّبُ أنّه إنتاج، ويُطلَق
/// بلا <c>ASPNETCORE_ENVIRONMENT</c>، <b>يعمل بلا مفتاحٍ ولا مصادقة</b> — بينما طبقةُ
/// الصلاحيّات تعامله إنتاجاً وتُنزل دورَه إلى <c>Viewer</c>. <b>وهو الخفضُ الذي أُغلق في
/// حملةٍ سابقة، عائداً من بابٍ آخر.</b></para>
///
/// <para><b>وسياسةُ القرار نفسُها مقيسةٌ سلفاً</b> في
/// <c>EnvironmentDowngradeGuardTests</c> — جدولُ <c>Decide</c> بكلّ تركيباته. <b>وما لم
/// يكن مقيساً أنّ الواجهةَ تسأله أصلاً</b>، وذلك هو النمطُ الجامعُ لهذا التدقيق:
/// سياسةٌ صحيحةٌ ومختبَرة، <b>ومستهلِكٌ لا يستشيرها</b>.</para>
/// </summary>
public class OneProductionDecisionTests : IDisposable
{
    public void Dispose()
    {
        // ‏يُعاد إلى **الاختبار** لا إلى علامة الجهاز — فالإسقاطُ يُعيد القرارَ إلى المصدر
        // ‏الرسميّ، وهو «إنتاج» على كلّ جهازٍ نُصِّب عليه المنتَج.
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Test);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ السلوكيُّ المركزيّ:</b> متى قال المصدرُ المُحصَّن «إنتاج»، <b>يرفض المضيفُ
    /// الإقلاعَ بلا مفتاح</b> — ولو كانت بيئةُ الاستضافة تطويراً.
    ///
    /// <para>و<c>IsolatedApiFactory</c> يُعلن <c>Environments.Development</c> صراحةً
    /// (‏<c>builder.UseEnvironment</c>)، <b>فلو بقي القرارُ على المصدر الضعيف لأقلع المضيفُ
    /// بلا مفتاحٍ ومرّ هذا الاختبار خضراءَ على كودٍ معطوب</b>. فرفضُه هو الدليل.</para>
    /// </summary>
    [Fact]
    public void AHardenedProductionMarker_ForcesTheApiKeyRequirement_EvenInADevelopmentHost()
    {
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);

        using var factory = new IsolatedApiFactory();

        var ex = Assert.ThrowsAny<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("BRQOSH_API_KEY", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وهو ألزمُ من الرفض:</b> مضيفُ اختبارٍ يُقلع كما كان.
    ///
    /// <para>فقرارٌ يقول «إنتاج» دائماً يُغلق الثغرةَ ويمنع كلَّ تشغيل، <b>ولا يُميّز بينهما
    /// إلّا هذا</b>.</para>
    /// </summary>
    [Fact]
    public void ATestEnvironment_StillStartsWithoutAKey()
    {
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Test);

        using var factory = new IsolatedApiFactory();
        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    /// <summary>
    /// <b>وإثباتُ أكوادٍ ثابت:</b> الإقلاعُ يجمع المصدرين ولا يكتفي بالأضعف.
    ///
    /// <para><b>والجمعُ بـ«أو» لا باختيار أحدهما:</b> أيُّ مصدرٍ يقول «إنتاج» يُصدَّق،
    /// <b>فلا يُخفَّض القرارُ أبداً</b>. والخطأُ في اتجاه التشديد يُكتشف بمفتاحٍ ناقص،
    /// وفي اتجاه التساهل <b>لا يُكتشف إلّا بعد الاختراق</b>.</para>
    /// </summary>
    [Fact]
    public void Startup_CombinesBothSources_AndNeverDowngrades()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "Brqosh.Api", "Program.cs"));

        var code = string.Join('\n', source
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        Assert.Contains(
            "app.Environment.IsProduction() || AppEnvironmentConfig.IsProduction",
            code, StringComparison.Ordinal);

        // ‏شاهدُ ضبطٍ موجب: الماسحُ يقرأ ملفّاً حقيقيّاً فيه ما نعلم أنّه فيه.
        Assert.Contains("mustAuthenticate", code, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }
}
