using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Inventory;

public record StockBalanceRow(
    long ProductId,
    long WarehouseId,
    string ProductCode,
    string ProductName,
    string WarehouseName,
    string Unit,
    decimal Quantity,
    decimal UnitCost,
    decimal TotalValue,
    /// <summary>
    /// (CEEF-229 · م2-١٧ب) صنفٌ موقوفٌ ما زالت بضاعتُه قائمة — يظهر ليُجرَد ويُصفَّى،
    /// وتَسِمه الشاشةُ «موقوف» فلا يُظنّ متاحاً للبيع.
    /// </summary>
    bool IsStopped = false);

/// <summary>أرصدة المخزون الحالية — محسوبة من الحركات المرحّلة.</summary>
public class StockBalanceQuery(BrqoshDbContext db)
{
    public async Task<IReadOnlyList<StockBalanceRow>> ExecuteAsync(long companyId, long? warehouseId = null,
        bool includeZeroBalances = false)
    {
        var inv = new InventoryService(db);
        // (CEEF-229 · م2-١٧ب) **الإيقافُ لا يُخفي البضاعة.**
        //
        // كان المرشّحُ هنا `p.IsActive`، فيختفي الصنفُ الموقوف ومعه بضاعتُه من الدفتر
        // المساعد بينما يبقى ثمنُها في حساب المخزون 1107 — كسرُ الهوية هـ-٣ بمقدار القيمة
        // كاملةً، مقيسٌ بـ‏−٨٠٠٠ ريالاً في `StoppedItemsKeepTheirStockTests`.
        //
        // وأخطرُ من الرقم أنّ البضاعة تصير **محبوسةً لا تُرى**: ورقةُ الجرد تُبنى من هنا،
        // فما لا يظهر فيها لا يُعَدّ ولا يُسوّى ولا يخرج من المستودع أبداً.
        //
        // فيُرفع المرشّح، ويُسقَط الموقوفُ **الفارغ** وحدَه أدناه — فلا يتضخّم التقريرُ
        // بكلّ صنفٍ أُوقف يوماً، ولا تختفي بضاعةٌ قائمة.
        var products = await db.Products
            .Where(p => p.CompanyId == companyId && p.Type == ProductType.Stockable)
            .OrderBy(p => p.Code)
            .ToListAsync();

        var warehouses = await db.Warehouses
            .Where(w => w.CompanyId == companyId && w.IsActive)
            .Where(w => warehouseId == null || w.Id == warehouseId)
            .ToListAsync();

        var rows = new List<StockBalanceRow>();
        foreach (var wh in warehouses)
        {
            foreach (var product in products)
            {
                var qty = await inv.GetOnHandAsync(companyId, product.Id, wh.Id);
                if (qty == 0 && !includeZeroBalances) continue;

                // ‏الموقوفُ يظهر لبضاعته لا لاسمه: صفرُ رصيدٍ يعني أنّه أُنجز، فيُخفى ولو
                // ‏طُلبت الأصفار — وإلا امتلأت ورقةُ الجرد بأصنافٍ ماتت منذ مواسم.
                if (!product.IsActive && qty == 0) continue;

                rows.Add(new StockBalanceRow(
                    product.Id, wh.Id,
                    product.Code, product.NameAr, wh.NameAr, product.Unit,
                    qty, product.WeightedAverageCost,
                    Math.Round(qty * product.WeightedAverageCost, 2, MidpointRounding.AwayFromZero),
                    IsStopped: !product.IsActive));
            }
        }
        return rows;
    }
}
