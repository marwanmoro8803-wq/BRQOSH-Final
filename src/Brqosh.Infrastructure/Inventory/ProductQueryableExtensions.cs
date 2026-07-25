using Brqosh.Domain.Inventory;

namespace Brqosh.Infrastructure.Inventory;

/// <summary>فلاتر أصناف قابلة للترجمة إلى SQL — بديل IsStockable المحسوبة.</summary>
public static class ProductQueryableExtensions
{
    public static IQueryable<Product> WhereStockable(this IQueryable<Product> query) =>
        query.Where(p => p.Type == ProductType.Stockable);

    public static IQueryable<Product> WhereActiveStockable(this IQueryable<Product> query, long companyId) =>
        query.Where(p => p.CompanyId == companyId && p.IsActive && p.Type == ProductType.Stockable);
}
