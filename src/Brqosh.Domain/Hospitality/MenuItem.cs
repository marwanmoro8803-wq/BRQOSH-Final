using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Hospitality;

/// <summary>قسمُ الصنف في القائمة — ويحكم العرضَ وتقاريرَ الأداء.</summary>
public enum MenuSection
{
    Appetizer = 0,
    Main = 1,
    Dessert = 2,
    Beverage = 3,
    Other = 4,
}

/// <summary>
/// <b>صنفٌ في قائمة الطعام — ووصفتُه هي ما يربطه بالمخزون.</b>
///
/// <para><b>ما كان:</b> ملفُّ «الضيافة» في شاشة التأسيس، <b>ولا قائمةَ طعامٍ ولا وصفة</b>.
/// فالمطعمُ يبيع «برجر» صنفاً في نقطة البيع، <b>والخبزُ واللحمُ في المستودع لا ينقصان
/// بريالٍ واحد</b>: المخزونُ يبقى كما اشتُري، وتكلفةُ المبيعات <b>صفر</b>، والربحُ في
/// القوائم <b>مئةٌ بالمئة</b>. ثمّ يأتي الجردُ آخرَ السنة بفارقٍ هائلٍ لا تفسيرَ له —
/// <b>فيُقيَّد «عجزُ جرد» عن سنةٍ كاملةٍ من البيع الصحيح</b>.</para>
///
/// <para><b>والوصفةُ ليست وثيقةً بل آلة</b>: تُفجَّر عند البيع فتصرف مكوّناتِها. وبغير
/// التفجير تبقى جدولاً يُطبع ولا يُحرّك ريالاً.</para>
/// </summary>
public class MenuItem
{
    private readonly List<RecipeComponent> _components = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }

    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public MenuSection Section { get; private set; }
    public decimal SalePrice { get; private set; }

    /// <summary>
    /// <b>الصنفُ الذي يُباع به الطبقُ في الفواتير ونقطة البيع.</b>
    ///
    /// <para><b>وبه يصير الصرفُ آلياً</b>: بيعُ هذا الصنف يُفجّر الوصفةَ ويصرف مكوّناتِها.
    /// <b>وبغيره يبقى الصرفُ زرّاً يُضغط</b> — فمَن نسي لم يصرف، ويظهر الفرقُ عجزَ جردٍ
    /// آخرَ السنة.</para>
    ///
    /// <para><b>والصنفُ المرتبط خدميٌّ لا مخزنيّ</b>: الطبقُ لا يُخزَّن — مكوّناتُه تُخزَّن.
    /// <b>وربطُه بصنفٍ مخزنيٍّ يخصم الطبقَ ومكوّناتِه معاً</b> فتُقيَّد التكلفةُ مرّتين.</para>
    /// </summary>
    public long? SaleProductId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyList<RecipeComponent> Components => _components;

    // EF Core
    private MenuItem()
    {
        Code = null!;
        NameAr = null!;
    }

    private MenuItem(long companyId, string code, string nameAr, MenuSection section, decimal salePrice)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        Section = section;
        SalePrice = salePrice;
    }

    public static MenuItem Create(long companyId, string code, string nameAr,
        MenuSection section, decimal salePrice)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود صنف القائمة مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم صنف القائمة مطلوب.");
        if (salePrice < 0m)
            throw new AccountingException("سعر البيع لا يكون سالباً.");

        return new MenuItem(companyId, code.Trim(), nameAr.Trim(), section,
            Math.Round(salePrice, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// يُضيف مكوّناً — <b>بكمّيته للحصّة الواحدة</b>.
    /// </summary>
    /// <param name="quantityPerServing">
    /// ما يُصرف من هذا المكوّن لحصّةٍ واحدة. <b>وهو للحصّة لا للدفعة</b>: وصفةٌ تُكتب
    /// لعشر حصصٍ وتُفجَّر كأنّها واحدة <b>تصرف عشرةَ أضعاف المخزون</b> عن كلّ طلب.
    /// </param>
    public void AddComponent(long productId, decimal quantityPerServing)
    {
        if (productId <= 0)
            throw new AccountingException("الصنف المخزنيّ مطلوب للمكوّن.");
        if (quantityPerServing <= 0m)
            throw new AccountingException("كمية المكوّن يجب أن تكون أكبر من صفر.");

        // ‏**ومكوّنٌ يتكرّر يُجمع لا يُستبدل**: وصفةٌ فيها «زيت» مرّتين لغرضين تصرف
        // ‏مجموعَهما. **والاستبدالُ يُسقط أحدَهما صامتاً** فينقص المصروفُ عن الحقيقة،
        // ‏ويظهر الفرقُ عجزَ جردٍ آخرَ السنة.
        var existing = _components.FirstOrDefault(c => c.ProductId == productId);
        if (existing is not null)
        {
            existing.Add(quantityPerServing);
            return;
        }

        _components.Add(RecipeComponent.Create(productId, quantityPerServing));
    }

    /// <summary>
    /// يربط الطبقَ بصنف البيع.
    ///
    /// <para><b>ويُشترط أن يكون خدمياً</b> — والحارسُ عند الرابط لا هنا، فالنطاقُ لا يقرأ
    /// الأصناف. <b>والاشتراطُ مذكورٌ ليُقرأ</b>.</para>
    /// </summary>
    public void LinkSaleProduct(long? saleProductId)
    {
        if (saleProductId is long id && id <= 0)
            throw new AccountingException("صنف البيع غير صالح.");

        SaleProductId = saleProductId;
    }

    public void RemoveComponent(long productId) =>
        _components.RemoveAll(c => c.ProductId == productId);

    public void SetPrice(decimal salePrice)
    {
        if (salePrice < 0m) throw new AccountingException("سعر البيع لا يكون سالباً.");
        SalePrice = Math.Round(salePrice, 2, MidpointRounding.AwayFromZero);
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}

/// <summary>مكوّنٌ في وصفةٍ — صنفٌ مخزنيٌّ وكمّيتُه للحصّة الواحدة.</summary>
public class RecipeComponent
{
    public long ProductId { get; private set; }
    public decimal QuantityPerServing { get; private set; }

    // EF Core
    private RecipeComponent() { }

    internal static RecipeComponent Create(long productId, decimal quantityPerServing) =>
        new() { ProductId = productId, QuantityPerServing = quantityPerServing };

    internal void Add(decimal quantity) => QuantityPerServing += quantity;
}
