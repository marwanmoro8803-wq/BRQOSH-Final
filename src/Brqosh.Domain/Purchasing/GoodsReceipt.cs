using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Purchasing;

public enum GoodsReceiptStatus
{
    Draft = 0,
    Posted = 1,
    Cancelled = 2,
}

/// <summary>سطر استلام بضاعة مربوط بأمر شراء و/أو صنف.</summary>
public class GoodsReceiptLine
{
    public long Id { get; private set; }
    public long? ProductId { get; private set; }
    public string Description { get; private set; }
    public decimal OrderedQuantity { get; private set; }
    public decimal ReceivedQuantity { get; private set; }
    public decimal UnitCost { get; private set; }

    private GoodsReceiptLine() { Description = null!; }

    public GoodsReceiptLine(string description, decimal orderedQuantity, decimal receivedQuantity,
        decimal unitCost, long? productId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف سطر الاستلام مطلوب.");
        if (receivedQuantity <= 0)
            throw new AccountingException("كمية الاستلام يجب أن تكون موجبة.");
        if (unitCost < 0)
            throw new AccountingException("تكلفة الوحدة لا تكون سالبة.");

        Description = description.Trim();
        OrderedQuantity = Math.Round(orderedQuantity, 4, MidpointRounding.AwayFromZero);
        ReceivedQuantity = Math.Round(receivedQuantity, 4, MidpointRounding.AwayFromZero);
        UnitCost = Math.Round(unitCost, 4, MidpointRounding.AwayFromZero);
        ProductId = productId;
    }
}

/// <summary>
/// إذن استلام بضاعة (GRN) — يحرّك المخزون مستقلاً عن فاتورة المشتريات.
/// أساس المطابقة الثلاثية: أمر شراء ↔ استلام ↔ فاتورة.
/// </summary>
public class GoodsReceipt : IBranchScoped
{
    private readonly List<GoodsReceiptLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long SupplierId { get; private set; }
    public long PurchaseOrderId { get; private set; }
    public long WarehouseId { get; private set; }
    public string? ReceiptNumber { get; private set; }
    public DateOnly ReceiptDate { get; private set; }
    public string Notes { get; private set; }
    public GoodsReceiptStatus Status { get; private set; } = GoodsReceiptStatus.Draft;
    public long? StockMovementId { get; private set; }

    /// <summary>
    /// (CEEF-229 · م1-١٩) قيدُ الاستحقاق — <b>ومرساةُ عكسه عند الإلغاء</b>.
    ///
    /// <para>ولا يُبحث عنه بـ<c>SourceDocument</c>: عكسُ القيد ينسخ المصدرَ نفسَه، وخدمةُ
    /// الإلغاء تستعمل السلسلةَ عينَها لحركتها المخزنية — فيصير المفتاحُ ملتبساً ببنائه.</para>
    /// </summary>
    public long? JournalEntryId { get; private set; }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public IReadOnlyList<GoodsReceiptLine> Lines => _lines.AsReadOnly();

    private GoodsReceipt() { Notes = null!; }

    private GoodsReceipt(long companyId, long supplierId, long purchaseOrderId, long warehouseId,
        DateOnly receiptDate, string notes)
    {
        CompanyId = companyId;
        SupplierId = supplierId;
        PurchaseOrderId = purchaseOrderId;
        WarehouseId = warehouseId;
        ReceiptDate = receiptDate;
        Notes = notes;
    }

    public static GoodsReceipt CreateDraft(long companyId, long supplierId, long purchaseOrderId,
        long warehouseId, DateOnly receiptDate, string notes = "")
    {
        if (supplierId <= 0) throw new AccountingException("المورد مطلوب لإذن الاستلام.");
        if (purchaseOrderId <= 0) throw new AccountingException("أمر الشراء مطلوب لإذن الاستلام.");
        if (warehouseId <= 0) throw new AccountingException("المستودع مطلوب لإذن الاستلام.");
        return new GoodsReceipt(companyId, supplierId, purchaseOrderId, warehouseId, receiptDate, (notes ?? "").Trim());
    }

    public void AddLine(string description, decimal orderedQuantity, decimal receivedQuantity,
        decimal unitCost, long? productId = null)
    {
        EnsureDraft();
        _lines.Add(new GoodsReceiptLine(description, orderedQuantity, receivedQuantity, unitCost, productId));
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        EnsureDraft();
        BranchId = branchId;
    }

    public void MarkPosted(string receiptNumber, long? stockMovementId)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new AccountingException("إذن الاستلام بلا سطور.");
        if (string.IsNullOrWhiteSpace(receiptNumber))
            throw new AccountingException("رقم إذن الاستلام مطلوب.");
        ReceiptNumber = receiptNumber.Trim();
        StockMovementId = stockMovementId;
        Status = GoodsReceiptStatus.Posted;
    }

    public void MarkCancelled()
    {
        if (Status != GoodsReceiptStatus.Posted)
            throw new AccountingException("لا يُلغى إلا إذن استلام مرحّل.");
        Status = GoodsReceiptStatus.Cancelled;
    }

    private void EnsureDraft()
    {
        if (Status != GoodsReceiptStatus.Draft)
            throw new AccountingException("إذن الاستلام غير قابل للتعديل.");
    }
}
