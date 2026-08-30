using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Sales;

namespace Brqosh.Domain.Purchasing;

public enum PurchaseInvoiceStatus
{
    Draft = 0,
    Posted = 1,
    Cancelled = 2,
}

/// <summary>
/// فاتورة مشتريات — ضريبة المدخلات 15% قابلة للخصم إذا كان المورد مسجلاً.
/// القيد الآلي: مشتريات/مصروف + ضريبة مدخلات (مدين) ← الموردون (دائن).
/// </summary>
public class PurchaseInvoice : IBranchScoped, ILineDocumentTotals
{
    private readonly List<SalesInvoiceLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long SupplierId { get; private set; }

    /// <summary>رقم فاتورة المورد الورقية — مرجع إلزامي لخصم ضريبة المدخلات.</summary>
    public string SupplierInvoiceReference { get; private set; }

    /// <summary>الرقم الداخلي PI-{سنة}-{تسلسل} — يُمنح عند الترحيل.</summary>
    public string? InvoiceNumber { get; private set; }

    public DateOnly InvoiceDate { get; private set; }

    /// <summary>هل تُخصم ضريبة المدخلات؟ لا تُخصم إذا كان المورد غير مسجل.</summary>
    public bool VatDeductible { get; private set; }

    public PurchaseInvoiceStatus Status { get; private set; } = PurchaseInvoiceStatus.Draft;
    public long? JournalEntryId { get; private set; }

    /// <summary>أمر الشراء المرتبط — اختياري.</summary>
    public long? PurchaseOrderId { get; private set; }

    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();

    public decimal Subtotal { get; private set; }
    public decimal VatAmount { get; private set; }


    /// <summary>
    /// تاريخ الإلغاء (O-21، CEEF-219). الإقرار الضريبي يعكس **وقائع الفترة**:
    /// المستند الملغى يبقى في إقرار فترة إصداره كما قُدِّم للهيئة، ويظهر أثر إلغائه
    /// في فترة الإلغاء. بدون هذا التاريخ لا سبيل لوضع الأثر في فترته الصحيحة.
    /// </summary>
    public DateOnly? CancelledAt { get; private set; }

    // EF Core
    private PurchaseInvoice()
    {
        SupplierInvoiceReference = null!;
    }

    private PurchaseInvoice(long companyId, long supplierId, DateOnly invoiceDate,
        string supplierInvoiceReference, bool vatDeductible)
    {
        CompanyId = companyId;
        SupplierId = supplierId;
        InvoiceDate = invoiceDate;
        SupplierInvoiceReference = supplierInvoiceReference;
        VatDeductible = vatDeductible;
    }

    public static PurchaseInvoice CreateDraft(long companyId, long supplierId, DateOnly invoiceDate,
        string supplierInvoiceReference, bool vatDeductible)
    {
        if (supplierId <= 0)
            throw new AccountingException("المورد مطلوب للفاتورة.");
        if (string.IsNullOrWhiteSpace(supplierInvoiceReference))
            throw new AccountingException("رقم فاتورة المورد مطلوب — مرجع إلزامي للتدقيق وخصم الضريبة.");

        return new PurchaseInvoice(companyId, supplierId, invoiceDate, supplierInvoiceReference.Trim(), vatDeductible);
    }

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
        _lines.Add(new SalesInvoiceLine(description.Trim(), quantity, unitPrice, productId, vatCategory, taxExemptionReasonCode?.Trim(), taxExemptionReason?.Trim()));
        RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        (Subtotal, VatAmount) = LineDocumentTotalsCalculator.Compute(_lines);
    }

    /// <summary>
    /// الترحيل: مدين المشتريات/المخزون، مدين ضريبة المدخلات إن كانت قابلة للخصم، دائن الموردين.
    /// الأصناف المخزنية تُقيَّد على حساب المخزون؛ غير المخزنية على حساب المشتريات.
    /// </summary>
    public JournalEntry Post(string invoiceNumber, Account purchasesAccount, Account vatInputAccount,
        Account payableAccount, decimal inventorySubtotal = 0m, Account? inventoryAccount = null,
        decimal grniRelief = 0m, Account? grniAccount = null,
        decimal priceVariance = 0m, Account? varianceAccount = null)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new AccountingException("الفاتورة بلا سطور.");
        if (Subtotal <= 0)
            throw new AccountingException("إجمالي الفاتورة يجب أن يكون أكبر من صفر.");
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new AccountingException("الرقم الداخلي مطلوب عند الترحيل.");

        RecalculateTotals();

        var entry = JournalEntry.CreateDraft(
            CompanyId, InvoiceDate,
            $"فاتورة مشتريات {invoiceNumber} — مرجع المورد {SupplierInvoiceReference}",
            sourceDocument: $"PurchaseInvoice:{invoiceNumber}");

        if (VatDeductible)
        {
            var expenseSubtotal = Subtotal - inventorySubtotal - grniRelief - priceVariance;
            if (expenseSubtotal > 0)
                entry.AddLine(purchasesAccount, expenseSubtotal, 0m, "مشتريات غير مخزنية");
            if (inventorySubtotal > 0)
            {
                if (inventoryAccount is null)
                    throw new AccountingException("حساب المخزون مطلوب للأصناف المخزنية.");
                entry.AddLine(inventoryAccount, inventorySubtotal, 0m, "إدخال مخزون");
            }
            AddAccrualRelief(entry, grniRelief, grniAccount, priceVariance, varianceAccount);
            if (VatAmount > 0)
                entry.AddLine(vatInputAccount, VatAmount, 0m, "ضريبة مدخلات قابلة للخصم 15%");
        }
        else
        {
            // مورد غير مسجل: الضريبة غير قابلة للخصم وتُرسمل ضمن التكلفة
            if (inventorySubtotal > 0 && inventoryAccount is not null)
            {
                var expensePart = this.GrandTotal() - inventorySubtotal - grniRelief - priceVariance;
                entry.AddLine(inventoryAccount, inventorySubtotal, 0m, "إدخال مخزون");
                if (expensePart > 0)
                    entry.AddLine(purchasesAccount, expensePart, 0m, "مشتريات غير مخزنية");
            }
            else
                entry.AddLine(purchasesAccount, this.GrandTotal(), 0m, "قيمة المشتريات شاملة ضريبة غير قابلة للخصم");
        }

        entry.AddLine(payableAccount, 0m, this.GrandTotal(), "استحقاق للمورد");

        InvoiceNumber = invoiceNumber.Trim();
        Status = PurchaseInvoiceStatus.Posted;
        return entry;
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        EnsureDraft();
        BranchId = branchId;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void LinkPurchaseOrder(long purchaseOrderId)
    {
        if (purchaseOrderId <= 0)
            throw new AccountingException("أمر الشراء غير صالح.");
        PurchaseOrderId = purchaseOrderId;
    }

    public void MarkCancelled(DateOnly cancelledAt)
    {
        if (Status != PurchaseInvoiceStatus.Posted)
            throw new AccountingException("لا يُلغى إلا فاتورة مشتريات مرحّلة.");
        Status = PurchaseInvoiceStatus.Cancelled;
        CancelledAt = cancelledAt;
    }

    private void EnsureDraft()
    {
        if (Status != PurchaseInvoiceStatus.Draft)
            throw new AccountingException("الفاتورة مرحّلة — لا تُعدَّل.");
    }
    /// <summary>
    /// (CEEF-229 · م1-١٩) إقفالُ استحقاق الاستلام، وفرقُ السعر إن اختلف عن سعر الاستلام.
    ///
    /// <para><b>ولا يُمدَّن المخزونُ مرّةً ثانية:</b> وجودُ إذنِ استلامٍ يمنع حركةً مخزنيةً
    /// جديدة، فمتوسطُ التكلفة لا يُعاد حسابه — وأيُّ ريالٍ يُضاف إلى 1107 هنا لا يقابله شيءٌ
    /// في الدفتر المساعد، <b>فيكسر الهوية هـ-٣ كسراً دائماً</b>. فالفرقُ يذهب مصروفاً.</para>
    /// </summary>
    private static void AddAccrualRelief(
        JournalEntry entry, decimal grniRelief, Account? grniAccount,
        decimal priceVariance, Account? varianceAccount)
    {
        if (grniRelief > 0)
        {
            if (grniAccount is null)
                throw new AccountingException("حساب البضاعة المستلمة غير المفوترة مطلوب لإقفال الاستحقاق.");
            entry.AddLine(grniAccount, grniRelief, 0m, "إقفال بضاعة مستلمة غير مفوترة");
        }

        if (priceVariance == 0m)
            return;

        if (varianceAccount is null)
            throw new AccountingException("حساب فروق أسعار المشتريات مطلوب عند اختلاف سعر الفاتورة عن الاستلام.");

        if (priceVariance > 0)
            entry.AddLine(varianceAccount, priceVariance, 0m, "فرق سعر مشتريات");
        else
            entry.AddLine(varianceAccount, 0m, -priceVariance, "فرق سعر مشتريات");
    }

}
