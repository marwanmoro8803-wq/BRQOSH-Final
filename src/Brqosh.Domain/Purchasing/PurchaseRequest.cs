using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Sales;

namespace Brqosh.Domain.Purchasing;

public enum PurchaseRequestStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    ConvertedToPo = 4,
}

/// <summary>طلب شراء — يمر بسلسلة موافقات قبل تحويله لأمر شراء.</summary>
public class PurchaseRequest : IBranchScoped
{
    private readonly List<SalesInvoiceLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long SupplierId { get; private set; }
    public string? RequestNumber { get; private set; }
    public DateOnly RequestDate { get; private set; }
    public string Justification { get; private set; }
    public PurchaseRequestStatus Status { get; private set; } = PurchaseRequestStatus.Draft;
    public long? PurchaseOrderId { get; private set; }
    public long? WorkflowInstanceId { get; private set; }

    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();
    public decimal Subtotal => _lines.Sum(l => l.LineTotal);

    private PurchaseRequest()
    {
        Justification = null!;
    }

    private PurchaseRequest(long companyId, long supplierId, DateOnly requestDate, string justification)
    {
        CompanyId = companyId;
        SupplierId = supplierId;
        RequestDate = requestDate;
        Justification = justification;
    }

    public static PurchaseRequest CreateDraft(long companyId, long supplierId, DateOnly requestDate, string justification = "")
    {
        if (supplierId <= 0)
            throw new AccountingException("المورد مطلوب لطلب الشراء.");
        return new PurchaseRequest(companyId, supplierId, requestDate, (justification ?? "").Trim());
    }

    /// <summary>
    /// (م-٥ · ن-٥٢) <b>والفئةُ الضريبيّةُ تُقبَل هنا — وكانت لا تُقبَل.</b> انظر شرحَ
    /// <c>SalesQuotation.AddLine</c>: العجزُ عن التعبير وعمودُ ن-٥١ العدديّ وجهان لسببٍ واحد،
    /// <b>والوسيطُ لا يُضاف بلا تمريرِه في <c>PurchaseRequestWorkflowService</c></b>.
    /// </summary>
    public void AddLine(string description, decimal quantity, decimal unitPrice, long? productId = null,
        Tax.VatCategory vatCategory = Tax.VatCategory.Standard,
        string? taxExemptionReasonCode = null, string? taxExemptionReason = null)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف الصنف مطلوب.");
        if (quantity <= 0 || unitPrice < 0)
            throw new AccountingException("الكمية يجب أن تكون موجبة والسعر لا يكون سالباً.");
        DocumentLineScale.EnsureUnitPriceHalala(unitPrice);
        _lines.Add(new SalesInvoiceLine(description.Trim(), quantity, unitPrice, productId,
            vatCategory, taxExemptionReasonCode, taxExemptionReason));
    }

    public void MarkSubmitted(string requestNumber, long workflowInstanceId)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new AccountingException("طلب الشراء بلا سطور.");
        if (string.IsNullOrWhiteSpace(requestNumber))
            throw new AccountingException("رقم الطلب مطلوب عند الإرسال.");
        RequestNumber = requestNumber.Trim();
        WorkflowInstanceId = workflowInstanceId;
        Status = PurchaseRequestStatus.Submitted;
    }

    public void MarkApproved()
    {
        if (Status != PurchaseRequestStatus.Submitted)
            throw new AccountingException("يُعتمد طلب مُرسَل فقط.");
        Status = PurchaseRequestStatus.Approved;
    }

    public void MarkRejected()
    {
        if (Status != PurchaseRequestStatus.Submitted)
            throw new AccountingException("يُرفض طلب مُرسَل فقط.");
        Status = PurchaseRequestStatus.Rejected;
    }

    public void MarkConvertedToPo(long purchaseOrderId)
    {
        if (Status != PurchaseRequestStatus.Approved)
            throw new AccountingException("يُحوَّل الطلب المعتمد فقط إلى أمر شراء.");
        PurchaseOrderId = purchaseOrderId;
        Status = PurchaseRequestStatus.ConvertedToPo;
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        EnsureDraft();
        BranchId = branchId;
    }

    private void EnsureDraft()
    {
        if (Status != PurchaseRequestStatus.Draft)
            throw new AccountingException("طلب الشراء غير قابل للتعديل — الحالة: " + Status);
    }
}
