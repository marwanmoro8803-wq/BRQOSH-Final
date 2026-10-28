using System.Runtime.CompilerServices;
using Brqosh.Application.Setup;

namespace Brqosh.Application.Tests;

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

        // **القرارُ يُفرَض صراحةً** (٢٠٢٦-١٠-٢٧): متغيّرُ العمليّة وحدَه لا يكفي.
        //
        // `AppEnvironmentConfig.Decide` يُعطي الأولويّةَ **للمصدر الرسميّ على مستوى الجهاز**:
        // متى قال «إنتاج» فلا شيءَ يخفضه — ولا حتى `BRQOSH_ENV=test` أعلاه. والمُنصِّبُ يضبط
        // ذلك المصدرَ على كلّ جهازٍ نُصِّب عليه المنتَج.
        //
        // **وهذا ليس افتراضاً**: على جهازِ المالك (‏BRQOSH منصَّبٌ فيه) صار
        // `BRQOSH_ENV=production` في نطاق الجهاز، فسقطت اختباراتُ الترخيص التي تفتح SQLite
        // **98/100** بعد أن كانت 100/100 على جهازٍ غير منصَّب — لا لعيبِ كود، بل لأن الحزمةَ
        // رهينةُ الجهازِ الذي تشغّله. و`Brqosh.Api.Tests` كان قد سبق إلى هذا الإصلاح نفسه
        // (‏ن-٣٨) ووثّق العلّة؛ فيُطبَّق هنا بالمثل.
        AppEnvironmentConfig.SetTestDefault(AppEnvironment.Test);
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Test);
    }
}
