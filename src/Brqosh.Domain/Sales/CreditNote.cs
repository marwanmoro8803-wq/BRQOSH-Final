using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Sales;

public enum CreditNoteStatus
{
    Draft = 0,
    Posted = 1,
    Cancelled = 2,
}

/// <summary>
/// إشعار دائن (مردودات ومسموحات المبيعات) — الأداة النظامية الوحيدة لتصحيح فاتورة مرحّلة.
/// القيد الآلي: مردودات المبيعات + ضريبة المخرجات (مدين) ← العملاء (دائن).
/// </summary>
public class CreditNote : IBranchScoped, ILineDocumentTotals
{
    private readonly List<SalesInvoiceLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long CustomerId { get; private set; }

    /// <summary>الفاتورة الأصلية محل التصحيح — إلزامية نظاماً (متطلب هيئة الزكاة).</summary>
    public long OriginalInvoiceId { get; private set; }

    public string? NoteNumber { get; private set; }
    public DateOnly NoteDate { get; private set; }
    public string Reason { get; private set; }
    public CreditNoteStatus Status { get; private set; } = CreditNoteStatus.Draft;
    public long? JournalEntryId { get; private set; }

    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();

    public decimal Subtotal { get; private set; }
    public decimal VatAmount { get; private set; }


    /// <summary>
    /// تاريخ الإلغاء (O-21، CEEF-219). الإقرار الضريبي يعكس **وقائع الفترة**:
    /// المستند الملغى يبقى في إقرار فترة إصداره كما قُدِّم للهيئة، ويظهر أثر إلغائه
    /// في فترة الإلغاء. بدون هذا التاريخ لا سبيل لوضع الأثر في فترته الصحيحة.
    /// </summary>
    /// <summary>
    /// (CEEF-229 · م2-٢٤) <b>لحظةُ الترحيل — لا تاريخُه وحده.</b>
    ///
    /// <para>رمزُ الطور الأوّل إلزاميّ نظاماً على كلّ مسجَّلٍ في ضريبة القيمة المضافة، ويحمل
    /// وسماً زمنياً. وكان المسارُ المطبوع يختلق له <b>ظهرَ يوم الإصدار</b> لأنّ المستند لا
    /// يملك لحظةً يعلنها. فالمشتري يمسح الرمزَ فيرى وقتاً ليس وقتَ شرائه، وكلُّ فواتير اليوم
    /// تحمل الطابعَ نفسه حرفاً بحرف.</para>
    ///
    /// <para>وهو <b>قابلٌ للعدم</b> عمداً: المستنداتُ المرحّلة قبل هذه الدفعة لا لحظةَ لها،
    /// وادّعاءُ لحظةٍ لها اختلاقٌ آخر. فتُقرأ فارغةً ويعود المسارُ إلى الظهر الحتميّ مُقِرّاً.</para>
    /// </summary>
    public DateTime? PostedAtUtc { get; private set; }

    public DateOnly? CancelledAt { get; private set; }

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

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٦) <b>خصمُ المستند على الإشعار — وكان لا يُمثَّل أصلاً.</b>
    ///
    /// <para>الفاتورةُ تحمل خصمَ مستندٍ وخصمَ سطر، <b>والإشعارُ الدائنُ كان يُبنى منها بلا
    /// أيٍّ منهما</b>. فمرتجعٌ جزئيٌّ <b>يُعيد للعميل أكثرَ ممّا دفع</b> ويُنقص ضريبةَ
    /// المخرجات أكثرَ ممّا استُحقّ، <b>ومرتجعٌ كاملٌ لفاتورةٍ مخصومةٍ يُرفض</b> لأنّ إجماليَّه
    /// يتجاوز المتبقّي منها.</para>
    ///
    /// <para><b>والحسابُ كان موجوداً كلَّه</b>: <c>SalesInvoiceLine</c> تحمل
    /// <c>DiscountPercent</c>، و<c>LineDocumentTotalsCalculator</c> يوزّع خصمَ المستند على
    /// الأوعية ويُسكِن الفلسَ الأخيرَ في الوعاء الأخير. <b>وإنّما كان الإشعارُ لا يُمرّر
    /// شيئاً منه.</b></para>
    /// </summary>
    public decimal DocumentDiscountAmount { get; private set; }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٦) <b>حصّةُ المُرتجَع من خصم الفاتورة</b> — بنسبة صافيه إلى صافيها.
    ///
    /// <para><b>وفي النطاق لا في الحوار</b>: هذا حسابُ مالٍ يُقاس، ولو سكن شاشةَ WPF لَما
    /// قِيس إلّا بتشغيل التطبيق — <b>وما لا يُقاس يبيت</b>. ويلزم كلَّ مسارٍ يُنشئ إشعاراً،
    /// لا شاشةَ سطح المكتب وحدَها.</para>
    ///
    /// <para><b>ومرتجعٌ كاملٌ يأخذ الخصمَ كلَّه بالضبط</b> — النسبةُ واحدٌ فلا يبقى فلسٌ
    /// عالق، <b>فيُقفل الإشعارُ الفاتورةَ تماماً</b> بدل أن يتجاوز متبقّيها فيُرفض.</para>
    ///
    /// <para><b>ولا يتجاوز صافيَ المُرتجَع</b>: التقريبُ لأعلى في مرتجعٍ شبهِ كاملٍ قد
    /// يتجاوزه بفلس، <b>و<see cref="ApplyDocumentDiscount"/> يرفض ذلك بحقّ</b>.</para>
    /// </summary>
    public static decimal DocumentDiscountShareFor(SalesInvoice invoice, decimal returnedNet)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (invoice.DocumentDiscountAmount <= 0m || returnedNet <= 0m)
            return 0m;

        var invoiceNet = invoice.Lines.Sum(l => l.LineTotal);
        if (invoiceNet <= 0m)
            return 0m;

        var share = Math.Round(
            invoice.DocumentDiscountAmount * returnedNet / invoiceNet, 2, MidpointRounding.AwayFromZero);

        return Math.Min(share, returnedNet);
    }

    /// <summary>
    /// يضبط خصمَ المستند على الإشعار — <b>ولا يتجاوز صافيَ سطوره</b>، فإشعارٌ بوعاءٍ سالبٍ
    /// فاتورةٌ لا إشعار.
    /// </summary>
    public void ApplyDocumentDiscount(decimal amount)
    {
        EnsureDraft();
        if (amount < 0m)
            throw new AccountingException("خصم المستند لا يكون سالباً.");

        var linesNet = _lines.Sum(l => l.LineTotal);
        if (amount > linesNet)
            throw new AccountingException(
                $"خصم المستند {amount:N2} يتجاوز صافي سطور الإشعار {linesNet:N2}.");

        DocumentDiscountAmount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        RecalculateTotals();
    }

    public void AddLine(string description, decimal quantity, decimal unitPrice, long? productId = null,
        Tax.VatCategory vatCategory = Tax.VatCategory.Standard,
        string? taxExemptionReasonCode = null, string? taxExemptionReason = null,
        decimal discountPercent = 0m)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف السطر مطلوب.");
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

        if (discountPercent is < 0m or > 100m)
            throw new AccountingException("نسبةُ خصم السطر بين صفرٍ ومئة.");

        _lines.Add(new SalesInvoiceLine(description.Trim(), quantity, unitPrice, productId, vatCategory,
            taxExemptionReasonCode?.Trim(), taxExemptionReason?.Trim(), DiscountPercent: discountPercent));
        RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        // ‏(ح-٦) ‏**وخصمُ المستند يدخل الحساب** — وكان يُستدعى بلا وسيطٍ فيُحسب الإشعارُ
        // ‏على السطور الخامّة. والوسيطُ نفسُه تُمرّره `SalesInvoice`، والموزِّعُ واحد.
        (Subtotal, VatAmount) = LineDocumentTotalsCalculator.Compute(
            _lines, documentDiscount: DocumentDiscountAmount);
    }

    /// <summary>
    /// الترحيل: مدين مردودات المبيعات، مدين ضريبة المخرجات (تخفيض)، دائن العملاء.
    /// مع أصناف مخزنية: دائن COGS / مدين مخزون (عكس التكلفة).
    /// </summary>
    public JournalEntry Post(string noteNumber, Account salesReturnsAccount, Account vatOutputAccount,
        Account receivableAccount, decimal cogsReversal = 0m, Account? cogsAccount = null, Account? inventoryAccount = null)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new AccountingException("إشعار الدائن بلا سطور.");
        if (Subtotal <= 0)
            throw new AccountingException("قيمة الإشعار يجب أن تكون أكبر من صفر.");
        if (string.IsNullOrWhiteSpace(noteNumber))
            throw new AccountingException("رقم الإشعار مطلوب عند الترحيل.");

        RecalculateTotals();

        var entry = JournalEntry.CreateDraft(
            CompanyId, NoteDate,
            $"إشعار دائن رقم {noteNumber} — {Reason}",
            sourceDocument: $"CreditNote:{noteNumber}");

        entry.AddLine(salesReturnsAccount, Subtotal, 0m, "مردودات ومسموحات المبيعات");
        if (VatAmount > 0)
            entry.AddLine(vatOutputAccount, VatAmount, 0m, "تخفيض ضريبة المخرجات 15%");
        entry.AddLine(receivableAccount, 0m, this.GrandTotal(), "تخفيض ذمة العميل");

        if (cogsReversal > 0)
        {
            if (cogsAccount is null || inventoryAccount is null)
                throw new AccountingException("حسابات COGS والمخزون مطلوبة لإرجاع الأصناف.");
            entry.AddLine(inventoryAccount, cogsReversal, 0m, "إرجاع للمخزون");
            entry.AddLine(cogsAccount, 0m, cogsReversal, "عكس تكلفة المبيعات");
        }

        NoteNumber = noteNumber.Trim();
        Status = CreditNoteStatus.Posted;
        PostedAtUtc = DateTime.UtcNow;
        return entry;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void MarkCancelled(DateOnly cancelledAt)
    {
        if (Status != CreditNoteStatus.Posted)
            throw new AccountingException("لا يُلغى إلا إشعار دائن مرحّل.");
        Status = CreditNoteStatus.Cancelled;
        CancelledAt = cancelledAt;
    }

    private void EnsureDraft()
    {
        if (Status != CreditNoteStatus.Draft)
            throw new AccountingException("إشعار الدائن مرحّل — لا يُعدَّل.");
    }
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        EnsureDraft();
        BranchId = branchId;
    }

}
