namespace Brqosh.Application.Accounting;

/// <summary>أنواع المستندات التي تمر عبر محرك قواعد الترحيل.</summary>
public enum PostingDocumentKind
{
    JournalEntry = 0,
    SalesInvoice = 1,
    PurchaseInvoice = 2,
    CreditNote = 3,
    DebitNote = 4,
    ReceiptVoucher = 5,
    PaymentVoucher = 6,
    StockMovement = 7,
    FixedAsset = 8,
    FixedAssetDisposal = 9,
    GoodsReceipt = 10,
    Payroll = 11,
    Cheque = 12,
    BankStatementDifference = 13,
    ProgressBilling = 14,
    Other = 99,
}

/// <summary>طلب تحقق قبل الترحيل — Financial Integrity Layer.</summary>
public sealed record PostingValidationRequest(
    long CompanyId,
    DateOnly DocumentDate,
    PostingDocumentKind DocumentKind,
    IReadOnlyList<long>? AccountIds = null,
    string? DocumentKey = null);

/// <summary>سياسة ترحيل قابلة للاستبدال (دولة/قطاع).</summary>
public interface IPostingPolicy
{
    Task ValidateBeforePostAsync(PostingValidationRequest request, CancellationToken ct = default);

    /// <summary>حل رمز حساب افتراضي للشركة (يجب أن يكون موجوداً وقابلاً للترحيل).</summary>
    Task<long> ResolveAccountIdAsync(long companyId, string systemCode, CancellationToken ct = default);
}
