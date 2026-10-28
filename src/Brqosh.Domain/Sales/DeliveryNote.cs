using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Sales;

public enum DeliveryNoteStatus
{
    Posted = 1,
    Cancelled = 2,
}

/// <summary>
/// ‏إذنُ تسليمٍ من أمر بيع — يُخرج البضاعةَ من المستودع ويُقيِّد تكلفتَها عند التسليم،
/// ‏فالفاتورةُ اللاحقةُ بالكمّية المسلَّمة لا تصرف المخزونَ مرّةً ثانية (<see cref="SalesInvoice.StockIssuedByDelivery"/>).
/// </summary>
public class DeliveryNote
{
    private readonly List<DeliveryNoteLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long SalesOrderId { get; private set; }
    public long CustomerId { get; private set; }
    public long WarehouseId { get; private set; }
    public DateOnly DeliveryDate { get; private set; }
    public string Number { get; private set; } = "";
    public DeliveryNoteStatus Status { get; private set; } = DeliveryNoteStatus.Posted;
    public long? StockMovementId { get; private set; }
    public long? JournalEntryId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public IReadOnlyList<DeliveryNoteLine> Lines => _lines.AsReadOnly();

    private DeliveryNote() { }

    public static DeliveryNote Create(long companyId, long salesOrderId, long customerId, long warehouseId,
        DateOnly deliveryDate, string number)
    {
        if (salesOrderId <= 0 || customerId <= 0 || warehouseId <= 0)
            throw new AccountingException("إذن التسليم يتطلب أمر بيع وعميلاً ومستودعاً.");
        if (string.IsNullOrWhiteSpace(number))
            throw new AccountingException("رقم إذن التسليم مطلوب.");
        return new DeliveryNote
        {
            CompanyId = companyId, SalesOrderId = salesOrderId, CustomerId = customerId, WarehouseId = warehouseId,
            DeliveryDate = deliveryDate, Number = number.Trim(), CreatedAtUtc = DateTime.UtcNow,
        };
    }

    public void AddLine(int orderLineIndex, long productId, decimal quantity, decimal unitCost)
    {
        if (orderLineIndex < 0 || productId <= 0)
            throw new AccountingException("سطر إذن التسليم غير صالح.");
        if (quantity <= 0m)
            throw new AccountingException("الكمية يجب أن تكون موجبة.");
        _lines.Add(new DeliveryNoteLine(orderLineIndex, productId, quantity, unitCost));
    }

    public void LinkStockMovement(long movementId) => StockMovementId = movementId;
    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void Cancel()
    {
        if (Status == DeliveryNoteStatus.Cancelled)
            throw new AccountingException("إذن التسليم ملغى مسبقاً.");
        Status = DeliveryNoteStatus.Cancelled;
    }
}

public class DeliveryNoteLine
{
    public long Id { get; private set; }
    public long DeliveryNoteId { get; private set; }
    public int OrderLineIndex { get; private set; }
    public long ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }

    private DeliveryNoteLine() { }

    internal DeliveryNoteLine(int orderLineIndex, long productId, decimal quantity, decimal unitCost)
    {
        OrderLineIndex = orderLineIndex;
        ProductId = productId;
        Quantity = quantity;
        UnitCost = unitCost;
    }
}
