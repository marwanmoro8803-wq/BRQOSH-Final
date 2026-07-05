using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Sales;

public enum SalesInvoiceStatus
{
    Draft = 0,

    /// <summary>مرحّلة — ولّدت قيدها المحاسبي ولا تُعدَّل (التصحيح بإشعار دائن).</summary>
    Posted = 1,

    Cancelled = 2,
}

/// <summary>
/// فاتورة مبيعات بضريبة القيمة المضافة السعودية (15%).
/// الترحيل يولّد القيد آلياً: عميل/نقدية (مدين) ← إيراد مبيعات + ضريبة مخرجات (دائن).
/// </summary>
public class SalesInvoice
{
    public const decimal SaudiVatRate = 0.15m;

    private readonly List<SalesInvoiceLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long CustomerId { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public SalesInvoiceStatus Status { get; private set; } = SalesInvoiceStatus.Draft;

    /// <summary>القيد المحاسبي المتولد عند الترحيل.</summary>
    public long? JournalEntryId { get; private set; }

    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();

    /// <summary>الإجمالي قبل الضريبة (وعاء الضريبة).</summary>
    public decimal Subtotal => _lines.Sum(l => l.LineTotal);

    /// <summary>ضريبة القيمة المضافة — تُقرَّب لهللتين على مستوى الفاتورة.</summary>
    public decimal VatAmount => Math.Round(Subtotal * SaudiVatRate, 2, MidpointRounding.AwayFromZero);

    /// <summary>الإجمالي شامل الضريبة.</summary>
    public decimal GrandTotal => Subtotal + VatAmount;

    // EF Core
    private SalesInvoice()
    {
    }

    private SalesInvoice(long companyId, long customerId, DateOnly invoiceDate)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        InvoiceDate = invoiceDate;
    }

    public static SalesInvoice CreateDraft(long companyId, long customerId, DateOnly invoiceDate)
    {
        if (customerId <= 0)
            throw new AccountingException("العميل مطلوب للفاتورة.");
        return new SalesInvoice(companyId, customerId, invoiceDate);
    }

    public void AddLine(string description, decimal quantity, decimal unitPrice)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف الصنف مطلوب في سطر الفاتورة.");
        if (quantity <= 0)
            throw new AccountingException("الكمية يجب أن تكون أكبر من صفر.");
        if (unitPrice < 0)
            throw new AccountingException("سعر الوحدة لا يكون سالباً.");

        _lines.Add(new SalesInvoiceLine(description.Trim(), quantity, unitPrice));
    }

    /// <summary>
    /// ترحيل الفاتورة وتوليد قيدها: مدين العميل بالإجمالي، دائن الإيراد بالوعاء، دائن ضريبة المخرجات.
    /// </summary>
    public JournalEntry Post(string invoiceNumber, Account receivableAccount, Account revenueAccount, Account vatOutputAccount)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new AccountingException("الفاتورة بلا سطور — أضف صنفاً واحداً على الأقل.");
        if (Subtotal <= 0)
            throw new AccountingException("إجمالي الفاتورة يجب أن يكون أكبر من صفر.");
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new AccountingException("رقم الفاتورة مطلوب عند الترحيل.");

        var entry = JournalEntry.CreateDraft(
            CompanyId, InvoiceDate,
            $"فاتورة مبيعات رقم {invoiceNumber}",
            sourceDocument: $"SalesInvoice:{invoiceNumber}");

        entry.AddLine(receivableAccount, GrandTotal, 0m, "إجمالي الفاتورة شامل الضريبة");
        entry.AddLine(revenueAccount, 0m, Subtotal, "إيراد المبيعات");
        if (VatAmount > 0)
            entry.AddLine(vatOutputAccount, 0m, VatAmount, "ضريبة القيمة المضافة 15%");

        InvoiceNumber = invoiceNumber.Trim();
        Status = SalesInvoiceStatus.Posted;
        return entry;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    private void EnsureDraft()
    {
        if (Status != SalesInvoiceStatus.Draft)
            throw new AccountingException("الفاتورة مرحّلة — التعديل ممنوع، والتصحيح يكون بإشعار دائن.");
    }
}

public record SalesInvoiceLine(string Description, decimal Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
}
