using Brqosh.Domain.Modules;

namespace Brqosh.Application.Setup;

/// <summary>حالة الإعداد الأولي — تُحفظ بعد إتمام معالج أول تشغيل.</summary>
/// <param name="CompanyName">
/// <b>الاسمُ المعروض</b> — ويُعاد كتابتُه من هويّة القاعدة بعد قبولِ المالك، فيتبع
/// أيَّ تصحيحٍ يجريه في «إعدادات المنشأة».
/// </param>
/// <param name="LicensedCompanyName">
/// (م-٣ · ن-٣٥) <b>الاسمُ الذي قُبل به الترخيص — يُثبَّت مرّةً ولا يُعاد كتابتُه.</b>
///
/// <para><b>العطل:</b> كان الترخيصُ يُقاس على <see cref="CompanyName"/> نفسِه، <b>وهو
/// الحقلُ الذي يُعيد التطبيقُ كتابتَه</b> من هويّة القاعدة. فمن صحّح خطأً مطبعيّاً في اسم
/// منشأته وقبِل مزامنةَ الهويّة، <b>وجد الإنتاجَ مقفلاً عند الإقلاع التالي</b> برسالة
/// «الترخيص لمنشأة أخرى» — والمنشأةُ منشأتُه.</para>
///
/// <para><b>ولم يكن ذلك سهواً بل علاجاً لمشكلةٍ مقابلة:</b> المزامنةُ أُضيفت لأنّ من
/// يُصحّح الاسمَ كان يُتَّهم بعد أشهرٍ بأنّ قاعدتَه «تحمل دفاتر منشأة أخرى». <b>فالعلاجُ
/// الأوّلُ صار سببَ الثاني</b> — والحلُّ فصلُ الحقلين لا التنازلُ عن أحدهما.</para>
///
/// <para><b>ولم يُفكَّ الرباط:</b> الترخيصُ ما زال مربوطاً باسمِ منشأةٍ وبمعرّفِ جهاز،
/// و<c>CompanyMismatch</c> ما زال يقع. <b>المتغيّرُ أنّ المقيسَ عليه لا يتحرّك.</b></para>
///
/// <para>و<c>null</c> تعني تنصيباً سبق هذا الحقل — فيُثبَّت عند أوّل قبولٍ ناجح.</para>
/// </param>
public record SetupState(
    bool IsCompleted,
    BusinessProfile Profile,
    string CompanyName,
    string VatRegistrationNumber,
    DateTime CompletedAtUtc,
    string CountryTemplateId = "saudi-arabia",
    string? LicensedCompanyName = null);

/// <summary>مخزن حالة الإعداد — التنفيذ الحالي ملف محلي، وسيصبح PostgreSQL في مرحلة قاعدة البيانات.</summary>
public interface ISetupStateStore
{
    SetupState? Load();
    void Save(SetupState state);
}
