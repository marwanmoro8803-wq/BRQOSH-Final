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

    /// <summary>
    /// (CEEF-230 · ط4-٥) عملةُ الفاتورة — و<c>SAR</c> هي الأصل.
    ///
    /// <para><b>وكان المستوردُ لا يستطيع تسجيلَ فاتورةٍ بالدولار إطلاقاً</b>: يُدخلها
    /// محوَّلةً بيده، <b>فتضيع القيمةُ الأصلية</b> ولا يُعرف عند السداد بأيّ سعرٍ نشأت —
    /// ولا يُحسب فرقُ عملةٍ محقَّق. وأُعلن هذا النقصُ في <c>ط1-٤</c> وبقي.</para>
    /// </summary>
    public string CurrencyCode { get; private set; } = "SAR";

    /// <summary>سعرُ الصرف <b>يوم الفاتورة</b> — وبه نشأت الذمّة، وإليه يُقاس فرقُ السداد.</summary>
    public decimal ExchangeRate { get; private set; } = 1m;

    /// <summary>الأساسُ بعملة الفاتورة — <b>وهو ما يُطالِب به المورّد</b>.</summary>
    public decimal ForeignSubtotal { get; private set; }

    /// <summary>الضريبةُ بعملة الفاتورة.</summary>
    public decimal ForeignVatAmount { get; private set; }

    public decimal ForeignGrandTotal => ForeignSubtotal + ForeignVatAmount;

    public bool IsForeignCurrency => CurrencyCode != "SAR";

    public void SetCurrency(string currencyCode, decimal exchangeRate)
    {
        if (Status != PurchaseInvoiceStatus.Draft)
            throw new AccountingException("الفاتورة مرحّلة — لا تُعدَّل.");
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3)
            throw new AccountingException("رمز العملة ثلاثةُ أحرفٍ بمعيار ISO 4217.");
        if (exchangeRate <= 0m)
            throw new AccountingException("سعرُ الصرف يجب أن يكون موجباً.");

        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        ExchangeRate = exchangeRate;
        RecalculateTotals();
    }

    /// <summary>
    /// <b>ويُحسب بعملة الفاتورة ثمّ يُضرب في السعر</b> — لا العكس.
    ///
    /// <para>فالضريبةُ تُحسب على الأساس <b>بعملته</b> ثمّ يُحوَّل الاثنان. <b>وتحويلُ
    /// السطور أوّلاً ثمّ حسابُ الضريبة يُنتج هللاتِ فرقٍ لا تُطابق فاتورةَ المورّد</b> —
    /// وهي التي يُطالِب بها.</para>
    /// </summary>
    public void RecalculateTotals()
    {
        (ForeignSubtotal, ForeignVatAmount) = LineDocumentTotalsCalculator.Compute(_lines);

        Subtotal = Math.Round(ForeignSubtotal * ExchangeRate, 2, MidpointRounding.AwayFromZero);
        VatAmount = Math.Round(ForeignVatAmount * ExchangeRate, 2, MidpointRounding.AwayFromZero);
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
            // ‏مورد غير مسجل: الضريبة غير قابلة للخصم وتُرسمل ضمن التكلفة — **فالوعاءُ
            // ‏`GrandTotal` لا `Subtotal`**، إذ لا سطرَ ضريبةِ مدخلاتٍ يحمل فرقَها.
            //
            // (جاهزيّةُ الإطلاق · ح-٣) ‏**وكان هذا الفرعُ لا يُقفل استحقاقَ الاستلام أبداً.**
            //
            // ‏`AddAccrualRelief` — وهي وحدَها ما يُمدِّن حسابَ «بضاعةٌ وردت ولم تُفوتر» —
            // ‏كانت تُستدعى في فرع المورّد المسجَّل وحدَه. **والفرعان يصفان المورّدَ لا
            // ‏المستند**: إذنُ الاستلام يسبق الفاتورةَ في الحالتين، وحسابُ الاستحقاق يُدان
            // ‏في الحالتين. فوقع أثران بحسب المسار:
            //
            // ‏**(أ) والصنفُ مخزنيّ:** المدينُ كان `GrandTotal - grniRelief - priceVariance`
            // ‏والدائنُ `GrandTotal` — **فالقيدُ ناقصٌ بمقدار الإقفال ويرتدّ على حارس
            // ‏التوازن**، أي لا تُرحَّل الفاتورةُ أصلاً.
            //
            // ‏**(ب) وغيرُ المخزنيّ** (أو بلا حساب مخزون): كان يُمدَّن `GrandTotal` كاملاً
            // ‏على المشتريات **ويُترك 2112 دائناً إلى الأبد** — والتكلفةُ دخلت مرّةً بإذن
            // ‏الاستلام ومرّةً هنا. **والميزانُ سليمٌ تماماً فلا يشكو أحد**: التزامٌ وهميٌّ
            // ‏يكبر كلَّ شهر، ومشترياتٌ مُضخَّمة.
            //
            // ‏والعلاجُ أن يتماثل الفرعان: الوعاءُ وحدَه يختلف.
            var expensePart = this.GrandTotal() - inventorySubtotal - grniRelief - priceVariance;

            if (inventorySubtotal > 0)
            {
                if (inventoryAccount is null)
                    throw new AccountingException("حساب المخزون مطلوب للأصناف المخزنية.");
                entry.AddLine(inventoryAccount, inventorySubtotal, 0m, "إدخال مخزون");
            }

            if (expensePart > 0)
                entry.AddLine(purchasesAccount, expensePart, 0m,
                    "قيمة المشتريات شاملة ضريبة غير قابلة للخصم");

            AddAccrualRelief(entry, grniRelief, grniAccount, priceVariance, varianceAccount);
        }

        entry.AddLine(payableAccount, 0m, this.GrandTotal(), "استحقاق للمورد");

        InvoiceNumber = invoiceNumber.Trim();
        Status = PurchaseInvoiceStatus.Posted;
        return entry;
    }

    /// <summary>
    /// (CEEF-230 · ط1-٧) مركزُ تكلفة المستند — يُختم على سطور المصروف في قيده.
    ///
    /// <para><b>وعلى المستند لا على السطر</b>: فاتورةُ كهرباءٍ كلُّها لمركزٍ واحد، وتوزيعُ
    /// سطورها على مراكزَ شتّى حالةٌ نادرةٌ تُعالَج بقيدٍ يدويّ. <b>وحقلٌ على كلّ سطرٍ
    /// يُطلب ملؤه دائماً يُملأ عشوائياً</b>.</para>
    /// </summary>
    public long? CostCenterId { get; private set; }

    /// <summary>يربط الفاتورةَ بمركز تكلفة — ولا يُقبل بعد الترحيل.</summary>
    public void AssignCostCenter(long? costCenterId)
    {
        EnsureDraft();
        CostCenterId = costCenterId;
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
