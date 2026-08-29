using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Sales;

namespace Brqosh.Domain.Purchasing;

public enum PurchaseOrderStatus
{
    Draft = 0,
    Approved = 1,
    Cancelled = 2,
    Fulfilled = 3,
    Submitted = 4,
    Rejected = 5,
}

/// <summary>
/// أمر شراء — لا يُرحَّل محاسبياً؛ الترحيل عند فاتورة المشتريات المرتبطة.
/// </summary>
public class PurchaseOrder : IBranchScoped, ILineDocumentTotals
{
    private readonly List<SalesInvoiceLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long SupplierId { get; private set; }
    public string? OrderNumber { get; private set; }
    public DateOnly OrderDate { get; private set; }
    public DateOnly? ExpectedDeliveryDate { get; private set; }
    public string Notes { get; private set; }
    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Draft;
    public long? WorkflowInstanceId { get; private set; }

    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();
    public decimal Subtotal { get; private set; }
    public decimal VatAmount { get; private set; }

    private PurchaseOrder()
    {
        Notes = null!;
    }

    private PurchaseOrder(long companyId, long supplierId, DateOnly orderDate, string notes, DateOnly? expectedDelivery)
    {
        CompanyId = companyId;
        SupplierId = supplierId;
        OrderDate = orderDate;
        Notes = notes;
        ExpectedDeliveryDate = expectedDelivery;
    }

    public static PurchaseOrder CreateDraft(long companyId, long supplierId, DateOnly orderDate,
        string notes = "", DateOnly? expectedDelivery = null)
    {
        if (supplierId <= 0)
            throw new AccountingException("المورد مطلوب لأمر الشراء.");

        return new PurchaseOrder(companyId, supplierId, orderDate, (notes ?? "").Trim(), expectedDelivery);
    }

    public void AddLine(string description, decimal quantity, decimal unitPrice, long? productId = null,
        Tax.VatCategory vatCategory = Tax.VatCategory.Standard)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف الصنف مطلوب.");
        if (quantity <= 0 || unitPrice < 0)
            throw new AccountingException("الكمية يجب أن تكون موجبة والسعر لا يكون سالباً.");
        DocumentLineScale.EnsureUnitPriceHalala(unitPrice);
        _lines.Add(new SalesInvoiceLine(description.Trim(), quantity, unitPrice, productId, vatCategory));
        RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        (Subtotal, VatAmount) = LineDocumentTotalsCalculator.Compute(_lines);
    }

    public void Approve(string orderNumber)
    {
        EnsureDraft();
        FinalizeApproval(orderNumber);
    }

    public void MarkSubmitted(long workflowInstanceId)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new AccountingException("أمر الشراء بلا سطور.");
        WorkflowInstanceId = workflowInstanceId;
        Status = PurchaseOrderStatus.Submitted;
    }

    public void MarkWorkflowApproved(string orderNumber)
    {
        if (Status != PurchaseOrderStatus.Submitted)
            throw new AccountingException("يُعتمد أمر مُرسَل فقط.");
        FinalizeApproval(orderNumber);
    }

    public void MarkRejected()
    {
        if (Status != PurchaseOrderStatus.Submitted)
            throw new AccountingException("يُرفض أمر مُرسَل فقط.");
        Status = PurchaseOrderStatus.Rejected;
    }

    private void FinalizeApproval(string orderNumber)
    {
        if (_lines.Count == 0)
            throw new AccountingException("أمر الشراء بلا سطور.");
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new AccountingException("رقم أمر الشراء مطلوب عند الاعتماد.");

        RecalculateTotals();
        OrderNumber = orderNumber.Trim();
        Status = PurchaseOrderStatus.Approved;
    }

    public void Cancel()
    {
        if (Status is PurchaseOrderStatus.Fulfilled or PurchaseOrderStatus.Cancelled)
            throw new AccountingException("لا يمكن إلغاء أمر الشراء في حالته الحالية.");
        Status = PurchaseOrderStatus.Cancelled;
    }

    public void MarkFulfilled()
    {
        if (Status != PurchaseOrderStatus.Approved)
            throw new AccountingException("يُستكمل أمر الشراء المعتمد فقط.");
        Status = PurchaseOrderStatus.Fulfilled;
    }

    private void EnsureDraft()
    {
        if (Status != PurchaseOrderStatus.Draft)
            throw new AccountingException("أمر الشراء غير قابل للتعديل — الحالة: " + Status);
    }
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        EnsureDraft();
        BranchId = branchId;
    }

}
