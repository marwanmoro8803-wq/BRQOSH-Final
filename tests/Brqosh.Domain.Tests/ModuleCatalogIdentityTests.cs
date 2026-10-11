using Brqosh.Domain.Modules;

namespace Brqosh.Domain.Tests;

/// <summary>
/// (CEEF-229 · م2-١٩) <b>هوية أنظمة العمل — «ما من نظامٍ يبيع بضاعةً مخزنية إلا ويشتريها».</b>
///
/// <para><b>العطل، وهو تأسيسيّ:</b> <c>BusinessProfile.Retail</c> يمنح عشر وحداتٍ فيها المخزون
/// ونقطة البيع — <b>وليس فيها المشتريات</b>. و<c>NavigationCatalog</c> يربط مجموعة «المشتريات
/// والموردون» كلَّها بتلك الوحدة، فتختفي من القائمة: الموردون · أوامر الشراء · أذونات الاستلام
/// · فواتير المشتريات · إشعارات المدين.</para>
///
/// <para><b>والأثر ماليّ مباشر:</b> منشأةٌ تشتري لتبيع لا تستطيع تسجيل مورّدٍ ولا فاتورة شراء —
/// فيُقدَّم إقرار القيمة المضافة <b>بلا مدخلاتٍ قابلة للخصم</b>، وتُسدَّد للهيئة أكثر ممّا عليها.
/// وأشدّ منه: البضاعة لا تدخل المخزون أصلاً، فيصطدم أوّلُ بيعٍ بحارس «الرصيد غير كافٍ».</para>
///
/// <para><b>ولا رجعة:</b> <c>Company.UpdateBusinessProfile</c> بلا مستدعٍ واحد في المستودع،
/// وشاشة الإعدادات تعرض نظام العمل نصّاً للقراءة. فالعطل يقع في الدقيقة الأولى من التركيب
/// ويحكم على المنشأة إلى الأبد.</para>
///
/// <para><b>ولماذا هوية لا تعداد:</b> إصلاحُ <c>Retail</c> وحده يترك <c>Hospitality</c> —
/// والمطعم يشتري كما يشتري المتجر. والهوية تحرس كل نظامٍ يُضاف بعد اليوم.</para>
/// </summary>
public class ModuleCatalogIdentityTests
{
    public static TheoryData<BusinessProfile> AllProfiles()
    {
        var data = new TheoryData<BusinessProfile>();
        foreach (var profile in Enum.GetValues<BusinessProfile>())
            data.Add(profile);
        return data;
    }

    /// <summary>
    /// <b>الهوية المركزية.</b> من يملك مخزوناً يملك مشتريات — فالبضاعة لا تُخلق في المستودع.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void AnyProfileWithInventory_AlsoHasPurchasing(BusinessProfile profile)
    {
        var modules = ModuleCatalog.ResolveModules(profile);
        if (!modules.Contains(ModuleKey.Inventory)) return;

        Assert.True(modules.Contains(ModuleKey.Purchasing),
            $"نظام العمل «{profile}» يملك المخزون بلا مشتريات — فلا سبيل لإدخال بضاعةٍ إليه، " +
            $"ويُقدَّم الإقرار الضريبيّ بلا مدخلاتٍ قابلة للخصم.");
    }

    /// <summary>
    /// ومن يبيع — نقطةَ بيعٍ أو فاتورةً — يملك مخزوناً. فالبيع بلا مخزونٍ لا يُقيّد تكلفة.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void AnyProfileWithPointOfSale_AlsoHasInventoryAndSales(BusinessProfile profile)
    {
        var modules = ModuleCatalog.ResolveModules(profile);
        if (!modules.Contains(ModuleKey.PointOfSale)) return;

        Assert.True(modules.Contains(ModuleKey.Inventory), $"«{profile}»: نقطة بيع بلا مخزون.");
        Assert.True(modules.Contains(ModuleKey.Sales), $"«{profile}»: نقطة بيع بلا مبيعات.");
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> نظامٌ بلا مخزون لا يُفرض عليه المشتريات — فالهوية شرطية لا
    /// تعميمٌ يمنح كل نظامٍ كل شيء. ولولا هذا لكان «أضِف كل الوحدات للجميع» يُمرّرها.
    /// </summary>
    [Fact]
    public void AProfileWithoutInventory_IsNotForcedToHavePurchasing()
    {
        var realEstate = ModuleCatalog.ResolveModules(BusinessProfile.RealEstate);

        Assert.DoesNotContain(ModuleKey.Inventory, realEstate);
        Assert.DoesNotContain(ModuleKey.Manufacturing, realEstate);
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١١٩ · ‏`ui-ux-i18n-printing#7` · ‏`sector-modules#11`) <b>منشأةٌ لا تبيع بضاعةً تشتري خدماتٍ
    /// ‏ومصروفاتٍ — بمورّدٍ وفاتورةِ شراءٍ وضريبةِ مدخلات.</b>
    ///
    /// <para>‏كان «محاسبة عامّة» و«عقارات» بلا مشتريات: لا مورّدَ ولا فاتورةَ شراء، فسندُ الصرف الظاهرُ لهما لا يُستعمل،
    /// ‏والصيانةُ والمرافقُ ومقاولو الباطن تُدفع بلا ضريبة مدخلاتٍ قابلةٍ للخصم — فيُسدَّد للهيئة أكثرُ ممّا عليها.
    /// ‏<b>ولم يكن يكفي إضافتُها</b>: المشترياتُ كانت تجرّ المخزون، فتُمنح العقاراتُ مستودعاتٍ لا تحتاجها.</para>
    /// </summary>
    [Theory]
    [InlineData(BusinessProfile.General)]
    [InlineData(BusinessProfile.RealEstate)]
    public void A_service_business_buys_from_suppliers_without_holding_stock(BusinessProfile profile)
    {
        var modules = ModuleCatalog.ResolveModules(profile);

        Assert.Contains(ModuleKey.Purchasing, modules);
        Assert.DoesNotContain(ModuleKey.Inventory, modules);
    }

    /// <summary>‏(ح-١١٩) والمشترياتُ لا تجرّ المخزون — ومن يحتاجه يُعلنه، فالمقاولاتُ والتصنيعُ يبقيان بمخزونهما.</summary>
    [Fact]
    public void Purchasing_does_not_pull_inventory_and_the_profiles_that_hold_stock_keep_it()
    {
        Assert.DoesNotContain(ModuleKey.Inventory, ModuleCatalog.DependenciesOf(ModuleKey.Purchasing));

        foreach (var profile in new[] { BusinessProfile.Construction, BusinessProfile.Manufacturing, BusinessProfile.Retail,
                     BusinessProfile.Healthcare, BusinessProfile.Logistics, BusinessProfile.Hospitality })
            Assert.Contains(ModuleKey.Inventory, ModuleCatalog.ResolveModules(profile));
    }

    /// <summary>ودفتر الأستاذ مضمونٌ للجميع — لا نظام عملٍ بلا محاسبة.</summary>
    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void EveryProfile_HasTheGeneralLedger(BusinessProfile profile) =>
        Assert.Contains(ModuleKey.GeneralLedger, ModuleCatalog.ResolveModules(profile));
}
