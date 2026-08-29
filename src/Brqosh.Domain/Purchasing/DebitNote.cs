using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Sales;

namespace Brqosh.Domain.Purchasing;

public enum DebitNoteStatus
{
    Draft = 0,
    Posted = 1,
    Cancelled = 2,
}

/// <summary>
/// إشعار مدين للمشتريات (خصم/مرتجع مشتريات) — يصحّح فاتورة مشتريات مرحّلة.
/// القيد: دائن مشتريات/مخزون + دائن ضريبة مدخلات ← مدين الموردين.
/// </summary>
public class DebitNote : IBranchScoped, ILineDocumentTotals
{
    private readonly List<SalesInvoiceLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long SupplierId { get; private set; }
    public long OriginalPurchaseInvoiceId { get; private set; }
    public string? NoteNumber { get; private set; }
    public DateOnly NoteDate { get; private set; }
    public string Reason { get; private set; }
    public DebitNoteStatus Status { get; private set; } = DebitNoteStatus.Draft;
    public long? JournalEntryId { get; private set; }

    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();
    public decimal Subtotal { get; private set; }
    public decimal VatAmount { get; private set; }

    /// <summary>
    /// تاريخ الإلغاء (O-21، CEEF-219). الإقرار الضريبي يعكس **وقائع الفترة**:
    /// المستند الملغى يبقى في إقرار فترة إصداره كما قُدِّم للهيئة، ويظهر أثر إلغائه
    /// في فترة الإلغاء. بدون هذا التاريخ لا سبيل لوضع الأثر في فترته الصحيحة.
    /// </summary>
    public DateOnly? CancelledAt { get; private set; }

    private DebitNote() { Reason = null!; }

    private DebitNote(long companyId, long supplierId, long originalPurchaseInvoiceId, DateOnly noteDate, string reason)
    {
        CompanyId = companyId;
        SupplierId = supplierId;
        OriginalPurchaseInvoiceId = originalPurchaseInvoiceId;
        NoteDate = noteDate;
        Reason = reason;
    }

    public static DebitNote CreateDraft(long companyId, long supplierId, long originalPurchaseInvoiceId,
        DateOnly noteDate, string reason)
    {
        if (originalPurchaseInvoiceId <= 0)
            throw new AccountingException("إشعار المدين يجب أن يرتبط بفاتورة مشتريات أصلية.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new AccountingException("سبب الإشعار مطلوب.");
        return new DebitNote(companyId, supplierId, originalPurchaseInvoiceId, noteDate, reason.Trim());
    }

    public void AddLine(string description, decimal quantity, decimal unitPrice, long? productId = null,
        Tax.VatCategory vatCategory = Tax.VatCategory.Standard,
        string? taxExemptionReasonCode = null, string? taxExemptionReason = null)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف السطر مطلوب.");
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

    public JournalEntry Post(string noteNumber, Account purchasesOrInventory, Account vatInput,
        Account payable, decimal inventoryReduction = 0m, Account? inventoryAccount = null,
        Account? purchasesExpense = null)
    {
        EnsureDraft();
        if (_lines.Count == 0) throw new AccountingException("إشعار المدين بلا سطور.");
        if (Subtotal <= 0) throw new AccountingException("قيمة الإشعار يجب أن تكون أكبر من صفر.");
        if (string.IsNullOrWhiteSpace(noteNumber))
            throw new AccountingException("رقم الإشعار مطلوب.");

        RecalculateTotals();

        var entry = JournalEntry.CreateDraft(CompanyId, NoteDate,
            $"إشعار مدين مشتريات {noteNumber} — {Reason}",
            sourceDocument: $"DebitNote:{noteNumber}");

        // تخفيض ذمة المورد
        entry.AddLine(payable, this.GrandTotal(), 0m, "تخفيض ذمة المورد");

        var nonInv = Subtotal - inventoryReduction;
        if (inventoryReduction > 0)
        {
            if (inventoryAccount is null)
                throw new AccountingException("حساب المخزون مطلوب لتخفيض أصناف مخزنية.");
            entry.AddLine(inventoryAccount, 0m, inventoryReduction, "تخفيض مخزون — إشعار مدين");
        }
        if (nonInv > 0)
            entry.AddLine(purchasesExpense ?? purchasesOrInventory, 0m, nonInv, "تخفيض مشتريات/مصروف");

        if (VatAmount > 0)
            entry.AddLine(vatInput, 0m, VatAmount, "تخفيض ضريبة المدخلات 15%");

        NoteNumber = noteNumber.Trim();
        Status = DebitNoteStatus.Posted;
        return entry;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void MarkCancelled(DateOnly cancelledAt)
    {
        if (Status != DebitNoteStatus.Posted)
            throw new AccountingException("لا يُلغى إلا إشعار مدين مرحّل.");
        Status = DebitNoteStatus.Cancelled;
        CancelledAt = cancelledAt;
    }

    private void EnsureDraft()
    {
        if (Status != DebitNoteStatus.Draft)
            throw new AccountingException("إشعار المدين غير قابل للتعديل.");
    }
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        EnsureDraft();
        BranchId = branchId;
    }

}
