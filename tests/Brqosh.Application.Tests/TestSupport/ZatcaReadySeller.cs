using Brqosh.Domain.Companies;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.TestSupport;

/// <summary>
/// (CEEF-229 · م2-١٣أ) <b>منشأةٌ مكتملةُ الهوية — كما يجب أن تكون قبل تفعيل الفوترة.</b>
///
/// <para><b>لماذا وُجد هذا:</b> <c>DatabaseInitializer</c> لا يملأ الاسمَ المسجَّل ولا رقمَ
/// السجل التجاريّ ولا العنوانَ الوطنيّ — فالمنشأةُ الجديدة ناقصةٌ بالتعريف. وكانت
/// <b>ستةَ عشرَ اختباراً</b> في سبعة ملفّات تُفعّل الفوترة على منشأةٍ كهذه، فتُصدر مستنداتٍ
/// ينقصها طرفُ البائع كاملاً — أي أنّ <b>كلَّ اختبارات ZATCA في المستودع كانت تقيس تهيئةً
/// ترفضها الهيئة</b>.</para>
///
/// <para>ولم يظهر ذلك قبل إضافة الحارس في <c>ZatcaEInvoiceGenerator</c>، لأنّ لا اختبار كان
/// يُصادق ما يخرج من المولّد فعلاً — وهو ما أُغلق في م2-١٣.</para>
///
/// <para>فبيانات هذه المنشأة هي <b>الحالة الطبيعية</b>: منشأةٌ سعودية استوفت ما تطلبه الهيئة
/// من طرف البائع. واستعمالُها في كلّ اختبار ZATCA يجعل ما يُقاس شبيهاً بما يُشحن.</para>
/// </summary>
public static class ZatcaReadySeller
{
    /// <summary>
    /// (CEEF-229 · م2-١٣ب) <b>شهادةُ توقيعٍ حقيقية — تُولَّد مرّةً لكلّ تشغيل.</b>
    ///
    /// <para><b>لماذا:</b> كانت الاختباراتُ الستةَ عشرَ نفسُها تُفعّل الفوترة بمسارَي شهادةٍ
    /// <b>فارغَين</b>. فكان <c>TrySignWithResult</c> يُعيد <c>null</c> صامتاً، وتخرج
    /// المستنداتُ <b>بلا توقيع</b> وبرمز QR من الطور الأوّل وببصمةٍ محسوبةٍ بخوارزميةٍ أخرى.
    /// أي أنّ <b>كلَّ اختبارات ZATCA كانت تقيس المسارَ المنحدر</b>، لا المسارَ الذي يُشحن.</para>
    ///
    /// <para>وشهادةٌ موقَّعةٌ ذاتياً بمنحنى EC P-256 تكفي: الغرضُ أن يجري التوقيعُ فعلاً
    /// ويُتحقَّق منه محلياً — لا أن تُقبل من الهيئة.</para>
    /// </summary>
    public static (string CertPath, string KeyPath) Certificate => LazyCertificate.Value;

    private static readonly Lazy<(string CertPath, string KeyPath)> LazyCertificate = new(() =>
        ZatcaSignatureVerifier.CreateSigningCertificate(
            Path.Combine(Path.GetTempPath(), "brqosh-zatca-signer", Guid.NewGuid().ToString("N"))),
        isThreadSafe: true);

    /// <summary>يُكمل هوية المنشأة النظامية في مكانها — بلا حفظ.</summary>
    public static void Complete(Company company) =>
        company.UpdateRegulatoryIdentity(
            registrationName: "منشأة الاختبار للتجارة",
            commercialRegistrationNumber: "1010123456",
            address: NationalAddress.Create(
                buildingNumber: "1234",
                streetName: "طريق الملك فهد",
                districtName: "العليا",
                cityName: "الرياض",
                postalZone: "12345",
                countrySubentity: "منطقة الرياض",
                additionalNumber: "5678"));

    /// <summary>يُكمل هوية المنشأة ويحفظ — للاختبارات التي لا تُمسك بالكائن.</summary>
    public static async Task CompleteAsync(BrqoshDbContext db, long companyId)
    {
        var company = await db.Companies.SingleAsync(c => c.Id == companyId);
        Complete(company);
        await db.SaveChangesAsync();
    }
}
