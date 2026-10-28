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

    /// <summary>حصّةُ البطاقة في الدفع المختلط — والباقي من الإجمالي نقد. فارغةٌ لغير المختلط.</summary>
    public decimal? PosCardAmount { get; private set; }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١١٠ · <c>order-to-cash-procure-to-pay-inventory#12</c>) <b>المستودعُ الذي يُصرف منه البيع.</b> وفارغُه هو
    /// المستودعُ الافتراضيّ كما كانت كلُّ فاتورةٍ قبله؛ <b>والاستلامُ والجردُ والتحويلُ تختار أيَّ مستودع</b>، فالبيعُ وحدَه كان
    /// حبيسَ الافتراضيّ — ومنشأةٌ متعدّدةُ الفروع لا تبيع من مخزن فرعها الثاني.
    /// </summary>
    public long? WarehouseId { get; private set; }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · <c>sector-modules#6</c>) <b>الحجزُ الذي تُصفّيه هذه الفاتورة</b> — فاتورةُ المغادرة. يُضبط <b>قبل</b>
    /// الترحيل لأنّ المستندَ الإلكترونيَّ يُولَّد داخله، ومنه يجد المولّدُ فواتيرَ الدفعات المقدّمة (386) فيُسوّيها فيها.
    /// </summary>
    public long? SettlesReservationId { get; private set; }

    /// <summary>يربط فاتورةَ المغادرة بحجزها قبل الترحيل — مرّةً، والمسودّةُ وحدَها.</summary>
    public void MarkAsSettlementOf(long reservationId)
    {
        if (Status != SalesInvoiceStatus.Draft)
            throw new AccountingException("لا يُربط حجزٌ بفاتورةٍ بعد ترحيلها.");
        SettlesReservationId = reservationId;
    }

    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();

    /// <summary>الإجمالي قبل الضريبة — مُخزَّن ويُحدَّث مع السطور.</summary>
    /// <summary>
    /// (CEEF-230 · ط1-٤) عملةُ المستند — و<c>SAR</c> هي الأصل.
    ///
    /// <para><b>ولا مستندَ في هذا النظام كان يقبل عملةً أجنبية</b>: أربعةُ أعمدةِ عملةٍ
    /// في القاعدة كلِّها، وكلُّها على المنشأة والمجموعة والحساب البنكيّ — <b>ولا واحدٌ
    /// على مستند</b>.</para>
    /// </summary>
    public string CurrencyCode { get; private set; } = "SAR";

    /// <summary>
    /// سعرُ الصرف إلى عملة الدفاتر <b>يوم المستند</b> — ويُثبَّت عليه.
    ///
    /// <para><b>ولا يُعاد حسابُه لاحقاً</b>: الذمّةُ نشأت بقيمةٍ بعينها، وتغييرُ السعر
    /// بأثرٍ رجعيّ يُعيد كتابة إيرادٍ مُقرٍّ عنه للهيئة.</para>
    /// </summary>
    public decimal ExchangeRate { get; private set; } = 1m;

    /// <summary>الوعاءُ <b>بعملة المستند</b> — وهو ما يراه العميل وما يخرج في الفوترة.</summary>
    public decimal ForeignSubtotal { get; private set; }

    /// <summary>الضريبةُ بعملة المستند.</summary>
    public decimal ForeignVatAmount { get; private set; }

    /// <summary>
    /// الوعاءُ <b>بعملة الدفاتر</b> — وهو ما يُرحَّل ويُقاس بالهويّات.
    ///
    /// <para><b>ولذلك بقي اسمُه كما هو</b>: كلُّ ما بُني على هذا الحقل — الأستاذُ والذممُ
    /// والهويّاتُ والتقاريرُ — يبقى صحيحاً بلا تعديلٍ واحد. <b>وبسعر صرفٍ واحدٍ يساوي
    /// الأجنبيَّ حرفاً</b>، فلا يتغيّر رقمٌ في قاعدةٍ قائمة.</para>
    /// </summary>
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
        string? batchNumber = null, decimal discountPercent = 0m, string? unitCode = null)
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
            Math.Round(discountPercent, 4, MidpointRounding.AwayFromZero),
            string.IsNullOrWhiteSpace(unitCode) ? null : unitCode.Trim()));
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

    /// <summary>
    /// (CEEF-230 · ط1-٤) يضبط عملةَ المستند وسعرَ صرفها.
    ///
    /// <para><b>ويُرفض بعد الترحيل</b>: تغييرُ العملة أو السعر يُعيد كتابة قيدٍ مرحَّل
    /// وإقرارٍ سُلّم.</para>
    /// </summary>
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
        RecalculateTotals();
    }

    /// <summary>
    /// (دفاترٌ بغير الريال) مستندٌ بسعر صرفٍ <c>1</c> هو بعملة الدفاتر نفسِها، فيُوسَم بها: المستندُ يُنشأ بـ<c>SAR</c> أصلاً،
    /// وفي منشأةٍ دفاترُها بغيره يُصحَّح الوسمُ قبل الحفظ (<c>BooksCurrencyInterceptor</c>). لا يمسّ مستنداً بعملةٍ أجنبيّةٍ صريحة.
    /// </summary>
    public void AlignToBooksCurrency(string booksCurrencyCode)
    {
        if (ExchangeRate == 1m && CurrencyCode == "SAR" && !string.IsNullOrWhiteSpace(booksCurrencyCode))
            CurrencyCode = booksCurrencyCode.Trim().ToUpperInvariant();
    }

    public void RecalculateTotals()
    {
        (ForeignSubtotal, ForeignVatAmount) = LineDocumentTotalsCalculator.Compute(
            _lines, documentDiscount: DocumentDiscountAmount);

        // ‏**التحويلُ على الوعاء والضريبة كلٍّ على حدة** لا على الإجمالي: الهيئةُ تقرأ
        // ‏الضريبةَ بعملة الدفاتر رقماً مستقلّاً، وتقريبُ الإجمالي ثمّ اشتقاقُها منه
        // ‏يُنتج فلساً لا يُطابق. وبسعرٍ واحدٍ يعود الرقمان إلى ما كانا حرفاً.
        Subtotal = Math.Round(ForeignSubtotal * ExchangeRate, 2, MidpointRounding.AwayFromZero);
        VatAmount = Math.Round(ForeignVatAmount * ExchangeRate, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>الإجماليُّ بعملة المستند — وهو ما يُطالَب به العميل.</summary>
    public decimal ForeignGrandTotal => ForeignSubtotal + ForeignVatAmount;

    /// <summary>عملةُ المستند تخالف عملةَ الدفاتر — فيلزم المستندَ إفصاحٌ إضافيّ.</summary>
    public bool IsForeignCurrency => ExchangeRate != 1m;

    /// <summary>
    /// ترحيل الفاتورة وتوليد قيدها: مدين العميل بالإجمالي، دائن الإيراد بالوعاء، دائن ضريبة المخرجات.
    /// إذا وُجدت أصناف مخزنية: يُضاف قيد تكلفة المبيعات (مدين COGS / دائن مخزون).
    /// </summary>
    public JournalEntry Post(string invoiceNumber, Account receivableAccount, Account revenueAccount,
        Account vatOutputAccount, decimal cogsAmount = 0m, Account? cogsAccount = null, Account? inventoryAccount = null,
        IReadOnlyList<(Account Account, decimal Amount)>? revenueSplit = null,
        IReadOnlyList<(Account Cogs, Account Inventory, decimal Amount)>? cogsSplit = null,
        (Account Account, decimal Amount)? secondDebit = null)
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new AccountingException("الفاتورة بلا سطور — أضف صنفاً واحداً على الأقل.");
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new AccountingException("رقم الفاتورة مطلوب عند الترحيل.");

        // ‏الفحصُ بعد إعادة الحساب — لا على إجماليٍّ قديمٍ قد لا يطابق السطور.
        RecalculateTotals();
        if (Subtotal <= 0)
            throw new AccountingException("إجمالي الفاتورة يجب أن يكون أكبر من صفر.");

        var entry = JournalEntry.CreateDraft(
            CompanyId, InvoiceDate,
            $"فاتورة مبيعات رقم {invoiceNumber}",
            sourceDocument: $"SalesInvoice:{invoiceNumber}");

        if (secondDebit is { } split)
        {
            // ‏الدفع المختلط: حصّةُ البطاقة لحسابها، والباقي للحساب الأوّل (النقد).
            if (split.Amount <= 0m || split.Amount >= this.GrandTotal())
                throw new AccountingException("مبلغ البطاقة في الدفع المختلط يجب أن يكون أكبر من صفر وأقل من إجمالي الفاتورة.");
            entry.AddLine(receivableAccount, this.GrandTotal() - split.Amount, 0m, "حصّة النقد من الدفع المختلط");
            entry.AddLine(split.Account, split.Amount, 0m, "حصّة البطاقة من الدفع المختلط");
        }
        else
            entry.AddLine(receivableAccount, this.GrandTotal(), 0m, "إجمالي الفاتورة شامل الضريبة");
        // ‏حساباتُ الصنف/الفئة: الإيرادُ موزَّعٌ على حساباته، ومجموعُه الإيرادُ نفسُه.
        if (revenueSplit is { Count: > 0 })
        {
            if (revenueSplit.Sum(r => r.Amount) != Subtotal)
                throw new AccountingException("توزيع الإيراد على حسابات الأصناف لا يساوي إجمالي الفاتورة.");
            foreach (var (acc, amount) in revenueSplit.Where(r => r.Amount != 0m))
                entry.AddLine(acc, 0m, amount, "إيراد المبيعات");
        }
        else
            entry.AddLine(revenueAccount, 0m, Subtotal, "إيراد المبيعات");
        if (VatAmount > 0)
            entry.AddLine(vatOutputAccount, 0m, VatAmount, Tax.VatLineLabels.Output(SaudiVatRate));

        if (cogsAmount > 0 && cogsSplit is { Count: > 0 })
        {
            if (cogsSplit.Sum(c => c.Amount) != cogsAmount)
                throw new AccountingException("توزيع تكلفة المبيعات على حسابات الأصناف لا يساوي التكلفة.");
            foreach (var (cogsAcc, invAcc, amount) in cogsSplit.Where(c => c.Amount > 0m))
            {
                entry.AddLine(cogsAcc, amount, 0m, "تكلفة البضاعة المباعة");
                entry.AddLine(invAcc, 0m, amount, "صرف من المخزون");
            }
        }
        else if (cogsAmount > 0)
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

    /// <summary>
    /// (ح-١١٠) يضبط مستودعَ الصرف — <b>والفارغُ هو الافتراضيّ</b>. <b>ولا يُغيَّر بعد الترحيل</b>: حركةُ الصرف مرحَّلةٌ في مستودعها،
    /// والإلغاءُ والمرتجعُ يرجعان إليه. <b>والمستودعُ نفسُه يُسأل عند الترحيل</b> (في المنشأة وغيرُ موقوف).
    /// </summary>
    public void SetWarehouse(long? warehouseId)
    {
        if (warehouseId is <= 0)
            throw new AccountingException("المستودع غير صالح.");
        EnsureDraft();
        WarehouseId = warehouseId;
    }

    public void MarkPosSale(long shiftId, PosPaymentMethod method, decimal? cardAmount = null)
    {
        if (method == Retail.PosPaymentMethod.Mixed)
        {
            if (cardAmount is not > 0m)
                throw new AccountingException("الدفع المختلط يتطلب مبلغ البطاقة (أكبر من صفر).");
            PosCardAmount = Math.Round(cardAmount.Value, 2, MidpointRounding.AwayFromZero);
        }
        else
            PosCardAmount = null;
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
    decimal DiscountPercent = 0m,

    /// <summary>
    /// (CEEF-230 · ط1-١) وحدةُ السطر — <b>و<c>null</c> تعني وحدةَ الصنف</b>، وهو الأصل
    /// وسلوكُ كلّ ما مضى. فلا يتغيّر رقمٌ في قاعدةٍ قائمة.
    ///
    /// <para><b>ولا تُتاح إلا حيث تُحوَّل:</b> `AddLine` في فاتورة المبيعات وإذنِ الاستلام
    /// وحدَهما يقبلان الوحدة. وبقيّةُ المستندات الستّة التي تشترك في هذا السجلّ لا تُمرّرها
    /// — <b>فالمُصرِّفُ يضمن ألّا توجد وحدةٌ في مسارٍ لا يُحوّلها</b>، بلا حارسٍ يُنسى.</para>
    /// </summary>
    string? UnitCode = null,

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١٠٨ · ‏`order-to-cash-procure-to-pay-inventory#7`) ‏**انتهاءُ الدفعة المشتراة** —
    /// ‏لفاتورة الشراء المباشرة وحدَها (تُهمَل في بقيّة المستندات التي تشترك في هذا السجلّ ولا عمودَ لها).
    /// </summary>
    DateOnly? ExpiryDate = null)
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

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-١٠٨) ‏**منشئٌ بلا الانتهاء لـEF وحدَه** — العمودُ مُهمَلٌ في ثمانية جداولَ من التسعة،
    /// ‏فلا يجد EF خاصيّةً يربط بها وسيطَ المنشئ الأوّل؛ وهذا يُطابق خصائصَه المخزَّنة كلَّها.
    /// </summary>
    public SalesInvoiceLine(string Description, decimal Quantity, decimal UnitPrice, long? ProductId,
        Tax.VatCategory VatCategory, string? TaxExemptionReasonCode, string? TaxExemptionReason,
        string? BatchNumber, decimal DiscountPercent, string? UnitCode)
        : this(Description, Quantity, UnitPrice, ProductId, VatCategory, TaxExemptionReasonCode,
            TaxExemptionReason, BatchNumber, DiscountPercent, UnitCode, null)
    {
    }
}
