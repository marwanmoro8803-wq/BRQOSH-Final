using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;

namespace Brqosh.Application.Manufacturing;

/// <summary>‏عقودُ التصنيع في Application — المنطقُ في Infrastructure، والشاشاتُ تطلب العقدَ لا النوعَ الملموس.</summary>
public interface IWorkOrderService
{
    Task<WorkOrder> ReleaseAsync(long companyId, long workOrderId, CancellationToken ct = default);

    Task<WorkOrder> CompleteAsync(long companyId, long workOrderId, decimal producedQuantity,
        string username, DateOnly? completionDate = null, string? outputBatchNumber = null,
        DateOnly? outputExpiry = null, CancellationToken ct = default, decimal scrapQuantity = 0m,
        bool closeOrder = true, IReadOnlyList<(long ProductId, decimal Quantity, decimal UnitValue)>? byProducts = null,
        IReadOnlyList<string>? outputSerials = null);

    Task<decimal> IssueMaterialsAsync(long companyId, long workOrderId,
        IReadOnlyList<(long ProductId, decimal Quantity)> issues, string username,
        DateOnly? issueDate = null, CancellationToken ct = default);

    Task<InventoryTracking> OutputTrackingAsync(long companyId, long workOrderId, CancellationToken ct = default);

    Task<bool> OutputIsTrackedAsync(long companyId, long workOrderId, CancellationToken ct = default);

    Task CancelAsync(long companyId, long workOrderId, string username,
        DateOnly? cancellationDate = null, CancellationToken ct = default);
}

public interface IBillOfMaterialsService
{
    Task<BillOfMaterials> UpdateAsync(long companyId, long bomId, string nameAr,
        IReadOnlyList<(long ComponentProductId, decimal Quantity)> lines,
        decimal labourCostPerUnit, decimal overheadRatePercent, string username, CancellationToken ct = default);
}

public record WipRowDto(long WorkOrderId, string? OrderNumber, string BomName, string ProductName,
    decimal PlannedQuantity, decimal ProducedQuantity, string StatusAr, WorkOrderStatus Status);

public interface IWipQuery
{
    Task<IReadOnlyList<WipRowDto>> ListOpenAsync(long companyId, CancellationToken ct = default);
}

public record ProductionCostRowDto(
    long WorkOrderId, string OrderNumber, string ProductName, DateOnly CompletedOn,
    decimal PlannedQuantity, decimal ProducedQuantity, decimal YieldPercent,
    decimal MaterialCost, decimal LabourCost, decimal OverheadCost, decimal TotalCost, decimal UnitCost);

public record MaterialUsageRowDto(
    long ProductId, string ProductName, decimal PlannedQuantity, decimal IssuedQuantity,
    decimal QuantityVariance, decimal IssuedCost);

public interface IProductionCostSheetQuery
{
    Task<IReadOnlyList<ProductionCostRowDto>> ExecuteAsync(long companyId, DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<IReadOnlyList<MaterialUsageRowDto>> MaterialUsageAsync(long companyId, long workOrderId, CancellationToken ct = default);
}
