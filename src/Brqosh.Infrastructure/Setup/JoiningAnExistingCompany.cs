using Brqosh.Application.Setup;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Infrastructure.Setup;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥٠ · <c>installer-deployment-ops#9</c>) <b>جهازٌ ثانٍ ينضمّ إلى منشأةٍ قائمة — صراحةً.</b>
///
/// <para><b>ما كان:</b> معالجُ التهيئة على جهازٍ ثانٍ (أو حسابِ ويندوز ثانٍ) يطلب اسمَ منشأةٍ ورقمَها ومديراً جديداً،
/// ثمّ يُوصَل بقاعدةٍ فيها منشأة — <b>فيتجاهل الإقلاعُ ما أُدخل كلَّه</b>: <c>DatabaseInitializer</c> يُعيد المنشأةَ القائمة،
/// و<c>CreateInitialAdminAsync</c> يخرج صامتاً لأنّ فيها مستخدمين، وفحصُ الهويّة يُتخطّى «لأنّه تركيبٌ جديد». فيظنّ المُعِدُّ
/// أنّه أنشأ منشأتَه ومديرَها، <b>ثمّ لا يدخل بكلمة المرور التي اختارها</b>، ويُقاس ترخيصُه على اسمٍ ليس في القاعدة.</para>
///
/// <para><b>وما صار:</b> قاعدةٌ فيها منشأةٌ تُعرض للانضمام بقرار؛ والانضمامُ يأخذ هويّةَ المنشأة من القاعدة، <b>ولا يُنشئ
/// مديراً</b> — الدخولُ بحسابٍ موجودٍ فيها. والقرارُ هنا دالّةٌ نقيّة؛ والسؤالُ في <c>App</c>.</para>
/// </summary>
public static class JoiningAnExistingCompany
{
    /// <summary>هل يلزم سؤالُ الانضمام — قاعدةٌ فيها منشأةٌ وتركيبٌ يُعَدّ للتوّ.</summary>
    public static bool MustAsk(DatabaseIdentity identity) => !identity.IsVirgin;

    /// <summary>
    /// حالةُ الإعداد بعد الانضمام: <b>اسمُ المنشأة ورقمُها من القاعدة</b>، والاسمُ المُرخَّصُ ما حمله التركيبُ إن وُجد وإلا اسمُ المنشأة.
    /// </summary>
    public static SetupState Joined(SetupState setup, DatabaseIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(setup);
        if (identity.IsVirgin)
            throw new InvalidOperationException("لا منشأةَ في القاعدة ينضمّ إليها التركيب.");

        var name = identity.CompanyName ?? setup.CompanyName;
        return setup with
        {
            CompanyName = name,
            VatRegistrationNumber = identity.VatNumber ?? setup.VatRegistrationNumber,
            LicensedCompanyName = setup.LicensedCompanyName ?? name,
        };
    }

    /// <summary>حالةُ الإعداد حين يُرفض الانضمام — <b>غيرُ مكتملة</b>، فيُعرض المعالجُ عند الإقلاع التالي ولا يُتبنّى شيء.</summary>
    public static SetupState Declined(SetupState setup) => setup with { IsCompleted = false };
}
