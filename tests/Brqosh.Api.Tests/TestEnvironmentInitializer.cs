using Brqosh.Application.Setup;
using System.Runtime.CompilerServices;

namespace Brqosh.Api.Tests;

/// <summary>
/// (CEEF-228) يُعلن هذا التجميع عن بيئته صراحةً.
///
/// صار الافتراض في <c>AppEnvironmentConfig.Resolve()</c> هو <b>الإنتاج</b> — يفشل إلى الأمان
/// لأن المثبِّت لا يضبط <c>BRQOSH_ENV</c> فكان كل تركيب حقيقيّ يعمل في وضع التطوير،
/// وهو ما كان يُطفئ حرّاساً مكتوبةً وصحيحة (كشف رمز OTP، قبول SQLite، تجاوز RBAC).
/// الاختبارات تُصرّح بأنها اختبارات بدل أن تستفيد من افتراضٍ متساهل.
///
/// ملاحظة مقصودة: نضبط <c>BRQOSH_ENV</c> ولا نضبط <c>BRQOSH_TEST</c> — الأخير يُطفئ
/// طبقة الصلاحيات كاملةً (<c>SecurityContext.AutomatedTestBypass</c>)، ولا نريد ذلك هنا.
/// </summary>
internal static class TestEnvironmentInitializer
{
    [ModuleInitializer]
    internal static void DeclareTestEnvironment()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BRQOSH_ENV")))
            Environment.SetEnvironmentVariable("BRQOSH_ENV", "test");

        // ‏(م-٤ · ن-٣٨) ‏**ويُفرَض القرارُ صراحةً — فمتغيّرُ العمليّة وحدَه لا يكفي.**
        //
        // ‏`AppEnvironmentConfig.Decide` يُعطي الأولويّةَ **للمصدر الرسميّ على مستوى الجهاز**:
        // ‏متى قال «إنتاج» فلا شيءَ يخفضه — ولا حتى `BRQOSH_ENV=test` أعلاه. والمُنصِّبُ
        // ‏يضبط ذلك المصدرَ على كلّ جهازٍ نُصِّب عليه المنتَج.
        //
        // ‏**فبلا هذا السطر تكون الحزمةُ رهينةَ جهازِ من يُشغّلها**: تمرّ عند من لم يُنصّب،
        // ‏وتسقط عند من نصّب — ومضيفُ الاختبار يطلب `BRQOSH_API_KEY` فلا يُبنى أصلاً.
        // ‏وقد رُصد هذا الاعتمادُ في مراجعةٍ خصميّةٍ قبل أن يقع.
        AppEnvironmentConfig.SetTestDefault(AppEnvironment.Test);
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Test);
    }
}
