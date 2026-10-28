using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Treasury;

public enum PaymentAllocationDocumentType
{
    SalesInvoice = 0,
    PurchaseInvoice = 1,
    ProgressBilling = 2,
}

/// <summary>سطر تخصيص دفعة على فاتورة/مستخلص — يربط مبلغاً من سند بذمة مستند.</summary>
public class PaymentAllocationLine : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long? ReceiptVoucherId { get; private set; }
    public long? PaymentVoucherId { get; private set; }
    public PaymentAllocationDocumentType DocumentType { get; private set; }
    public long DocumentId { get; private set; }
    public string DocumentNumber { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly AllocationDate { get; private set; }

    /// <summary>عُكس التخصيص (بعكس سنده) — يبقى السطر للتاريخ ويُستبعد من كل الحسابات. لا حذف فعلي.</summary>
    public bool IsReversed { get; private set; }
    public DateTime? ReversedAt { get; private set; }

    private PaymentAllocationLine()
    {
        DocumentNumber = null!;
    }

    private PaymentAllocationLine(long companyId, PaymentAllocationDocumentType documentType,
        long documentId, string documentNumber, decimal amount, DateOnly allocationDate,
        long? receiptVoucherId, long? paymentVoucherId)
    {
        CompanyId = companyId;
        DocumentType = documentType;
        DocumentId = documentId;
        DocumentNumber = documentNumber;
        Amount = amount;
        AllocationDate = allocationDate;
        ReceiptVoucherId = receiptVoucherId;
        PaymentVoucherId = paymentVoucherId;
    }

    public static PaymentAllocationLine ForReceipt(
        long companyId, long receiptVoucherId, PaymentAllocationDocumentType documentType,
        long documentId, string documentNumber, decimal amount, DateOnly date)
    {
        Validate(documentId, documentNumber, amount);
        return new PaymentAllocationLine(companyId, documentType, documentId, documentNumber.Trim(),
            Math.Round(amount, 2, MidpointRounding.AwayFromZero), date, receiptVoucherId, null);
    }

    public static PaymentAllocationLine ForPayment(
        long companyId, long paymentVoucherId, PaymentAllocationDocumentType documentType,
        long documentId, string documentNumber, decimal amount, DateOnly date)
    {
        Validate(documentId, documentNumber, amount);
        return new PaymentAllocationLine(companyId, documentType, documentId, documentNumber.Trim(),
            Math.Round(amount, 2, MidpointRounding.AwayFromZero), date, null, paymentVoucherId);
    }

    private static void Validate(long documentId, string documentNumber, decimal amount)
    {
        if (documentId <= 0)
            throw new AccountingException("المستند المخصص عليه مطلوب.");
        if (string.IsNullOrWhiteSpace(documentNumber))
            throw new AccountingException("رقم المستند مطلوب للتخصيص.");
        if (amount <= 0)
            throw new AccountingException("مبلغ التخصيص يجب أن يكون أكبر من صفر.");
    }
    public void MarkReversed(DateTime reversedAtUtc)
    {
        if (IsReversed) return;
        IsReversed = true;
        ReversedAt = reversedAtUtc;
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
