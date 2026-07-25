using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
namespace Brqosh.Domain.Inventory;

/// <summary>رصيد مخزون مجمّع لكل منتج/مستودع — يُحدَّث عند كل حركة مرحّلة.</summary>
public class InventoryBalance : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long WarehouseId { get; private set; }
    public long ProductId { get; private set; }
    public decimal QuantityOnHand { get; private set; }

    private InventoryBalance() { }

    public static InventoryBalance Create(long companyId, long warehouseId, long productId, decimal qty) =>
        new()
        {
            CompanyId = companyId,
            WarehouseId = warehouseId,
            ProductId = productId,
            QuantityOnHand = qty,
        };

    public void ApplyDelta(decimal delta) => QuantityOnHand += delta;

    public void SetQuantity(decimal qty) => QuantityOnHand = qty;
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
