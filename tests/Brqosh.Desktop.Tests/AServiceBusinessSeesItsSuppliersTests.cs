using Brqosh.Desktop.Navigation;
using Brqosh.Domain.Modules;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١١٩ · ‏`ui-ux-i18n-printing#7`) <b>«محاسبة عامّة» و«عقارات» يريان الموردين وفواتيرَ الشراء —
/// ‏ولا يريان ما يخصّ البضاعة.</b>
///
/// <para>‏كانت القائمةُ تعرض لهما سندَ الصرف ولا مورّدَ يُصرف له ولا فاتورةَ شراء. وإذنُ استلام البضاعة يُدخل إلى مستودع،
/// ‏فلا يظهر إلّا لمن يملك المخزون.</para>
/// </summary>
public class AServiceBusinessSeesItsSuppliersTests
{
    private static HashSet<string> Keys(BusinessProfile profile) =>
        NavigationCatalog.ForProfile(profile).SelectMany(g => g.Pages).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

    [Theory]
    [InlineData(BusinessProfile.General)]
    [InlineData(BusinessProfile.RealEstate)]
    public void A_service_business_sees_suppliers_and_purchase_invoices_but_no_stock_screens(BusinessProfile profile)
    {
        var keys = Keys(profile);

        foreach (var key in new[] { "suppliers", "purchase_invoices", "debit_notes", "payable_aging", "payment_vouchers" })
            Assert.Contains(key, keys);
        foreach (var key in new[] { "goods_receipts", "products", "warehouses" })
            Assert.DoesNotContain(key, keys);
    }

    /// <summary>شاهدُ ضبط: من يملك المخزونَ يرى إذنَ الاستلام كما كان.</summary>
    [Theory]
    [InlineData(BusinessProfile.Retail)]
    [InlineData(BusinessProfile.Construction)]
    [InlineData(BusinessProfile.Manufacturing)]
    public void A_stock_holding_business_still_sees_goods_receipts(BusinessProfile profile) =>
        Assert.Contains("goods_receipts", Keys(profile));
}
