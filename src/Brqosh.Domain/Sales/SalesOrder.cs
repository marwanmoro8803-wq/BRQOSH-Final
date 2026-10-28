using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Sales;

public enum SalesOrderStatus
{
    Draft = 0,
    Confirmed = 1,
    Invoiced = 2,
    Cancelled = 3,
    /// <summary>فُوتر بعضُه — ويبقى قابلاً لفاتورةٍ أخرى بالمتبقّي.</summary>
    PartInvoiced = 4,
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

    /// <summary>‏خصمُ المستند (مبلغاً بعملة الأمر) — يُوزَّع على فواتيره بنسبة صافي ما تُفوتره، والأخيرةُ تأخذ الباقي.</summary>
    public decimal DocumentDiscountAmount { get; private set; }

    /// <summary>‏عملةُ الأمر وسعرُ صرفه — تنتقلان إلى فواتيره.</summary>
    public string CurrencyCode { get; private set; } = "SAR";
    public decimal ExchangeRate { get; private set; } = 1m;
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
        string? taxExemptionReasonCode = null, string? taxExemptionReason = null, decimal discountPercent = 0m)
    {
        EnsureDraft();
        if (discountPercent < 0m || discountPercent > 100m)
            throw new AccountingException(
                $"نسبة خصم السطر «{description}» يجب أن تكون بين 0 و100 — ووردت {discountPercent:N2}.");
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف الصنف مطلوب.");
        if (quantity <= 0 || unitPrice < 0)
            throw new AccountingException("الكمية يجب أن تكون موجبة والسعر لا يكون سالباً.");
        DocumentLineScale.EnsureUnitPriceHalala(unitPrice);

        // الرفضُ عند بناء السطر — أبكرُ موضعٍ ممكن.
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

        _lines.Add(new SalesInvoiceLine(description.Trim(), quantity, unitPrice, productId, vatCategory, taxExemptionReasonCode?.Trim(), taxExemptionReason?.Trim(),
            DiscountPercent: Math.Round(discountPercent, 4, MidpointRounding.AwayFromZero)));
        RecalculateTotals();
    }

    public void ApplyDocumentDiscount(decimal amount)
    {
        EnsureDraft();
        if (amount < 0m)
            throw new AccountingException("خصم المستند لا يكون سالباً.");
        var linesNet = _lines.Sum(l => l.LineTotal);
        if (amount > linesNet)
            throw new AccountingException(
                $"خصم المستند {amount:N2} يتجاوز صافي السطور {linesNet:N2} — وفاتورةٌ بوعاءٍ سالبٍ إشعارُ دائنٍ لا فاتورة.");
        DocumentDiscountAmount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        RecalculateTotals();
    }

    public void SetCurrency(string currencyCode, decimal exchangeRate, string? booksCurrencyCode = null)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3)
            throw new AccountingException("رمز العملة ثلاثةُ أحرفٍ بمعيار ISO 4217.");
        if (exchangeRate <= 0m)
            throw new AccountingException("سعرُ الصرف يجب أن يكون موجباً.");
        BooksCurrency.Validate(currencyCode, exchangeRate, booksCurrencyCode);
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        ExchangeRate = exchangeRate;
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
    /// <summary>‏فاتورةٌ (كاملةٌ أو جزئيّة) صدرت من الأمر — <paramref name="fullyInvoiced"/> حين لا يبقى شيء.</summary>
    public void RecordInvoice(long invoiceId, bool fullyInvoiced)
    {
        if (Status is not (SalesOrderStatus.Confirmed or SalesOrderStatus.PartInvoiced))
            throw new AccountingException("يُفوتر أمر مؤكد فقط.");
        SalesInvoiceId = invoiceId;
        Status = fullyInvoiced ? SalesOrderStatus.Invoiced : SalesOrderStatus.PartInvoiced;
    }

    /// <summary>‏أُلغيت إحدى فواتير الأمر: يعود «مؤكداً» إن لم تبقَ فاتورةٌ قائمة، وإلا «مفوتراً جزئياً».</summary>
    public void AfterInvoiceCancelled(long? latestStandingInvoiceId)
    {
        if (Status is not (SalesOrderStatus.Invoiced or SalesOrderStatus.PartInvoiced))
            throw new AccountingException("أمر البيع غير مرتبط بهذه الفاتورة.");
        SalesInvoiceId = latestStandingInvoiceId;
        Status = latestStandingInvoiceId is null ? SalesOrderStatus.Confirmed : SalesOrderStatus.PartInvoiced;
    }

    public void ReopenAfterInvoiceCancelled(long invoiceId)
    {
        if (Status != SalesOrderStatus.Invoiced || SalesInvoiceId != invoiceId)
            throw new AccountingException("أمر البيع غير مرتبط بهذه الفاتورة.");
        SalesInvoiceId = null;
        Status = SalesOrderStatus.Confirmed;
    }

    public void Cancel()
    {
        if (Status is SalesOrderStatus.Invoiced or SalesOrderStatus.PartInvoiced)
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
        (Subtotal, VatAmount) = LineDocumentTotalsCalculator.Compute(_lines, documentDiscount: DocumentDiscountAmount);
    }
}
