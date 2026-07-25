using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Retail;

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
public class SalesInvoice : IBranchScoped, ILineDocumentTotals
{
    public const decimal SaudiVatRate = 0.15m;

    private readonly List<SalesInvoiceLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long CustomerId { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public SalesInvoiceStatus Status { get; private set; } = SalesInvoiceStatus.Draft;

    /// <summary>القيد المحاسبي المتولد عند الترحيل.</summary>
    public long? JournalEntryId { get; private set; }

    /// <summary>وردية POS إن وُجدت — لربط تقارير الورديات.</summary>
    public long? PosShiftId { get; private set; }

    public PosPaymentMethod? PosPaymentMethod { get; private set; }

    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();

    /// <summary>الإجمالي قبل الضريبة — مُخزَّن ويُحدَّث مع السطور.</summary>
    public decimal Subtotal { get; private set; }

    /// <summary>ضريبة القيمة المضافة — مُخزَّنة.</summary>
    public decimal VatAmount { get; private set; }


    /// <summary>
    /// تاريخ الإلغاء (O-21، CEEF-219). الإقرار الضريبي يعكس **وقائع الفترة**:
    /// المستند الملغى يبقى في إقرار فترة إصداره كما قُدِّم للهيئة، ويظهر أثر إلغائه
    /// في فترة الإلغاء. بدون هذا التاريخ لا سبيل لوضع الأثر في فترته الصحيحة.
    /// </summary>
    public DateOnly? CancelledAt { get; private set; }

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

    public void AddLine(string description, decimal quantity, decimal unitPrice, long? productId = null,
        Tax.VatCategory vatCategory = Tax.VatCategory.Standard)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف الصنف مطلوب في سطر الفاتورة.");
        if (quantity <= 0)
            throw new AccountingException("الكمية يجب أن تكون أكبر من صفر.");
        if (unitPrice < 0)
            throw new AccountingException("سعر الوحدة لا يكون سالباً.");

        _lines.Add(new SalesInvoiceLine(description.Trim(), quantity, unitPrice, productId, vatCategory));
        RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        (Subtotal, VatAmount) = LineDocumentTotalsCalculator.Compute(_lines);
    }

    /// <summary>
    /// ترحيل الفاتورة وتوليد قيدها: مدين العميل بالإجمالي، دائن الإيراد بالوعاء، دائن ضريبة المخرجات.
    /// إذا وُجدت أصناف مخزنية: يُضاف قيد تكلفة المبيعات (مدين COGS / دائن مخزون).
    /// </summary>
    public JournalEntry Post(string invoiceNumber, Account receivableAccount, Account revenueAccount,
        Account vatOutputAccount, decimal cogsAmount = 0m, Account? cogsAccount = null, Account? inventoryAccount = null)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new AccountingException("الفاتورة بلا سطور — أضف صنفاً واحداً على الأقل.");
        if (Subtotal <= 0)
            throw new AccountingException("إجمالي الفاتورة يجب أن يكون أكبر من صفر.");
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new AccountingException("رقم الفاتورة مطلوب عند الترحيل.");

        RecalculateTotals();

        var entry = JournalEntry.CreateDraft(
            CompanyId, InvoiceDate,
            $"فاتورة مبيعات رقم {invoiceNumber}",
            sourceDocument: $"SalesInvoice:{invoiceNumber}");

        entry.AddLine(receivableAccount, this.GrandTotal(), 0m, "إجمالي الفاتورة شامل الضريبة");
        entry.AddLine(revenueAccount, 0m, Subtotal, "إيراد المبيعات");
        if (VatAmount > 0)
            entry.AddLine(vatOutputAccount, 0m, VatAmount, "ضريبة القيمة المضافة 15%");

        if (cogsAmount > 0)
        {
            if (cogsAccount is null || inventoryAccount is null)
                throw new AccountingException("حسابات تكلفة المبيعات والمخزون مطلوبة.");
            entry.AddLine(cogsAccount, cogsAmount, 0m, "تكلفة البضاعة المباعة");
            entry.AddLine(inventoryAccount, 0m, cogsAmount, "صرف من المخزون");
        }

        InvoiceNumber = invoiceNumber.Trim();
        Status = SalesInvoiceStatus.Posted;
        return entry;
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        EnsureDraft();
        BranchId = branchId;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void MarkPosSale(long shiftId, PosPaymentMethod method)
    {
        PosShiftId = shiftId;
        PosPaymentMethod = method;
    }

    /// <summary>إلغاء فاتورة مرحّلة — يتطلب عكس القيد وإرجاع المخزون من الخدمة.</summary>
    public void MarkCancelled(DateOnly cancelledAt)
    {
        if (Status != SalesInvoiceStatus.Posted)
            throw new AccountingException("لا يُلغى إلا فاتورة مرحّلة.");
        Status = SalesInvoiceStatus.Cancelled;
        CancelledAt = cancelledAt;
    }

    private void EnsureDraft()
    {
        if (Status != SalesInvoiceStatus.Draft)
            throw new AccountingException("الفاتورة مرحّلة — التعديل ممنوع، والتصحيح يكون بإشعار دائن.");
    }
}

public record SalesInvoiceLine(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    long? ProductId = null,
    Tax.VatCategory VatCategory = Tax.VatCategory.Standard)
{
    public decimal LineTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
}
