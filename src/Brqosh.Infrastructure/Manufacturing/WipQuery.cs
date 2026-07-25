using Brqosh.Domain.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Manufacturing;

public class WipQuery(BrqoshDbContext db)
{
    public record WipRow(long WorkOrderId, string? OrderNumber, string BomName, string ProductName,
        decimal PlannedQuantity, decimal ProducedQuantity, string StatusAr, WorkOrderStatus Status);

    public async Task<IReadOnlyList<WipRow>> ListOpenAsync(long companyId, CancellationToken ct = default)
    {
        var orders = await db.WorkOrders
            .Where(w => w.CompanyId == companyId && w.Status == WorkOrderStatus.Released)
            .OrderByDescending(w => w.OrderDate)
            .ToListAsync(ct);

        if (orders.Count == 0) return [];

        var bomIds = orders.Select(o => o.BomId).Distinct().ToList();
        var boms = await db.BillsOfMaterials.Where(b => bomIds.Contains(b.Id)).ToListAsync(ct);
        var productIds = boms.Select(b => b.FinishedProductId).Distinct().ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.NameAr, ct);
        var bomMap = boms.ToDictionary(b => b.Id);

        return orders.Select(o =>
        {
            var bom = bomMap[o.BomId];
            return new WipRow(o.Id, o.OrderNumber, bom.NameAr,
                products.GetValueOrDefault(bom.FinishedProductId, "—"),
                o.PlannedQuantity, o.ProducedQuantity,
                WorkOrder.StatusLabel(o.Status), o.Status);
        }).ToList();
    }
}
