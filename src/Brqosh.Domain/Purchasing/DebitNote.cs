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
    /// (جاهزيّةُ الإطلاق · ح-٣٩) <b>والإشعارُ المدينُ يحمل عملةَ فاتورتِه —
    /// مرآةُ ح-٣٥ في المشتريات.</b>
    ///
    /// <para><b>وكان بلا عملةٍ أصلاً</b>، وسطورُ فاتورةِ الشراء الأجنبيّة
    /// <b>مُسعّرةٌ بعملتِها</b>. <b>وقُيس</b>: شراءٌ بـ<c>USD 1,000</c> بسعر <c>3.75</c>
    /// (٤٬٣١٢٫٥٠)، <b>ومرتجعٌ كاملٌ يُنقص المستحقَّ للمورّد ١٬١٥٠ فقط</b> —
    /// <b>فتبقى الدفاترُ تقول إنّا مدينون بـ٣٬١٦٢٫٥٠ وقد أعدنا كلَّ شيء</b>،
    /// وضريبةُ المدخلات تُعكَس بـ١٥٠ من ٥٦٢٫٥٠ <b>فيُستردّ ما لم يُدفَع</b>.</para>
    ///
    /// <para><b>والمرآةُ <c>PurchaseInvoice</c> حرفاً</b>، <b>والافتراضُ ريالٌ بسعرِ واحد
    /// فلا يتغيّر رقمٌ في إشعارٍ قائم</b>.</para>
    /// </summary>
    public string CurrencyCode { get; private set; } = "SAR";
    public decimal ExchangeRate { get; private set; } = 1m;
    public decimal ForeignSubtotal { get; private set; }
    public decimal ForeignVatAmount { get; private set; }

    /// <summary>الإجماليُّ بعملة المستند — وهو ما يُستردّ من المورّد.</summary>
    public decimal ForeignGrandTotal => ForeignSubtotal + ForeignVatAmount;

    /// <summary>عملةُ المستند تخالف عملةَ الدفاتر.</summary>
    public bool IsForeignCurrency => CurrencyCode != "SAR";

    /// <summary><b>تُورَّث من الفاتورة الأصليّة لا تُختار</b> — وسعرٌ آخرُ يخلق فرقَ صرفٍ لم يقع.</summary>
    public void SetCurrency(string currencyCode, decimal exchangeRate)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3)
            throw new AccountingException("رمز العملة ثلاثةُ أحرفٍ بمعيار ISO 4217.");
        if (exchangeRate <= 0m)
            throw new AccountingException("سعرُ الصرف يجب أن يكون موجباً.");

        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        ExchangeRate = exchangeRate;
        RecalculateTotals();
    }

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
        string? taxExemptionReasonCode = null, string? taxExemptionReason = null,
        string? batchNumber = null)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف السطر مطلوب.");
        if (quantity <= 0 || unitPrice < 0)
            throw new AccountingException("الكمية يجب أن تكون موجبة والسعر لا يكون سالباً.");
        DocumentLineScale.EnsureUnitPriceHalala(unitPrice);
        _lines.Add(new SalesInvoiceLine(description.Trim(), quantity, unitPrice, productId, vatCategory, taxExemptionReasonCode?.Trim(), taxExemptionReason?.Trim(),
            // ‏(جاهزيّةُ الإطلاق · ح-١٠٥ · ‏`order-to-cash-procure-to-pay-inventory#7`) ‏**والدفعةُ المرتجَعةُ تُسمّى** — فيخرج المرتجعُ إلى المورّد منها وحدَها لا من أقدم ما استُلم.
            BatchNumber: string.IsNullOrWhiteSpace(batchNumber) ? null : batchNumber.Trim()));
        RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        (ForeignSubtotal, ForeignVatAmount) = LineDocumentTotalsCalculator.Compute(_lines);

        // ‏(ح-٣٩) **التحويلُ على الوعاء والضريبة كلٍّ على حدة** — كما في
        // ‏`PurchaseInvoice`. **وبسعرٍ واحدٍ يعود الرقمان إلى ما كانا حرفاً.**
        Subtotal = Math.Round(ForeignSubtotal * ExchangeRate, 2, MidpointRounding.AwayFromZero);
        VatAmount = Math.Round(ForeignVatAmount * ExchangeRate, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// (CEEF-234) <paramref name="vatDeductible"/> يجب أن يطابق شرط الفاتورة الأصلية
    /// (<see cref="PurchaseInvoice.VatDeductible"/>) — <b>لا قيمةً مستقلّة</b>.
    ///
    /// <para><b>والعطل الذي يمنعه:</b> فاتورةٌ من موردٍ غير مسجَّل لا تخصم ضريبتها أصلاً —
    /// <c>PurchaseInvoice.Post</c> يرسملها ضمن المشتريات ولا يُدين <c>1111</c> بشيء. فإشعارُ
    /// مدينٍ عليها كان **يُدائن <c>1111</c> دائماً** — يخفّض حساباً لم يُزَد فيه قط، فيفتح
    /// رصيداً وهمياً لا يقابله شيء، ويُخالف الإقرارَ الذي يستثني هذه الحالة أصلاً
    /// (<c>VatReturnQuery.DebitNotesDeductibleVat</c>).</para>
    /// </summary>
    public JournalEntry Post(string noteNumber, Account purchasesOrInventory, Account vatInput,
        Account payable, decimal inventoryReduction = 0m, Account? inventoryAccount = null,
        Account? purchasesExpense = null, bool vatDeductible = true)
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

        if (inventoryReduction > 0)
        {
            if (inventoryAccount is null)
                throw new AccountingException("حساب المخزون مطلوب لتخفيض أصناف مخزنية.");
            entry.AddLine(inventoryAccount, 0m, inventoryReduction, "تخفيض مخزون — إشعار مدين");
        }

        // غيرُ قابلةٍ للخصم: الضريبةُ رسمِلت في التكلفة أصلاً، فتخفيضها يتبع تخفيضَ التكلفة لا 1111
        var nonInv = (vatDeductible ? Subtotal : this.GrandTotal()) - inventoryReduction;
        if (nonInv > 0)
            entry.AddLine(purchasesExpense ?? purchasesOrInventory, 0m, nonInv, "تخفيض مشتريات/مصروف");

        if (vatDeductible && VatAmount > 0)
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
