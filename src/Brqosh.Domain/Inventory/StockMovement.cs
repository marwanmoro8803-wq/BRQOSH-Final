using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Inventory;

public enum StockMovementType
{
    /// <summary>رصيد افتتاحي — قيد: مخزون / أرباح مرحّلة.</summary>
    Opening = 1,

    /// <summary>إدخال — شراء أو إرجاع مبيعات.</summary>
    Receipt = 2,

    /// <summary>إخراج — بيع أو صرف يدوي.</summary>
    Issue = 3,

    /// <summary>تحويل بين مستودعين — بلا قيد محاسبي.</summary>
    Transfer = 4,

    /// <summary>تسوية جرد (زيادة) — مخزون / إيرادات أخرى.</summary>
    AdjustmentIn = 5,

    /// <summary>تسوية جرد (نقص) — مصروفات متنوعة / مخزون.</summary>
    AdjustmentOut = 6,

    /// <summary>إرجاع مبيعات — من إشعار دائن.</summary>
    SalesReturn = 7,
}

public enum StockMovementStatus
{
    Draft = 0,
    Posted = 1,
    Cancelled = 2,
}

/// <summary>حركة مخزنية — سجل غير قابل للتعديل بعد الترحيل (مثل القيود).</summary>
public class StockMovement : IBranchScoped
{
    private readonly List<StockMovementLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public string? MovementNumber { get; private set; }
    public DateOnly MovementDate { get; private set; }
    public StockMovementType Type { get; private set; }
    public StockMovementStatus Status { get; private set; } = StockMovementStatus.Draft;

    public long WarehouseId { get; private set; }

    /// <summary>مستودع الوجهة — للتحويل فقط.</summary>
    public long? ToWarehouseId { get; private set; }

    public string Description { get; private set; }
    public string? SourceDocument { get; private set; }
    public long? JournalEntryId { get; private set; }

    public IReadOnlyList<StockMovementLine> Lines => _lines.AsReadOnly();

    public decimal TotalValue => _lines.Sum(l => l.LineCost);

    // EF Core
    private StockMovement()
    {
        Description = null!;
    }

    private StockMovement(long companyId, DateOnly date, StockMovementType type,
        long warehouseId, long? toWarehouseId, string description, string? sourceDocument)
    {
        CompanyId = companyId;
        MovementDate = date;
        Type = type;
        WarehouseId = warehouseId;
        ToWarehouseId = toWarehouseId;
        Description = description;
        SourceDocument = sourceDocument;
    }

    public static StockMovement CreateDraft(long companyId, DateOnly date, StockMovementType type,
        long warehouseId, string description, long? toWarehouseId = null, string? sourceDocument = null)
    {
        if (warehouseId <= 0)
            throw new AccountingException("المستودع مطلوب.");
        if (type == StockMovementType.Transfer && (toWarehouseId is null or <= 0))
            throw new AccountingException("مستودع الوجهة مطلوب للتحويل.");
        if (type == StockMovementType.Transfer && toWarehouseId == warehouseId)
            throw new AccountingException("لا يمكن التحويل لنفس المستودع.");
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("بيان الحركة مطلوب.");

        return new StockMovement(companyId, date, type, warehouseId, toWarehouseId,
            description.Trim(), sourceDocument);
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        EnsureDraft();
        BranchId = branchId;
    }

    /// <summary>
    /// يُضيف سطراً — و<paramref name="batchNumber"/> هويّةُ الدفعة أو الرقمُ التسلسليّ.
    ///
    /// <para><b>ولا يُفرض هنا وجودُها</b>: النطاقُ يعرف معرّفَ الصنف ولا يعرف نمطَ تتبّعه،
    /// فالفرضُ من طبقةٍ تحمل الصنفَ نفسَه — وهو <c>InventoryService</c>. <b>وحارسٌ يُبنى
    /// حيث لا تكتمل معرفتُه يمرّ صامتاً.</b></para>
    /// </summary>
    public void AddLine(long productId, decimal quantity, decimal unitCost,
        string? batchNumber = null, DateOnly? expiryDate = null)
    {
        EnsureDraft();
        if (productId <= 0)
            throw new AccountingException("الصنف مطلوب في سطر الحركة.");
        if (quantity <= 0)
            throw new AccountingException("الكمية يجب أن تكون موجبة.");
        if (unitCost < 0)
            throw new AccountingException("تكلفة الوحدة لا تكون سالبة.");

        var batch = string.IsNullOrWhiteSpace(batchNumber) ? null : batchNumber.Trim();
        _lines.Add(new StockMovementLine(productId, quantity, unitCost, batch, expiryDate));
    }

    public void MarkPosted(string movementNumber)
    {
        if (Status != StockMovementStatus.Draft)
            throw new AccountingException("الحركة مرحّلة مسبقاً.");
        if (_lines.Count == 0)
            throw new AccountingException("الحركة بلا سطور.");
        if (string.IsNullOrWhiteSpace(movementNumber))
            throw new AccountingException("رقم الحركة مطلوب.");
        MovementNumber = movementNumber.Trim();
        Status = StockMovementStatus.Posted;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void MarkCancelled()
    {
        if (Status != StockMovementStatus.Posted)
            throw new AccountingException("لا تُلغى إلا حركة مخزون مرحّلة.");
        Status = StockMovementStatus.Cancelled;
    }

    public bool IsInbound => Type is StockMovementType.Opening or StockMovementType.Receipt
        or StockMovementType.AdjustmentIn or StockMovementType.SalesReturn;

    public bool IsOutbound => Type is StockMovementType.Issue or StockMovementType.AdjustmentOut;

    private void EnsureDraft()
    {
        if (Status != StockMovementStatus.Draft)
            throw new AccountingException("الحركة مرحّلة — لا تُعدَّل.");
    }
}

/// <summary>
/// سطرُ حركةٍ مخزنية.
///
/// <para>(CEEF-230 · ط1-٣) <b>و<c>BatchNumber</c> يبقى على السطر بعد الترحيل عمداً</b>:
/// هو أثرُ ما دخل أو خرج بعينه. وبدونه لا يُعرف عند الإلغاء <b>أيَّ دفعةٍ تُعاد</b> —
/// فيُردّ المخزونُ كمّيةً بلا هويّة وتنكسر <c>هـ-٩</c>.</para>
/// </summary>
public record StockMovementLine(
    long ProductId, decimal Quantity, decimal UnitCost,
    string? BatchNumber = null, DateOnly? ExpiryDate = null)
{
    public decimal LineCost => Math.Round(Quantity * UnitCost, 2, MidpointRounding.AwayFromZero);
}
