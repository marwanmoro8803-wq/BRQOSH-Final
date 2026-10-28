using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Sales;

public enum SalesOrderStatus
{
    Draft = 0,
    Confirmed = 1,
    Invoiced = 2,
    Cancelled = 3,
}

/// <summary>أمر بيع — LIFECYCLE-32/33 (Draft → Confirmed → Invoiced).</summary>
public class SalesOrder : ILineDocumentTotals
{
    private readonly List<SalesInvoiceLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long CustomerId { get; private set; }
    public long? SalesQuotationId { get; private set; }
    public string? OrderNumber { get; private set; }
    public DateOnly OrderDate { get; private set; }
    public SalesOrderStatus Status { get; private set; } = SalesOrderStatus.Draft;
    public long? SalesInvoiceId { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal VatAmount { get; private set; }
    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();

    private SalesOrder() { }

    private SalesOrder(long companyId, long customerId, DateOnly orderDate, long? quotationId)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        OrderDate = orderDate;
        SalesQuotationId = quotationId;
    }

    public static SalesOrder CreateDraft(long companyId, long customerId, DateOnly orderDate, long? quotationId = null)
    {
        if (customerId <= 0)
            throw new AccountingException("العميل مطلوب لأمر البيع.");
        return new SalesOrder(companyId, customerId, orderDate, quotationId);
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

        // (CEEF-229 · م1-١ب الشقّ الثاني) الرفضُ عند بناء السطر — أبكرُ موضعٍ ممكن.
        // فالفئةُ غيرُ الأساسية بلا سببٍ نظاميّ تُنتج مستنداً ترفضه الهيئة (BR-KSA-CL-06)،
        // ولا يُصلحه ملءُ البيانات بعد الترحيل: العدّادُ استُهلك والبصمةُ بُنيت عليه.
        if (Tax.VatExemptionReason.IsRequiredFor(vatCategory))
        {
            var reason = Tax.VatExemptionReason.Find(taxExemptionReasonCode)
                ?? throw new AccountingException(
                    $"سطر «{description}» بفئة «{Tax.VatCategoryExtensions.UblCode(vatCategory)}» يتطلّب سبب إعفاء نظاميّاً.");

            if (reason.Category != vatCategory)
                throw new AccountingException(
                    $"رمز «{reason.Code}» يخصّ فئة «{Tax.VatCategoryExtensions.UblCode(reason.Category)}» "
                    + $"لا فئة «{Tax.VatCategoryExtensions.UblCode(vatCategory)}».");

            // ‏ونصُّ السبب من القائمة لا من المستخدم — إلا «خارج النطاق»، فالمعيار يقول
            // ‏صراحةً إنّ نصَّها حرٌّ يكتبه المكلَّف حالةً بحالة. وبهذا لا ينحرف النصُّ عن رمزه.
            if (reason.Code != Tax.VatExemptionReason.OutOfScopeCode)
                taxExemptionReason = reason.EnglishText;
        }

        _lines.Add(new SalesInvoiceLine(description.Trim(), quantity, unitPrice, productId, vatCategory, taxExemptionReasonCode?.Trim(), taxExemptionReason?.Trim()));
        RecalculateTotals();
    }

    public void Confirm(string orderNumber)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new AccountingException("أمر البيع بلا سطور.");
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new AccountingException("رقم أمر البيع مطلوب عند التأكيد.");
        OrderNumber = orderNumber.Trim();
        Status = SalesOrderStatus.Confirmed;
    }

    public void MarkInvoiced(long invoiceId)
    {
        if (Status != SalesOrderStatus.Confirmed)
            throw new AccountingException("يُفوتر أمر مؤكد فقط.");
        SalesInvoiceId = invoiceId;
        Status = SalesOrderStatus.Invoiced;
    }

    /// <summary>عند إلغاء الفاتورة الصادرة من الأمر يعود الأمر «مؤكداً» ويُفكّ ربطه بها ليُفوتر من جديد.</summary>
    public void ReopenAfterInvoiceCancelled(long invoiceId)
    {
        if (Status != SalesOrderStatus.Invoiced || SalesInvoiceId != invoiceId)
            throw new AccountingException("أمر البيع غير مرتبط بهذه الفاتورة.");
        SalesInvoiceId = null;
        Status = SalesOrderStatus.Confirmed;
    }

    public void Cancel()
    {
        if (Status == SalesOrderStatus.Invoiced)
            throw new AccountingException("لا يُلغى أمر مفوتر.");
        Status = SalesOrderStatus.Cancelled;
    }

    private void EnsureDraft()
    {
        if (Status != SalesOrderStatus.Draft)
            throw new AccountingException("لا تُعدَّل أوامر البيع بعد التأكيد.");
    }

    private void RecalculateTotals()
    {
        (Subtotal, VatAmount) = LineDocumentTotalsCalculator.Compute(_lines);
    }
}
