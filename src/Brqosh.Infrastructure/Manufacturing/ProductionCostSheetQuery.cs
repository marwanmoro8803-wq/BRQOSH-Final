using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Manufacturing;

/// <summary>سطرُ كشف تكلفة الإنتاج لأمرٍ مكتمل.</summary>
public sealed record ProductionCostRow(
    long WorkOrderId, string OrderNumber, string ProductName, DateOnly CompletedOn,
    decimal PlannedQuantity, decimal ProducedQuantity, decimal YieldPercent,
    decimal MaterialCost, decimal LabourCost, decimal OverheadCost, decimal TotalCost, decimal UnitCost);

/// <summary>سطرُ استهلاك خامةٍ في أمر: المخطَّطُ للكمّيّة المخطَّطة مقابل المصروف فعلاً.</summary>
public sealed record MaterialUsageRow(
    long ProductId, string ProductName, decimal PlannedQuantity, decimal IssuedQuantity,
    decimal QuantityVariance, decimal IssuedCost);

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٢ · ‏`manufacturing#9`) <b>كشفُ تكلفة الإنتاج واستهلاكُ الموادّ — من الدفتر لا من حسابٍ موازٍ.</b>
///
/// <para>‏<b>التكلفةُ من قيد الإكمال المرحَّل</b> <c>WorkOrderComplete:{id}</c>: الموادُّ دائنُ حساب المخزون، والأجرُ دائنُ
/// ‏<c>5106</c>، والتحميلُ دائنُ <c>5107</c>، والإجماليُّ مدينُ تحت التشغيل — فالكشفُ يقول ما رُحِّل بالهللة، ولا يفترق عنه
/// ‏إن تغيّرت طريقةُ الحساب يوماً. <b>والاستهلاكُ من حركة الصرف</b> <c>WorkOrder:{id}</c> مقابلَ وصفة الأمر المجمَّدة
/// ‏مقيسةً على الكمّيّة المخطَّطة.</para>
///
/// <para>‏<b>وحدُّه:</b> الصرفُ اليومَ استهلاكٌ قياسيٌّ عند الإكمال (<c>manufacturing#4</c>) — فالمصروفُ يتبع الإنتاجَ لا الفعليّ،
/// ‏وفرقُ الكمّيّة فرقُ إنتاجيّةٍ (المخطَّط مقابل المُنتَج) لا فرقُ هدرٍ مقيس.</para>
/// </summary>
public sealed class ProductionCostSheetQuery(BrqoshDbContext db)
{
    private const string LabourAccount = "5106";
    private const string OverheadAccount = "5107";

    public async Task<IReadOnlyList<ProductionCostRow>> ExecuteAsync(
        long companyId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var orders = await db.WorkOrders
            .Where(w => w.CompanyId == companyId && w.Status == WorkOrderStatus.Completed)
            .Select(w => new { w.Id, w.OrderNumber, w.BomId, w.PlannedQuantity, w.ProducedQuantity })
            .ToListAsync(ct);
        if (orders.Count == 0)
            return [];

        var keys = orders.Select(o => $"WorkOrderComplete:{o.Id}").ToList();
        // ‏`WhereReportable` كبقيّة الاستعلامات (`ReversedEntriesStayInBalancesTests`) — والأمرُ الملغى خارجٌ بحالته أعلاه،
        // ‏فقيدُ إكماله المعكوسُ لا يبلغ الكشف.
        var entries = await db.JournalEntries
            .WhereReportable(companyId, from, to)
            .Where(e => e.SourceDocument != null && keys.Contains(e.SourceDocument))
            .Select(e => new
            {
                e.SourceDocument,
                e.EntryDate,
                Lines = e.Lines.Select(l => new { l.AccountCode, l.Debit, l.Credit }).ToList(),
            })
            .ToListAsync(ct);

        var inventoryCode = SaudiChartOfAccounts.SystemCodes.Inventory;
        var wipCode = SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess;

        var bomIds = orders.Select(o => o.BomId).Distinct().ToList();
        var products = await db.BillsOfMaterials
            .Where(b => bomIds.Contains(b.Id))
            .Join(db.Products.IgnoreQueryFilters(), b => b.FinishedProductId, p => p.Id, (b, p) => new { b.Id, p.NameAr })
            .ToDictionaryAsync(x => x.Id, x => x.NameAr, ct);

        var rows = new List<ProductionCostRow>();
        foreach (var entry in entries)
        {
            var order = orders.Single(o => $"WorkOrderComplete:{o.Id}" == entry.SourceDocument);
            var material = entry.Lines.Where(l => l.AccountCode == inventoryCode).Sum(l => l.Credit - l.Debit);
            var labour = entry.Lines.Where(l => l.AccountCode == LabourAccount).Sum(l => l.Credit - l.Debit);
            var overhead = entry.Lines.Where(l => l.AccountCode == OverheadAccount).Sum(l => l.Credit - l.Debit);
            var total = entry.Lines.Where(l => l.AccountCode == wipCode).Sum(l => l.Debit - l.Credit);

            rows.Add(new ProductionCostRow(
                order.Id, order.OrderNumber ?? order.Id.ToString(), products.GetValueOrDefault(order.BomId, ""),
                entry.EntryDate, order.PlannedQuantity, order.ProducedQuantity,
                order.PlannedQuantity == 0m ? 0m : Math.Round(order.ProducedQuantity / order.PlannedQuantity * 100m, 2, MidpointRounding.AwayFromZero),
                material, labour, overhead, total,
                order.ProducedQuantity == 0m ? 0m : Math.Round(total / order.ProducedQuantity, 4, MidpointRounding.AwayFromZero)));
        }

        return rows.OrderBy(r => r.CompletedOn).ThenBy(r => r.OrderNumber, StringComparer.Ordinal).ToList();
    }

    public async Task<IReadOnlyList<MaterialUsageRow>> MaterialUsageAsync(
        long companyId, long workOrderId, CancellationToken ct = default)
    {
        var order = await db.WorkOrders.SingleAsync(w => w.CompanyId == companyId && w.Id == workOrderId, ct);
        var bom = await db.BillsOfMaterials.SingleAsync(b => b.Id == order.BomId, ct);
        var scale = bom.OutputQuantity == 0m ? 0m : order.PlannedQuantity / bom.OutputQuantity;

        var source = $"WorkOrder:{order.Id}";
        var issued = await db.StockMovements
            .Where(m => m.CompanyId == companyId && m.SourceDocument == source
                        && m.Type == StockMovementType.Issue && m.Status == StockMovementStatus.Posted)
            .SelectMany(m => m.Lines)
            .Select(l => new { l.ProductId, l.Quantity, l.UnitCost })
            .ToListAsync(ct);

        var planned = order.Materials
            .GroupBy(m => m.ComponentProductId)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(m => m.Quantity) * scale, 4, MidpointRounding.AwayFromZero));

        var productIds = planned.Keys.Union(issued.Select(i => i.ProductId)).ToList();
        var names = await db.Products.IgnoreQueryFilters()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.NameAr, ct);

        return productIds
            .Select(id =>
            {
                var lines = issued.Where(i => i.ProductId == id).ToList();
                var issuedQty = lines.Sum(i => i.Quantity);
                var issuedCost = lines.Sum(i => Math.Round(i.Quantity * i.UnitCost, 2, MidpointRounding.AwayFromZero));
                var plannedQty = planned.GetValueOrDefault(id);
                return new MaterialUsageRow(id, names.GetValueOrDefault(id, ""), plannedQty, issuedQty,
                    issuedQty - plannedQty, issuedCost);
            })
            .OrderBy(r => r.ProductName, StringComparer.Ordinal)
            .ToList();
    }
}
