using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Hospitality;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>
/// إقرار ضريبة القيمة المضافة عن فترة: المبيعات وضريبة المخرجات،
/// المشتريات وضريبة المدخلات القابلة للخصم، وصافي الضريبة (مستحقة أو مستردة)،
/// ومطابقة تقريبية مع رصيد حساب 2107 (إن وُجدت حركة).
///
/// (O-21، CEEF-219) الإقرار يعكس **وقائع الفترة** لا حالة المستند اليوم.
/// كان يُرشّح على Status == Posted فقط: فاتورة صدرت في الربع الأول وأُلغيت في الثاني
/// تصبح Cancelled فتختفي من إعادة تشغيل إقرار الربع الأول — بأرقام أقلّ مما قُدِّم
/// للهيئة فعلاً، ومخالفة لقيدها الباقي في الأستاذ لتلك الفترة. وهو خطر فروقات
/// عند الفحص الضريبي.
///
/// الصواب محاسبياً: إقرار فترة الإصدار يبقى كما قُدِّم، وأثر الإلغاء يظهر في فترة
/// الإلغاء كتسوية سالبة — تماماً كما يفعل إشعار الدائن.
/// </summary>
/// <summary>
/// (CEEF-229 · A-15) وعاءٌ ضريبيّ واحد في الإقرار: فئةٌ ومبلغٌ خاضع وضريبة.
/// </summary>
public record VatReturnBucket(Brqosh.Domain.Tax.VatCategory Category, decimal TaxableAmount, decimal VatAmount);

public record VatReturnResult(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal SalesSubtotal,
    decimal OutputVat,
    decimal ProgressBillingsSubtotal,
    decimal ProgressBillingsVat,
    decimal CreditNotesSubtotal,
    decimal CreditNotesVat,
    decimal PurchasesSubtotal,
    decimal DeductibleInputVat,
    decimal DebitNotesSubtotal,
    decimal DebitNotesVat,
    decimal DebitNotesDeductibleVat,
    // (جاهزيّةُ الإطلاق · ح-٤١) ‏**الأبُ الذي تُعرض الأوعيةُ تحته** — صافي الوعاء من إجماليّات
    // ‏المستندات المخزونة، **لا من الأوعية**. إلزاميٌّ بلا افتراضيّ كحدود ح-٤٠.
    decimal SalesBaseNet,
    decimal PurchasesBaseNet,
    decimal? GlVatNetPayableBalance = null,
    // تسويات إلغاء مستندات صدرت في فترات سابقة وأُلغيت داخل هذه الفترة (CEEF-219).
    decimal CancelledOutputVat = 0m,
    decimal CancelledInputVat = 0m,
    IReadOnlyList<VatReturnBucket>? SalesByCategory = null,
    IReadOnlyList<VatReturnBucket>? PurchasesByCategory = null,
    // (CEEF-232 · ض-١) ضريبةٌ قُيّدت في الأستاذ بقيدٍ ليس مستنداً محسوباً ولا تسوية.
    decimal JournalOutputVat = 0m,
    decimal JournalInputVat = 0m)
{
    /// <summary>
    /// (CEEF-229 · A-15) المبيعات مفكَّكة حسب الفئة الضريبية — <b>صافيةً</b> من الإشعارات الدائنة.
    ///
    /// <para><b>لماذا:</b> نموذج الإقرار السعوديّ يفصل المبيعات الخاضعة بالنسبة الأساسية عن
    /// الصادرات الصفرية عن المبيعات المعفاة. وكان الإقرار يُخرج مجاميع كلّية فقط، رغم أن الفئة
    /// <b>موجودة على مستوى السطر</b> ويحسبها <c>LineDocumentTotalsCalculator.ComputeSubtotals</c>
    /// أصلاً في مولّد ZATCA — فالبيانات متوفّرة والتقرير يهدرها.</para>
    ///
    /// <para><b>الأثر بالأرقام:</b> ربعٌ فيه مبيعات محلية 1,000,000 (ضريبة 150,000) وصادرات صفرية
    /// 400,000 وإيجار معفى 100,000. المجموع الكلّي يقول وعاءً 1,500,000 مقابل ضريبة 150,000 — أي
    /// <b>10%</b>، نسبةٌ لا وجود لها في أيّ نظام ضريبيّ، فيقع تعارضٌ آليّ فوريّ على بوابة الهيئة.
    /// وتختفي 500,000 ريال من الإفصاح.</para>
    ///
    /// <para><b>حدٌّ مُصرَّح به:</b> التفكيك بالفئة <b>فقط</b> — ولا تُسنَد أرقام صناديق النموذج
    /// الرسميّ هنا، فأرقامها تحتاج مراجعة النموذج نصّاً (م4-٤). إسنادُ رقم صندوقٍ بالتخمين أسوأ
    /// من عدم إسناده.</para>
    ///
    /// <para>(جاهزيّةُ الإطلاق · ح-٤١) <b>وصارت بالريال، بعد خصم المستند، وتشمل كلَّ ما في
    /// الصافي من المستندات</b> — الفواتيرَ والمستخلصاتِ ناقصاً الإشعاراتِ وتسويةَ إلغاء الفترات
    /// السابقة. <b>وهويّتُها</b>: ضريبتُها + <see cref="JournalOutputVat"/> == <see cref="NetOutputVat"/>،
    /// ووعاؤها == <see cref="SalesBaseNet"/>.</para>
    /// </summary>
    public IReadOnlyList<VatReturnBucket> Sales => SalesByCategory ?? [];

    /// <summary>المشتريات مفكَّكة حسب الفئة — صافيةً من الإشعارات المدينة. انظر <see cref="Sales"/>.</summary>
    public IReadOnlyList<VatReturnBucket> Purchases => PurchasesByCategory ?? [];
    /// <summary>
    /// (CEEF-232 · ض-١) <b>ضريبةُ المخرجات — من المستندات <u>ومن الأستاذ معاً</u>.</b>
    ///
    /// <para><b>وكان الإقرارُ يقرأ المستنداتِ وحدَها.</b> فضريبةٌ قُيّدت على <c>2106</c>
    /// بقيدٍ لا يحمل أحدَ المستندات الخمسة <b>لا تبلغ الإقرار أبداً</b> — لا في فترتها
    /// ولا في غيرها. <b>وبيعُ الأصل الثابت توريدٌ خاضعٌ نظاماً وترحيلُه قيدٌ لا فاتورة.</b>
    /// فيُقدَّم الإقرارُ ناقصاً بضريبةٍ حُصّلت فعلاً، وهي حالةُ المطالبة بالضريبة والغرامة
    /// معاً عند الفحص.</para>
    ///
    /// <para>(جاهزيّةُ الإطلاق · ح-٤٠) <b>والمعادلةُ في <c>VatReturnNet</c> لا هنا</b> —
    /// مالكٌ واحدٌ للمطبوعة والشاشة، وكانت الشاشةُ تشتقّها بنفسها فتُسقط حدّين.</para>
    /// </summary>
    public decimal NetOutputVat => Brqosh.Application.Reporting.VatReturnNet.Output(
        OutputVat, ProgressBillingsVat, CreditNotesVat, CancelledOutputVat, JournalOutputVat);

    /// <summary>
    /// (CEEF-232 · ض-١) والمدخلاتُ كذلك — <b>والاتجاهُ هنا في صالح المنشأة</b>: عماها عنه
    /// يجعلها تدفع ضريبةً خصمُها حقٌّ لها، <b>وهو عطلٌ لا يشتكي منه أحدٌ ويُكلّف مالاً كلَّ ربع</b>.
    /// </summary>
    public decimal NetDeductibleInputVat => Brqosh.Application.Reporting.VatReturnNet.Input(
        DeductibleInputVat, DebitNotesDeductibleVat, CancelledInputVat, JournalInputVat);
    public decimal NetVatPayable => NetOutputVat - NetDeductibleInputVat;

    /// <summary>
    /// (CEEF-229 · م2-١٦) <b>غيابُ البيان ليس صفراً.</b>
    ///
    /// <para>كان الرصيد <c>decimal</c> يسقط إلى صفر حين لا يوجد الحساب أو لا حركة عليه،
    /// فيصير الفرقُ مساوياً <b>لصافي الإقرار كاملاً دائماً</b>. ولا كاتب لحساب «صافي ضريبة
    /// القيمة المضافة المستحقة» (2107) في المستودع كلّه — لا خدمة تسويةٍ ضريبية تُقفل
    /// المدخلات والمخرجات فيه — فالرصيد صفرٌ عند كل منشأة، والمؤشّر يقول أبداً إن الإقرار
    /// كلَّه لا يطابق الأستاذ.</para>
    ///
    /// <para>المؤشّر لا يُعرض اليوم في شاشةٍ ولا PDF، لكنه في عقد التطبيق — فأيّ شاشةٍ
    /// تُضاف غداً ستعرض تحذيراً يصرخ دائماً، <b>وتحذيرٌ يصرخ دائماً أسوأ من غيابه</b>: يتعلّم
    /// المستخدم تجاهله فيفوته التحذير الحقيقيّ.</para>
    ///
    /// <para>فصار <c>null</c> يعني «لا مطابقة ممكنة — لا حركة على الحساب»، وهو غير الصفر
    /// الذي يعني «طابق تماماً». والتسوية الضريبية نفسها بندٌ مؤجَّل مسجَّل في الخطة.</para>
    /// </summary>
    public decimal? GlVariance =>
        GlVatNetPayableBalance is decimal gl ? NetVatPayable - gl : null;

    /// <summary>هل يمكن مطابقة الإقرار بالأستاذ أصلاً؟</summary>
    public bool IsGlReconciliationAvailable => GlVatNetPayableBalance is not null;
}

public class VatReturnQuery(BrqoshDbContext db)
{
    public async Task<VatReturnResult> ExecuteAsync(long companyId, DateOnly from, DateOnly to)
    {
        var sales = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && (i.Status == SalesInvoiceStatus.Posted
                            || (i.Status == SalesInvoiceStatus.Cancelled && i.CancelledAt > to))
                        && i.InvoiceDate >= from && i.InvoiceDate <= to)
            .ToListAsync();

        var progressBillings = await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && (b.Status == ProgressBillingStatus.Posted
                            || (b.Status == ProgressBillingStatus.Cancelled && b.CancelledAt > to))
                        && b.BillingDate >= from && b.BillingDate <= to)
            .ToListAsync();

        var creditNotes = await db.CreditNotes
            .Where(n => n.CompanyId == companyId && (n.Status == CreditNoteStatus.Posted
                            || (n.Status == CreditNoteStatus.Cancelled && n.CancelledAt > to))
                        && n.NoteDate >= from && n.NoteDate <= to)
            .ToListAsync();

        var purchases = await db.PurchaseInvoices
            .Where(p => p.CompanyId == companyId && (p.Status == PurchaseInvoiceStatus.Posted
                            || (p.Status == PurchaseInvoiceStatus.Cancelled && p.CancelledAt > to))
                        && p.InvoiceDate >= from && p.InvoiceDate <= to)
            .ToListAsync();

        var debitNotes = await db.DebitNotes
            .Where(n => n.CompanyId == companyId && (n.Status == DebitNoteStatus.Posted
                            || (n.Status == DebitNoteStatus.Cancelled && n.CancelledAt > to))
                        && n.NoteDate >= from && n.NoteDate <= to)
            .ToListAsync();

        var deductiblePurchaseIds = await db.PurchaseInvoices
            .Where(p => p.CompanyId == companyId && p.VatDeductible)
            .Select(p => p.Id)
            .ToHashSetAsync();

        // (CEEF-219) الشقّ الثاني: مستندات صدرت **قبل** هذه الفترة وأُلغيت **داخلها**.
        // أثر الإلغاء ينتمي لفترة الإلغاء — كما لو صدر إشعار دائن. بدون هذا الشقّ
        // تختفي الضريبة من كل الفترات: لا من فترة الإصدار (بقيت كما قُدِّمت) ولا من غيرها.
        //
        // `VatAmount` خاصية محسوبة غير مُعيَّنة في المخطّط، فلا تُترجَم إلى SQL —
        // نُحضر الصفوف ثمّ نجمع في الذاكرة، كما تفعل الاستعلامات أعلاه.
        var cancelledSales = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.Status == SalesInvoiceStatus.Cancelled
                        && i.CancelledAt >= from && i.CancelledAt <= to && i.InvoiceDate < from)
            .ToListAsync();

        var cancelledProgressBillings = await db.ProgressBillings
            .Where(b => b.CompanyId == companyId && b.Status == ProgressBillingStatus.Cancelled
                        && b.CancelledAt >= from && b.CancelledAt <= to && b.BillingDate < from)
            .ToListAsync();

        var cancelledCreditNotes = await db.CreditNotes
            .Where(n => n.CompanyId == companyId && n.Status == CreditNoteStatus.Cancelled
                        && n.CancelledAt >= from && n.CancelledAt <= to && n.NoteDate < from)
            .ToListAsync();

        var cancelledPurchases = await db.PurchaseInvoices
            .Where(p => p.CompanyId == companyId && p.Status == PurchaseInvoiceStatus.Cancelled
                        && p.CancelledAt >= from && p.CancelledAt <= to && p.InvoiceDate < from
                        && p.VatDeductible)
            .ToListAsync();

        var cancelledOutputVat =
            cancelledSales.Sum(i => i.VatAmount)
          + cancelledProgressBillings.Sum(b => b.VatAmount)
          // إلغاء إشعار دائن يعكس خصماً سابقاً — فيزيد ضريبة المخرجات لا ينقصها.
          - cancelledCreditNotes.Sum(n => n.VatAmount);

        var cancelledInputVat = cancelledPurchases.Sum(p => p.VatAmount);

        var glBalance = await ReadVatNetPayableGlAsync(companyId, to);

        var (journalOutputVat, journalInputVat) = await ReadJournalOnlyVatAsync(companyId, from, to);

        // (CEEF-229 · A-15) التفكيك بالفئة — من السطور المحمَّلة أصلاً، بلا استعلام إضافيّ.
        //
        // (جاهزيّةُ الإطلاق · ح-٤١ · tax-zatca#12) ‏**لكلّ مستندٍ أوعيتُه بخصمه وسعره — ثمّ
        // ‏تُجمع.** وكانت السطورُ كلُّها تُصبّ خاماً في حسابٍ واحد: **بلا خصمِ مستندٍ وبعملة
        // ‏المستند**، والإجماليّاتُ بالريال بعد الخصم — فلا يمكن أن تتطابق. **وكان المستخلصُ
        // ‏وتسويةُ الإلغاء غائبَين**، وكلاهما في الصافي.
        //
        // ‏**والإشارةُ كإشارتها في الصافي حرفاً**: الإشعارُ الدائنُ وإلغاءُ ما صدر قبل الفترة
        // ‏يُطرحان، وإلغاءُ إشعارٍ دائنٍ سابقٍ يُعيد ما طرحه. فالهويّةُ:
        // ‏ضريبةُ الأوعية + ما قُيّد بلا مستند (ولا فئةَ له) == صافي المخرجات.
        var salesByCategory = SumByCategory(
        [
            .. sales.SelectMany(i => DocumentBuckets(
                i.Lines, i.DocumentDiscountAmount, i.ExchangeRate, i.Subtotal, +1)),
            .. progressBillings.Select(b => ProgressBillingBucket(b, +1)),
            .. creditNotes.SelectMany(n => DocumentBuckets(
                n.Lines, n.DocumentDiscountAmount, n.ExchangeRate, n.Subtotal, -1)),
            .. cancelledSales.SelectMany(i => DocumentBuckets(
                i.Lines, i.DocumentDiscountAmount, i.ExchangeRate, i.Subtotal, -1)),
            .. cancelledProgressBillings.Select(b => ProgressBillingBucket(b, -1)),
            .. cancelledCreditNotes.SelectMany(n => DocumentBuckets(
                n.Lines, n.DocumentDiscountAmount, n.ExchangeRate, n.Subtotal, +1)),
        ]);
        var purchasesByCategory = SumByCategory(
        [
            .. purchases.Where(p => p.VatDeductible).SelectMany(p => DocumentBuckets(
                p.Lines, 0m, p.ExchangeRate, p.Subtotal, +1)),
            .. debitNotes.Where(n => deductiblePurchaseIds.Contains(n.OriginalPurchaseInvoiceId))
                .SelectMany(n => DocumentBuckets(
                    n.Lines, 0m, n.ExchangeRate, n.Subtotal, -1)),
            .. cancelledPurchases.SelectMany(p => DocumentBuckets(
                p.Lines, 0m, p.ExchangeRate, p.Subtotal, -1)),
        ]);

        // ‏(ح-٤١) ‏**والأبُ الذي تُعرض الأوعيةُ تحته — من إجماليّات المستندات المخزونة لا من
        // ‏الأوعية.** فأبٌ يُحسب من أبنائه لا يُثبت شيئاً؛ **ومن مصدرٍ آخر تصير المساواةُ فحصاً.**
        var salesBaseNet =
            sales.Sum(i => i.Subtotal) + progressBillings.Sum(b => b.TaxableAmount)
            - creditNotes.Sum(n => n.Subtotal)
            - (cancelledSales.Sum(i => i.Subtotal) + cancelledProgressBillings.Sum(b => b.TaxableAmount)
               - cancelledCreditNotes.Sum(n => n.Subtotal));
        var purchasesBaseNet =
            purchases.Where(p => p.VatDeductible).Sum(p => p.Subtotal)
            - debitNotes.Where(n => deductiblePurchaseIds.Contains(n.OriginalPurchaseInvoiceId))
                        .Sum(n => n.Subtotal)
            - cancelledPurchases.Sum(p => p.Subtotal);

        return new VatReturnResult(
            from, to,
            SalesSubtotal: sales.Sum(i => i.Subtotal),
            OutputVat: sales.Sum(i => i.VatAmount),
            ProgressBillingsSubtotal: progressBillings.Sum(b => b.TaxableAmount),
            ProgressBillingsVat: progressBillings.Sum(b => b.VatAmount),
            CreditNotesSubtotal: creditNotes.Sum(n => n.Subtotal),
            CreditNotesVat: creditNotes.Sum(n => n.VatAmount),
            PurchasesSubtotal: purchases.Sum(p => p.Subtotal),
            DeductibleInputVat: purchases.Where(p => p.VatDeductible).Sum(p => p.VatAmount),
            DebitNotesSubtotal: debitNotes.Sum(n => n.Subtotal),
            DebitNotesVat: debitNotes.Sum(n => n.VatAmount),
            DebitNotesDeductibleVat: debitNotes
                .Where(n => deductiblePurchaseIds.Contains(n.OriginalPurchaseInvoiceId))
                .Sum(n => n.VatAmount),
            GlVatNetPayableBalance: glBalance,
            CancelledOutputVat: cancelledOutputVat,
            CancelledInputVat: cancelledInputVat,
            SalesByCategory: salesByCategory,
            PurchasesByCategory: purchasesByCategory,
            JournalOutputVat: journalOutputVat,
            JournalInputVat: journalInputVat,
            SalesBaseNet: salesBaseNet,
            PurchasesBaseNet: purchasesBaseNet);
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٤١) <b>أوعيةُ مستندٍ واحدٍ بالريال — كما يحسبها المستندُ لنفسه.</b>
    ///
    /// <para><b>بخصمه</b> (يُوزَّع على الأوعية بنسبها في <c>ComputeSubtotals</c>) <b>ثمّ
    /// بسعره</b> — الوعاءُ والضريبةُ كلٌّ على حدة، <b>كما يُحوِّل المستندُ إجماليَّه</b>
    /// (<c>RecalculateTotals</c>).</para>
    ///
    /// <para><b>وفلسُ التحويل:</b> <c>Σ round(وعاء × سعر)</c> قد يفترق عن
    /// <c>round(Σ وعاء × سعر)</c> — والثاني هو ما في الأستاذ. <b>فيُسكَن الفرقُ في طرفٍ واحدٍ
    /// مُعلَن</b> (وهو مذهبُ <c>ComputeSubtotals</c> نفسِه في خصم المستند): أكبرُ الأوعية.</para>
    ///
    /// <para><b>ولا يُمتصّ إلّا ما يُفسّره التقريب</b> — <c>0.005 × (عدد الأوعية + 1)</c>.
    /// فرقٌ أكبرُ يعني أنّ إجماليَّ المستند المخزونَ وسطورَه مختلفان — <b>عطلُ بياناتٍ لا
    /// تقريب</b> — <b>ويبقى ظاهراً</b> في المطابقة تحت الأب، لا يُبتلع في وعاء.</para>
    ///
    /// <para><b>ولا فرقَ في الضريبة أصلاً — بالبناء لا بالحظّ:</b> الأساسيّةُ وحدَها تحمل
    /// ضريبةً (الصفريُّ والمعفى وخارجُ النطاق بنسبة صفر)، <b>وللمستند وعاءٌ أساسيٌّ واحدٌ على
    /// الأكثر</b> — فتحويلُه هو تحويلُ ضريبة المستند حرفاً. <b>ولم يُكتب لها امتصاصٌ</b>: فرعٌ لا
    /// يُبلَغ لا يُقاس، ويوحي بخطرٍ غير قائم. <b>وفئةٌ ثانيةٌ بنسبةٍ غير صفريّةٍ تكسر هذا</b> —
    /// وتُمسكها هويّةُ <c>VatReturnBucketsReconcileTests</c> قبل أن تبلغ إقراراً.</para>
    /// </summary>
    private static IEnumerable<VatReturnBucket> DocumentBuckets(
        IReadOnlyCollection<SalesInvoiceLine> lines, decimal documentDiscount, decimal rate,
        decimal ledgerSubtotal, int sign)
    {
        if (lines.Count == 0)
            return [];

        var buckets = LineDocumentTotalsCalculator.ComputeSubtotals(lines, documentDiscount: documentDiscount)
            .Select(s => new VatReturnBucket(
                s.Category,
                Math.Round(s.TaxableAmount * rate, 2, MidpointRounding.AwayFromZero),
                Math.Round(s.VatAmount * rate, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        var gap = ledgerSubtotal - buckets.Sum(b => b.TaxableAmount);
        if (gap != 0m && Math.Abs(gap) <= 0.005m * (buckets.Count + 1))
        {
            var i = IndexOfLargestTaxable(buckets);
            buckets[i] = buckets[i] with { TaxableAmount = buckets[i].TaxableAmount + gap };
        }

        return sign > 0
            ? buckets
            : buckets.Select(b => b with { TaxableAmount = -b.TaxableAmount, VatAmount = -b.VatAmount });
    }

    /// <summary>
    /// (ح-٤١) <b>المستخلصُ وعاءٌ أساسيٌّ بالريال</b> — ضريبتُه <c>TaxableAmount × SaudiVatRate</c>
    /// بالبناء (<c>ProgressBilling.VatAmount</c>)، فلا فئةَ له سواها.
    /// </summary>
    private static VatReturnBucket ProgressBillingBucket(ProgressBilling billing, int sign) =>
        new(Brqosh.Domain.Tax.VatCategory.Standard, sign * billing.TaxableAmount, sign * billing.VatAmount);

    /// <summary>أوّلُ الأكبر وعاءً — فالتعادلُ يُحسم بترتيب الفئة، حتميّاً.</summary>
    private static int IndexOfLargestTaxable(IReadOnlyList<VatReturnBucket> buckets)
    {
        var best = 0;
        for (var i = 1; i < buckets.Count; i++)
            if (Math.Abs(buckets[i].TaxableAmount) > Math.Abs(buckets[best].TaxableAmount))
                best = i;
        return best;
    }

    /// <summary>
    /// جمعُ أوعية المستندات بالفئة — <b>بعد</b> تقريب كلّ مستند، لا قبله: فالمستندُ يُقرّب
    /// أوعيتَه لنفسه (<c>cac:TaxSubtotal</c>)، والإقرارُ يجمع ما قدّمته المستندات.
    /// </summary>
    private static IReadOnlyList<VatReturnBucket> SumByCategory(IEnumerable<VatReturnBucket> parts) =>
        [.. parts.GroupBy(b => b.Category)
                 .OrderBy(g => g.Key)
                 .Select(g => new VatReturnBucket(g.Key, g.Sum(b => b.TaxableAmount), g.Sum(b => b.VatAmount)))];

    /// <summary>
    /// (CEEF-232 · ض-١) بادئاتُ المستندات الخمسة التي يقرؤها الإقرارُ أصلاً.
    ///
    /// <para><b>وهي مفتاحُ ألّا يُحسب المستندُ مرّتين</b>: فاتورةُ المبيعات تُرحَّل قيداً
    /// على <c>2106</c> كذلك، <b>فقراءةُ الأستاذ بلا تمييزٍ تُضاعف ضريبةَ كلّ فاتورة</b> —
    /// ويُقدَّم إقرارٌ بثلاثين ألفاً على مبيعاتِ مئة ألف، وهي نسبةُ ٣٠٪ تردّها الهيئة فوراً.</para>
    ///
    /// <para><b>والعكسُ يرث مصدرَ أصله</b> (<c>CreateReversal</c>)، فإلغاءُ فاتورةٍ يبقى
    /// مستنداً محسوباً — <b>ولا يُعدّ توريداً جديداً بالسالب</b> فوق تسوية الإلغاء المحسوبة.</para>
    /// </summary>
    private static readonly string[] CountedDocumentPrefixes =
    [
        "SalesInvoice:", "CreditNote:", "ProgressBilling:", "PurchaseInvoice:", "DebitNote:",
    ];

    /// <summary>
    /// (CEEF-232 · ض-١) <b>الضريبةُ التي قُيّدت في الأستاذ ولم يحملها مستندٌ محسوب.</b>
    ///
    /// <para><b>والتمييزُ الحاكم — وبغيره ينكسر الإقرارُ في الاتجاه المقابل:</b> قيدُ
    /// <b>التسوية</b> ينقل الضريبةَ من <c>2106</c> إلى <c>2107</c> <b>وليس توريداً</b>.
    /// فعدُّه مخرجاتٍ سالبةً <b>يُفرّغ الإقرارَ من ضريبته كلِّها</b> — ويُقدَّم بلا ريالٍ
    /// على منشأةٍ باعت بمئة ألف. <b>والقاعدة: قيدٌ يمسّ حسابَ صافي الضريبة تسويةٌ لا توريد.</b></para>
    ///
    /// <para><b>والإشارةُ بطبيعة الحساب لا باصطلاح</b>: المخرجاتُ التزامٌ فالدائنُ يزيدها،
    /// والمدخلاتُ أصلٌ فالمدينُ يزيدها. <b>وقيدٌ يعكس توريداً يُنقص كليهما بالإشارة نفسِها</b>.</para>
    /// </summary>
    private async Task<(decimal OutputVat, decimal InputVat)> ReadJournalOnlyVatAsync(
        long companyId, DateOnly from, DateOnly to)
    {
        var vatAccounts = await db.Accounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.CompanyId == companyId
                        && (a.Code == SaudiChartOfAccounts.SystemCodes.VatOutput
                            || a.Code == SaudiChartOfAccounts.SystemCodes.VatInput
                            || a.Code == SaudiChartOfAccounts.SystemCodes.VatNetPayable))
            .Select(a => new { a.Id, a.Code })
            .ToListAsync();
        if (vatAccounts.Count == 0)
            return (0m, 0m);

        var outputId = vatAccounts
            .FirstOrDefault(a => a.Code == SaudiChartOfAccounts.SystemCodes.VatOutput)?.Id;
        var inputId = vatAccounts
            .FirstOrDefault(a => a.Code == SaudiChartOfAccounts.SystemCodes.VatInput)?.Id;
        var netId = vatAccounts
            .FirstOrDefault(a => a.Code == SaudiChartOfAccounts.SystemCodes.VatNetPayable)?.Id;
        if (outputId is null && inputId is null)
            return (0m, 0m);

        var candidates = await db.JournalEntries
            .WhereReportable(companyId, from, to)
            .Where(e => e.SourceDocument == null
                        || !CountedDocumentPrefixes.Any(pfx => e.SourceDocument.StartsWith(pfx)))
            // (CEEF-232 · ض-٣) ‏وزوجُ العكس يُستبعد **بطرفيه معاً** لا يُترك ليتقاصّ.
            // ‏فالأصلُ المعكوس حالتُه `Reversed`، والعكسُ يُشار إليه بـ`ReversalEntryId` —
            // ‏وهو سؤالُ «القائم» نفسُه، ومالكُه `StandingJournalEntries` (ح-٤٧).
            .WhereStanding(db.JournalEntries)
            .Select(e => new
            {
                TouchesNetVat = netId != null && e.Lines.Any(l => l.AccountId == netId),
                DepositVatOffset = e.SourceDocument != null
                                   && (e.SourceDocument.StartsWith(ReservationDepositService.AppliedPrefix)
                                       || e.SourceDocument.StartsWith(ReservationDepositService.RefundedPrefix)),
                Lines = e.Lines
                    .Where(l => l.AccountId == outputId || l.AccountId == inputId)
                    .Select(l => new { l.AccountId, l.Debit, l.Credit })
                    .ToList(),
            })
            .ToListAsync();

        var outputVat = 0m;
        var inputVat = 0m;
        foreach (var entry in candidates)
        {
            if (entry.TouchesNetVat)
                continue;

            // (CEEF-232 · ض-٣) ‏الحركةُ تُجمع **على مستوى القيد** ثمّ يُحكم عليها — لا سطراً سطراً.
            var entryOutput = 0m;
            var entryInput = 0m;
            foreach (var line in entry.Lines)
            {
                if (line.AccountId == outputId)
                    entryOutput += line.Credit - line.Debit;
                else
                    entryInput += line.Debit - line.Credit;
            }

            // (CEEF-232 · ض-٣) ‏**والنقصُ لا يُقرأ توريداً سالباً — بل تسوية.**
            //
            // ‏وهذا هو نصفُ البند الذي كاد يقلب نفعَه ضرراً: **حارسُ `2107` لم يكن يحرس شيئاً**،
            // ‏فلا كاتبَ لهذا الحساب في المستودع كلِّه (‏`SystemAccountCoverageTests`‏ يقولها نصّاً).
            // ‏والتسويةُ الوحيدةُ التي يعرفها المنتجُ فعلاً هي `مدين 2106 / دائن البنك` — بلا
            // ‏مستندٍ محسوب، وبلا مساسٍ بـ`2107`. **فكانت تُقرأ مخرجاتٍ سالبة**: منشأةٌ باعت بمئة
            // ‏ألفٍ ثمّ سدّدت ضريبةَ الربع تُقدّم إقرارَها التالي **منقوصاً بخمسةَ عشرَ ألفاً** —
            // ‏وهو عينُ العطل الذي جاء البندُ ليُغلقه، مقلوباً.
            //
            // ‏**والزيادةُ وحدَها توريد**: توريدٌ بلغ الأستاذَ بلا مستندٍ لا يكون إلّا زيادةً في
            // ‏الالتزام. وكلُّ نقصٍ إمّا سدادٌ للهيئة أو عكسُ قيدٍ — **والعكسُ استُبعد بطرفيه
            // ‏أعلاه**، فلم يبقَ للنقص إلّا السداد.
            //
            // ‏**والباقي المعلوم:** تصحيحٌ يدويٌّ يُنقص توريداً يُهمَل، فيُقدَّم الإقرارُ **زائداً**
            // ‏لا ناقصاً. وهو خطأٌ في جانب المنشأة لا جانب الهيئة، **وطريقُه الصحيح إشعارٌ دائن**
            // ‏لا قيدٌ يدويّ — وهو مستندٌ محسوبٌ أصلاً.
            if (entryOutput > 0m) outputVat += entryOutput;

            // ‏(جاهزيّةُ الإطلاق · ح-٨١) ‏**ونقصٌ ثانٍ معلومٌ غيرُ السداد: ردُّ ضريبة العربون عند إنزاله.**
            //
            // ‏ضريبةُ العربون أُقرّت في شهر قبضه (نقطةُ استحقاق)، وفاتورةُ المغادرة تحمل ضريبةَ الإقامة كلِّها —
            // ‏فالإنزالُ يردّ ما أُقرّ سلفاً. **وإهمالُه (كما يُهمَل النقصُ عامّةً) يُقرّ ضريبةَ العربون مرّتين.**
            // ‏ويُعرف بمصدره لا بإشارته: لا يُقرأ كلُّ نقصٍ خصماً، بل هذا وحدَه.
            // ‏(ح-٨٣) **وردُّ العربون عند الإلغاء مثلُه** — ضريبةُ مالٍ رُدّ تخرج من إقرار شهر الردّ.
            else if (entryOutput < 0m && entry.DepositVatOffset) outputVat += entryOutput;
            if (entryInput > 0m) inputVat += entryInput;
        }

        return (Math.Round(outputVat, 2, MidpointRounding.AwayFromZero),
                Math.Round(inputVat, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>رصيد حساب صافي الضريبة — <c>null</c> إذا لم يوجد الحساب أو لا حركة عليه.</summary>
    private async Task<decimal?> ReadVatNetPayableGlAsync(long companyId, DateOnly asOf)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a =>
            a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.VatNetPayable);
        if (account is null)
            return null;

        var rows = await new TrialBalanceQuery(db).ExecuteAsync(
            companyId, new DateOnly(1900, 1, 1), asOf);
        var row = rows.FirstOrDefault(r => r.AccountCode == account.Code);
        if (row is null)
            return null;

        // التزام — رصيد دائن موجب
        return row.BalanceCredit - row.BalanceDebit;
    }
}
