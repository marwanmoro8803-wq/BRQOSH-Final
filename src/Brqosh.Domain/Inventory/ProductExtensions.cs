namespace Brqosh.Domain.Inventory;

/// <summary>
/// امتدادات الصنف — <see cref="IsStockable"/> محسوبة في الذاكرة فقط.
/// في استعلامات EF استخدم <c>p.Type == ProductType.Stockable</c> أو <c>WhereStockable</c> من Infrastructure.
/// </summary>
public static class ProductExtensions
{
    public static bool IsStockable(this Product product) =>
        product.Type == ProductType.Stockable;
}
