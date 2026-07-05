using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Sales;

public enum CreditNoteStatus
{
    Draft = 0,
    Posted = 1,
}

/// <summary>
/// إشعار دائن (مردودات ومسموحات المبيعات) — الأداة النظامية الوحيدة لتصحيح فاتورة مرحّلة.
/// القيد الآلي: مردودات المبيعات + ضريبة المخرجات (مدين) ← العملاء (دائن).
/// </summary>
public class CreditNote
{
    private readonly List<SalesInvoiceLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long CustomerId { get; private set; }

    /// <summary>الفاتورة الأصلية محل التصحيح — إلزامية نظاماً (متطلب هيئة الزكاة).</summary>
    public long OriginalInvoiceId { get; private set; }

    public string? NoteNumber { get; private set; }
    public DateOnly NoteDate { get; private set; }
    public string Reason { get; private set; }
    public CreditNoteStatus Status { get; private set; } = CreditNoteStatus.Draft;
    public long? JournalEntryId { get; private set; }

    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();

    public decimal Subtotal => _lines.Sum(l => l.LineTotal);
    public decimal VatAmount => Math.Round(Subtotal * SalesInvoice.SaudiVatRate, 2, MidpointRounding.AwayFromZero);
    public decimal GrandTotal => Subtotal + VatAmount;

    // EF Core
    private CreditNote()
    {
        Reason = null!;
    }

    private CreditNote(long companyId, long customerId, long originalInvoiceId, DateOnly noteDate, string reason)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        OriginalInvoiceId = originalInvoiceId;
        NoteDate = noteDate;
        Reason = reason;
    }

    public static CreditNote CreateDraft(long companyId, long customerId, long originalInvoiceId, DateOnly noteDate, string reason)
    {
        if (originalInvoiceId <= 0)
            throw new AccountingException("إشعار الدائن يجب أن يرتبط بفاتورة أصلية.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new AccountingException("سبب الإشعار مطلوب (مرتجع، خصم، تصحيح خطأ...).");
        return new CreditNote(companyId, customerId, originalInvoiceId, noteDate, reason.Trim());
    }

    public void AddLine(string description, decimal quantity, decimal unitPrice)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف السطر مطلوب.");
        if (quantity <= 0 || unitPrice < 0)
            throw new AccountingException("الكمية يجب أن تكون موجبة والسعر لا يكون سالباً.");
        _lines.Add(new SalesInvoiceLine(description.Trim(), quantity, unitPrice));
    }

    /// <summary>
    /// الترحيل: مدين مردودات المبيعات بالوعاء، مدين ضريبة المخرجات (تخفيض)، دائن العملاء بالإجمالي.
    /// </summary>
    public JournalEntry Post(string noteNumber, Account salesReturnsAccount, Account vatOutputAccount, Account receivableAccount)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new AccountingException("إشعار الدائن بلا سطور.");
        if (Subtotal <= 0)
            throw new AccountingException("قيمة الإشعار يجب أن تكون أكبر من صفر.");
        if (string.IsNullOrWhiteSpace(noteNumber))
            throw new AccountingException("رقم الإشعار مطلوب عند الترحيل.");

        var entry = JournalEntry.CreateDraft(
            CompanyId, NoteDate,
            $"إشعار دائن رقم {noteNumber} — {Reason}",
            sourceDocument: $"CreditNote:{noteNumber}");

        entry.AddLine(salesReturnsAccount, Subtotal, 0m, "مردودات ومسموحات المبيعات");
        if (VatAmount > 0)
            entry.AddLine(vatOutputAccount, VatAmount, 0m, "تخفيض ضريبة المخرجات 15%");
        entry.AddLine(receivableAccount, 0m, GrandTotal, "تخفيض ذمة العميل");

        NoteNumber = noteNumber.Trim();
        Status = CreditNoteStatus.Posted;
        return entry;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    private void EnsureDraft()
    {
        if (Status != CreditNoteStatus.Draft)
            throw new AccountingException("إشعار الدائن مرحّل — لا يُعدَّل.");
    }
}
