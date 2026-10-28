using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Desktop.Views.Pages;
using Brqosh.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.ViewModels;

/// <summary>
/// منطق شاشة الأصناف خارج الشاشة: التحميل والإيقاف وإعادة التفعيل والحفظ هنا، والشاشة تعرض وتسأل المستخدم فقط.
/// </summary>
public sealed class ProductsViewModel
{
    public const int PageSize = 200;
    private List<Product> _items = [];

    public IReadOnlyList<ProductsPage.Row> Rows { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int StockableCount { get; private set; }

    public bool CanChangeActivation => PermissionService.CanPostAccounting(AppServices.CurrentUser);

    public Product? Find(long id) => _items.SingleOrDefault(p => p.Id == id);

    public async Task LoadAsync(string filter, bool showInactive)
    {
        await using var db = AppServices.CreateDb();
        // (CEEF-229 · م2-٢٣) الموقوف يظهر عند طلبه — لا يختفي إلى الأبد.
        var query = db.Products.Where(p =>
            p.CompanyId == AppServices.CompanyId && (p.IsActive || showInactive));
        if (!string.IsNullOrEmpty(filter))
            query = query.Where(p => p.Code.Contains(filter) || p.NameAr.Contains(filter));

        _items = await query.OrderBy(p => p.Code).Take(PageSize).ToListAsync();
        TotalCount = await db.Products.CountAsync(p =>
            p.CompanyId == AppServices.CompanyId && (p.IsActive || showInactive));
        StockableCount = _items.Count(p => p.Type == ProductType.Stockable);

        var visible = string.IsNullOrEmpty(filter) ? _items
            : _items.Where(p => p.Code.Contains(filter, StringComparison.OrdinalIgnoreCase) || p.NameAr.Contains(filter)).ToList();
        Rows = visible.Select(p => new ProductsPage.Row(p.Id, p.Code, p.NameAr, p.Unit,
            p.Type == ProductType.Stockable ? "مخزني" : "خدمة", p.SalePrice, p.WeightedAverageCost, p.Barcode)).ToList();
    }

    /// <summary>الحارس عند نقطة الكتابة لا في إخفاء الزر وحده.</summary>
    public async Task DeactivateAsync(long id)
    {
        if (!CanChangeActivation)
            throw new UnauthorizedAccessException(PermissionService.DeniedActionMessage("الإيقاف", "deactivate"));
        await using var db = AppServices.CreateDb();
        var product = await db.Products.SingleAsync(p => p.Id == id);
        product.Deactivate();
        await db.SaveChangesAsync();
    }

    public async Task ReactivateAsync(long id)
    {
        if (!CanChangeActivation)
            throw new UnauthorizedAccessException(PermissionService.DeniedActionMessage("إعادة التفعيل", "reactivate"));
        await using var db = AppServices.CreateDb();
        var product = await db.Products.SingleAsync(p => p.Id == id);
        product.Reactivate();
        await db.SaveChangesAsync();
    }
}
