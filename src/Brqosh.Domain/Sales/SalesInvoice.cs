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

    /// <summary>
    /// (CEEF-230 · ط1-٣) ‏و<paramref name="batchNumber"/> الدفعةُ التي تُصرَف من المستودع.
    ///
    /// <para><b>ويلزم للصنف المتتبَّع</b> — ويرفضه المَخنَقُ إن غاب. أمّا الصنفُ غيرُ
    /// المتتبَّع فيرفضه إن حضر: <b>قيمةٌ تُدخَل ولا تُخزَّن أسوأُ من قيمةٍ لا تُدخَل</b>.</para>
    /// </summary>
    public void AddLine(string description, decimal quantity, decimal unitPrice, long? productId = null,
        Tax.VatCategory vatCategory = Tax.VatCategory.Standard,
        string? taxExemptionReasonCode = null, string? taxExemptionReason = null,
        string? batchNumber = null, decimal discountPercent = 0m)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("وصف الصنف مطلوب في سطر الفاتورة.");
        if (quantity <= 0)
            throw new AccountingException("الكمية يجب أن تكون أكبر من صفر.");
        if (unitPrice < 0)
            throw new AccountingException("سعر الوحدة لا يكون سالباً.");
        DocumentLineScale.EnsureUnitPriceHalala(unitPrice);

        // (CEEF-230 · ط1-٢) ‏الخصمُ نسبةٌ بين صفرٍ ومئة. **والمئةُ مقبولة** — عيّنةٌ
        // ‏مجّانيةٌ سطرُها بخصمٍ كامل، وهي ممارسةٌ قائمةٌ لا حالةٌ شاذّة. وما فوقها
        // ‏يُنتج سطراً سالباً يقلب الوعاءَ الضريبيّ.
        if (discountPercent < 0m || discountPercent > 100m)
            throw new AccountingException(
                $"نسبة خصم السطر «{description}» يجب أن تكون بين 0 و100 — ووردت {discountPercent:N2}.");


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

        _lines.Add(new SalesInvoiceLine(description.Trim(), quantity, unitPrice, productId, vatCategory,
            taxExemptionReasonCode?.Trim(), taxExemptionReason?.Trim(),
            string.IsNullOrWhiteSpace(batchNumber) ? null : batchNumber.Trim(),
            Math.Round(discountPercent, 4, MidpointRounding.AwayFromZero)));
        RecalculateTotals();
    }

    /// <summary>
    /// (CEEF-230 · ط1-٢) <b>خصمُ المستند — يُوزَّع على الأوعية الضريبية لا يُطرح من الإجمالي.</b>
    ///
    /// <para>فاتورةٌ فيها سطرٌ أساسيّ (15%) وسطرٌ صفريّ (0%)، وخصمٌ على المستند كلِّه:
    /// <b>طرحُه من الإجمالي يترك الوعاءَ الأساسيَّ كما هو</b> فتُحتسب الضريبةُ على مبلغٍ
    /// أكبرَ ممّا دُفع. والصوابُ توزيعُه على الوعاءَين بنسبتيهما.</para>
    /// </summary>
    public decimal DocumentDiscountAmount { get; private set; }

    /// <summary>
    /// يضبط خصمَ المستند.
    ///
    /// <para><b>ولا يتجاوز صافيَ السطور</b>: خصمٌ أكبرُ منها يُنتج وعاءً سالباً — أي
    /// فاتورةً تُعيد مالاً، وتلك <b>إشعارُ دائنٍ لا فاتورة</b>.</para>
    /// </summary>
    public void ApplyDocumentDiscount(decimal amount)
    {
        EnsureDraft();
        if (amount < 0m)
            throw new AccountingException("خصم المستند لا يكون سالباً.");

        var linesNet = _lines.Sum(l => l.LineTotal);
        if (amount > linesNet)
            throw new AccountingException(
                $"خصم المستند {amount:N2} يتجاوز صافي السطور {linesNet:N2} — "
                + "وفاتورةٌ بوعاءٍ سالبٍ إشعارُ دائنٍ لا فاتورة.");

        // ‏**ويبقى الشرطُ صحيحاً بعد أيّ إضافة**: السطورُ تُضاف ولا تُحذف — لا
        // ‏`RemoveLine` في هذا الكيان — فصافيها لا ينقص، والخصمُ المقبول اليوم مقبولٌ غداً.
        // ‏ولو أُضيف حذفُ سطرٍ يوماً **لزم إعادةُ هذا الفحص هناك**.
        DocumentDiscountAmount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        RecalculateTotals();
    }

    public void RecalculateTotals()
    {
        (Subtotal, VatAmount) = LineDocumentTotalsCalculator.Compute(
            _lines, documentDiscount: DocumentDiscountAmount);
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
        PostedAtUtc = DateTime.UtcNow;
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
    Tax.VatCategory VatCategory = Tax.VatCategory.Standard,
    string? TaxExemptionReasonCode = null,
    string? TaxExemptionReason = null,

    /// <summary>(CEEF-230 · ط1-٣) الدفعةُ المصروفة — هويّةُ ما خرج، ويلزم للصنف المتتبَّع.</summary>
    string? BatchNumber = null,

    /// <summary>
    /// (CEEF-230 · ط1-٢) نسبةُ خصم السطر — <b>نسبةً لا مبلغاً</b>.
    ///
    /// <para>لأنّ النسبةَ هي ما يُتَّفق عليه تجارياً («خصمُ عشرة بالمئة»)، والمبلغُ مشتقٌّ
    /// منها. وتخزينُ الاثنين يفتح بابَ تناقضهما بعد تعديل الكمّية — <b>وقيمتان تصفان
    /// شيئاً واحداً تفترقان يوماً</b>.</para>
    /// </summary>
    decimal DiscountPercent = 0m)
{
    /// <summary>القيمةُ قبل الخصم — وهي ما يُعرض للعميل سعراً معلَناً.</summary>
    public decimal GrossAmount => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

    /// <summary>مبلغُ خصم السطر — مُقرَّبٌ على السطر، فهو ما يظهر في الفاتورة.</summary>
    public decimal DiscountAmount =>
        Math.Round(GrossAmount * DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// صافي السطر — <b>وهو الوعاءُ الضريبيّ</b>.
    ///
    /// <para>(CEEF-230 · ط1-٢) صار بعد الخصم. <b>والضريبةُ على الصافي نظاماً</b>: احتسابُها
    /// على الإجمالي يُحمّل العميلَ ضريبةً على مبلغٍ لم يدفعه، ويجعل المُصرَّحَ به للهيئة
    /// أكبرَ من الواجب.</para>
    /// </summary>
    public decimal LineTotal => GrossAmount - DiscountAmount;
}
