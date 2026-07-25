using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Inventory;

/// <summary>طبقة تكلفة FIFO — كمية متبقية بتكلفة وحدة ثابتة من استلام محدد.</summary>
public class InventoryCostLayer : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long ProductId { get; private set; }
    public long WarehouseId { get; private set; }
    public DateOnly ReceivedDate { get; private set; }
    public decimal OriginalQuantity { get; private set; }
    public decimal RemainingQuantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public string SourceDocument { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private InventoryCostLayer()
    {
        SourceDocument = null!;
    }

    private InventoryCostLayer(long companyId, long productId, long warehouseId, DateOnly receivedDate,
        decimal quantity, decimal unitCost, string sourceDocument)
    {
        CompanyId = companyId;
        ProductId = productId;
        WarehouseId = warehouseId;
        ReceivedDate = receivedDate;
        OriginalQuantity = quantity;
        RemainingQuantity = quantity;
        UnitCost = unitCost;
        SourceDocument = sourceDocument;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static InventoryCostLayer Create(long companyId, long productId, long warehouseId,
        DateOnly receivedDate, decimal quantity, decimal unitCost, string sourceDocument)
    {
        if (quantity <= 0)
            throw new AccountingException("كمية طبقة التكلفة يجب أن تكون موجبة.");
        if (unitCost < 0)
            throw new AccountingException("تكلفة الوحدة لا تكون سالبة.");

        return new InventoryCostLayer(companyId, productId, warehouseId, receivedDate,
            Math.Round(quantity, 4, MidpointRounding.AwayFromZero),
            Math.Round(unitCost, 4, MidpointRounding.AwayFromZero),
            (sourceDocument ?? "").Trim());
    }

    public decimal Consume(decimal quantity)
    {
        if (quantity <= 0)
            throw new AccountingException("كمية الاستهلاك يجب أن تكون موجبة.");
        if (quantity > RemainingQuantity)
            throw new AccountingException(
                $"طبقة FIFO غير كافية — المتبقي {RemainingQuantity:N4}، المطلوب {quantity:N4}.");

        RemainingQuantity = Math.Round(RemainingQuantity - quantity, 4, MidpointRounding.AwayFromZero);
        return Math.Round(quantity * UnitCost, 2, MidpointRounding.AwayFromZero);
    }

    public void Restore(decimal quantity)
    {
        if (quantity <= 0)
            throw new AccountingException("كمية الإرجاع يجب أن تكون موجبة.");
        RemainingQuantity = Math.Round(RemainingQuantity + quantity, 4, MidpointRounding.AwayFromZero);
        if (RemainingQuantity > OriginalQuantity)
            RemainingQuantity = OriginalQuantity;
    }

    /// <summary>توزيع تكلفة إضافية (شحن/رسوم) على الطبقة مع الحفاظ على الكمية.</summary>
    public void AbsorbLandedCost(decimal additionalTotalCost)
    {
        if (additionalTotalCost < 0)
            throw new AccountingException("التكلفة الإضافية لا تكون سالبة.");
        if (RemainingQuantity <= 0)
            throw new AccountingException("لا يمكن توزيع تكلفة على طبقة بلا كمية متبقية.");

        var addedPerUnit = Math.Round(additionalTotalCost / RemainingQuantity, 4, MidpointRounding.AwayFromZero);
        UnitCost = Math.Round(UnitCost + addedPerUnit, 4, MidpointRounding.AwayFromZero);
    }
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
