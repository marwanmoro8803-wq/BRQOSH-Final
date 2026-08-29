using Brqosh.Domain.Companies;
using Brqosh.Domain.Sales;

namespace Brqosh.Application.Tests.TestSupport;

/// <summary>
/// (CEEF-229 · م1-١ب) <b>مشترٍ مكتملُ الهوية — تهيئةٌ واحدة بدل تكرارِ سبعةِ حقولٍ في كلّ ملفّ.</b>
///
/// <para>حين أُغلق بابُ «الفاتورة القياسية لمشترٍ ناقص» سقط اثنا عشر اختباراً قائماً — لا
/// لأنّ الحارسَ خاطئ، بل لأنّ <b>تهيئاتِها كانت ناقصة</b>: عميلٌ برقمٍ ضريبيّ وبلا اسمٍ مسجَّل
/// ولا عنوانٍ وطنيّ. وهي بعينها صورةُ قاعدةِ بيانات منشأةٍ قائمة اليوم.</para>
///
/// <para>وهذا هو النمطُ نفسه الذي وقع في م2-١٣ مع هوية <b>البائع</b>: الحارسُ الجديد يكشف
/// نقصَ التهيئة قبل أن يكشف عطلَ الشيفرة. فتُكمَل التهيئة، ويبقى النقصُ الحقيقيّ مقيساً في
/// <c>AnIncompleteBuyer_IsRefused_ForAStandardInvoiceOnly</c> وحدَه.</para>
/// </summary>
internal static class StandardBuyerFixture
{
    /// <summary>يُكمل هويةَ المشتري النظامية ويُعيد العميلَ نفسه — ليُكتب في سطرِ الإنشاء.</summary>
    internal static Customer WithStandardIdentity(this Customer customer)
    {
        customer.UpdateRegulatoryIdentity(
            $"مؤسسة {customer.NameAr} التجارية",
            NationalAddress.Create(
                buildingNumber: "4321", streetName: "طريق العليا", districtName: "الملز",
                cityName: "الرياض", postalZone: "54321", countrySubentity: "منطقة الرياض",
                additionalNumber: "8765"));

        return customer;
    }
}
