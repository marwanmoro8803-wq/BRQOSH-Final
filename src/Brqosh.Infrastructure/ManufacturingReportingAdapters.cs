using Brqosh.Application.Manufacturing;
using Brqosh.Application.Reporting;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Infrastructure;

/// <summary>‏محوّلاتُ عقود التقارير والتصنيع إلى الاستعلامات الملموسة — تنسخ الصفوفَ إلى DTO بلا منطق.</summary>
internal sealed class WipQueryAdapter(BrqoshDbContext db) : IWipQuery
{
    public async Task<IReadOnlyList<WipRowDto>> ListOpenAsync(long companyId, CancellationToken ct = default) =>
        (await new WipQuery(db).ListOpenAsync(companyId, ct))
            .Select(r => new WipRowDto(r.WorkOrderId, r.OrderNumber, r.BomName, r.ProductName,
                r.PlannedQuantity, r.ProducedQuantity, r.StatusAr, r.Status)).ToList();
}

internal sealed class ProductionCostSheetQueryAdapter(BrqoshDbContext db) : IProductionCostSheetQuery
{
    public async Task<IReadOnlyList<ProductionCostRowDto>> ExecuteAsync(long companyId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        (await new ProductionCostSheetQuery(db).ExecuteAsync(companyId, from, to, ct))
            .Select(r => new ProductionCostRowDto(r.WorkOrderId, r.OrderNumber, r.ProductName, r.CompletedOn,
                r.PlannedQuantity, r.ProducedQuantity, r.YieldPercent, r.MaterialCost, r.LabourCost,
                r.OverheadCost, r.TotalCost, r.UnitCost)).ToList();

    public async Task<IReadOnlyList<MaterialUsageRowDto>> MaterialUsageAsync(long companyId, long workOrderId, CancellationToken ct = default) =>
        (await new ProductionCostSheetQuery(db).MaterialUsageAsync(companyId, workOrderId, ct))
            .Select(r => new MaterialUsageRowDto(r.ProductId, r.ProductName, r.PlannedQuantity, r.IssuedQuantity,
                r.QuantityVariance, r.IssuedCost)).ToList();
}

internal sealed class ReceivableAgingQueryAdapter(BrqoshDbContext db) : IReceivableAgingQuery
{
    public async Task<IReadOnlyList<ReceivableAgingRowDto>> ExecuteAsync(long companyId, DateOnly asOf, CancellationToken ct = default) =>
        (await new ReceivableAgingQuery(db).ExecuteAsync(companyId, asOf, ct))
            .Select(r => new ReceivableAgingRowDto(r.CustomerId, r.CustomerNameAr, r.Current, r.Days31To60,
                r.Days61To90, r.Over90, r.UnappliedCredit)).ToList();
}

internal sealed class PayableAgingQueryAdapter(BrqoshDbContext db) : IPayableAgingQuery
{
    public async Task<IReadOnlyList<PayableAgingRowDto>> ExecuteAsync(long companyId, DateOnly asOf, CancellationToken ct = default) =>
        (await new PayableAgingQuery(db).ExecuteAsync(companyId, asOf, ct))
            .Select(r => new PayableAgingRowDto(r.SupplierId, r.SupplierNameAr, r.Current, r.Days31To60,
                r.Days61To90, r.Over90, r.UnappliedDebit)).ToList();
}
