using Brqosh.Domain.Companies;
using Brqosh.Domain.Sales;

namespace Brqosh.Domain.Tests;

/// <summary>
/// (CEEF-229 · م1-١ب) <b>ما ينقص المشتري يُقال باسمه — عند الحفظ لا عند رفض الهيئة.</b>
///
/// <para>الفاتورة القياسية (B2B) تذهب إلى <b>المقاصّة قبل التسليم</b>، وطرفُ مشتريها معرَّف:
/// اسمٌ مسجَّل وعنوانٌ وطنيّ. وكان طرفُ المشتري يخرج باسمٍ ورقمٍ ضريبيّ وحدهما — بينما البائع
/// أخذ هذه الحقول كلَّها في Z-10.</para>
///
/// <para><b>والتمييزُ جوهريّ لا تجميليّ:</b> العميل النقديّ (بلا رقم ضريبيّ) تُصدَر له فاتورةٌ
/// <b>مبسّطة</b> تُرفع تقريراً بعد البيع ولا تحتاج عنواناً — والكاشيرُ لا يسأل الزبونَ عن حيّه.
/// فلو طُلب العنوانُ من الجميع لتعطّل البيعُ النقديّ كلُّه.</para>
/// </summary>
public class BuyerIdentityGapsTests
{
    private static Customer WithVat(string vat = "311111111100003") =>
        Customer.Create(1, "عميل", vat);

    private static NationalAddress CompleteAddress() =>
        NationalAddress.Create(
            buildingNumber: "4321", streetName: "طريق العليا", districtName: "الملز",
            cityName: "الرياض", postalZone: "54321", countrySubentity: "منطقة الرياض",
            additionalNumber: "8765");

    /// <summary>
    /// <b>القياس المركزيّ.</b> عميلٌ ذو رقمٍ ضريبيّ بلا هويةٍ نظامية — النقصُ يُسمّى حقلاً حقلاً.
    /// </summary>
    [Fact]
    public void AVatRegisteredCustomer_WithoutIdentity_NamesEveryGap()
    {
        var gaps = WithVat().StandardInvoiceGaps();

        Assert.NotEmpty(gaps);
        Assert.Contains(gaps, g => g.Contains("الاسم المسجَّل", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>والعميلُ النقديّ لا يُطالَب بشيء</b> — فاتورتُه مبسّطة، ولو طُولب لتعطّل البيعُ النقديّ.
    /// </summary>
    [Fact]
    public void ACashCustomer_IsAskedForNothing()
    {
        var walkIn = Customer.Create(1, "عميل نقدي");

        Assert.False(walkIn.SupportsStandardTaxInvoice);
        Assert.Empty(walkIn.StandardInvoiceGaps());
    }

    /// <summary>وعميلٌ مكتملُ الهوية يمرّ — وإلا لكان الحارس يمنع كلّ بيعٍ آجل.</summary>
    [Fact]
    public void ACompleteBuyer_HasNoGaps()
    {
        var customer = WithVat();
        customer.UpdateRegulatoryIdentity("مؤسسة العميل التجارية", CompleteAddress());

        Assert.Empty(customer.StandardInvoiceGaps());
    }

    /// <summary>
    /// <b>وحقلٌ واحدٌ ناقص يُسمَّى وحده</b> — فالرسالة تدلّ على ما يُملأ، لا تقول «ناقص».
    /// </summary>
    [Fact]
    public void OneMissingField_IsNamedAlone()
    {
        var customer = WithVat();
        customer.UpdateRegulatoryIdentity(
            "مؤسسة العميل التجارية",
            NationalAddress.Create(
                buildingNumber: "4321", streetName: "طريق العليا", districtName: "الملز",
                cityName: "الرياض", postalZone: "", countrySubentity: "منطقة الرياض",
                additionalNumber: "8765"));

        var gaps = customer.StandardInvoiceGaps();

        Assert.Single(gaps);
        Assert.Contains("البريد", gaps[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> رفعُ الرقم الضريبيّ عن عميلٍ ناقصٍ يُسكت المطالبة — فالمطالبةُ
    /// معلَّقةٌ بنوع الفاتورة لا بالعميل. ولولا ذلك لَما فرّق الاختبارُ الأوّل بين سببين.
    /// </summary>
    [Fact]
    public void TheDemand_FollowsTheInvoiceType_NotTheCustomer()
    {
        var withVat = WithVat();
        Assert.NotEmpty(withVat.StandardInvoiceGaps());

        var withoutVat = Customer.Create(1, "عميل");
        Assert.Empty(withoutVat.StandardInvoiceGaps());
    }
}
