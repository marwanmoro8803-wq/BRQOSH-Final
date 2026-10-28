using System.Runtime.CompilerServices;
using Brqosh.Application.Setup;

namespace Brqosh.Desktop.Tests;

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

        // **القرارُ يُفرَض صراحةً** (٢٠٢٦-١٠-٢٧): الجهازُ المنصَّبُ عليه BRQOSH يحمل
        // `BRQOSH_ENV=production` في نطاق الجهاز، وهو مصدرٌ رسميٌّ لا يخفضه متغيّرُ العمليّة —
        // فتسقط اختباراتٌ لا علاقةَ لها بالجهاز الذي تشغّلها. و`Brqosh.Api.Tests` سبق إلى هذا
        // الإصلاح نفسه (ن-٣٨)؛ فيُطبَّق هنا بالمثل.
        AppEnvironmentConfig.SetTestDefault(AppEnvironment.Test);
        AppEnvironmentConfig.ForceForTests(AppEnvironment.Test);
    }
}
