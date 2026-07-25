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
    decimal TotalValue);

/// <summary>أرصدة المخزون الحالية — محسوبة من الحركات المرحّلة.</summary>
public class StockBalanceQuery(BrqoshDbContext db)
{
    public async Task<IReadOnlyList<StockBalanceRow>> ExecuteAsync(long companyId, long? warehouseId = null,
        bool includeZeroBalances = false)
    {
        var inv = new InventoryService(db);
        var products = await db.Products
            .Where(p => p.CompanyId == companyId && p.IsActive && p.Type == ProductType.Stockable)
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
                rows.Add(new StockBalanceRow(
                    product.Id, wh.Id,
                    product.Code, product.NameAr, wh.NameAr, product.Unit,
                    qty, product.WeightedAverageCost,
                    Math.Round(qty * product.WeightedAverageCost, 2, MidpointRounding.AwayFromZero)));
            }
        }
        return rows;
    }
}
