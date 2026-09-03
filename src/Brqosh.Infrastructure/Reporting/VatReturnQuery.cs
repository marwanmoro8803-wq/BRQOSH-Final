using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
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
    /// </summary>
    public decimal NetOutputVat =>
        OutputVat + ProgressBillingsVat - CreditNotesVat - CancelledOutputVat + JournalOutputVat;

    /// <summary>
    /// (CEEF-232 · ض-١) والمدخلاتُ كذلك — <b>والاتجاهُ هنا في صالح المنشأة</b>: عماها عنه
    /// يجعلها تدفع ضريبةً خصمُها حقٌّ لها، <b>وهو عطلٌ لا يشتكي منه أحدٌ ويُكلّف مالاً كلَّ ربع</b>.
    /// </summary>
    public decimal NetDeductibleInputVat =>
        DeductibleInputVat - DebitNotesDeductibleVat - CancelledInputVat + JournalInputVat;
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
        // الإشعارات تُطرح بإشارة سالبة فيصير كل وعاء **صافياً** كما يُقدَّم في الإقرار.
        var salesByCategory = Aggregate(
            [.. sales.SelectMany(i => i.Lines), .. creditNotes.SelectMany(n => n.Lines.Select(Negate))]);
        var purchasesByCategory = Aggregate(
            [.. purchases.Where(p => p.VatDeductible).SelectMany(p => p.Lines),
             .. debitNotes.Where(n => deductiblePurchaseIds.Contains(n.OriginalPurchaseInvoiceId))
                          .SelectMany(n => n.Lines.Select(Negate))]);

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
            JournalInputVat: journalInputVat);
    }

    /// <summary>سطرٌ بإشارة معكوسة — الإشعارات تُطرح من وعائها لا تُفرَد في وعاءٍ خاصّ.</summary>
    private static SalesInvoiceLine Negate(SalesInvoiceLine line) =>
        line with { Quantity = -line.Quantity };

    /// <summary>
    /// تجميع السطور في أوعية بالفئة، بنفس تقريب <c>ComputeSubtotals</c> — على مستوى **الوعاء**
    /// لا السطر ولا المستند، وهو ما تفرضه دلالة <c>cac:TaxSubtotal</c> لدى الهيئة.
    /// </summary>
    private static IReadOnlyList<VatReturnBucket> Aggregate(IReadOnlyList<SalesInvoiceLine> lines) =>
        lines.Count == 0
            ? []
            : [.. LineDocumentTotalsCalculator.ComputeSubtotals(lines)
                   .Select(s => new VatReturnBucket(s.Category, s.TaxableAmount, s.VatAmount))];

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
            // ‏فالأصلُ المعكوس حالتُه `Reversed`، والعكسُ يُشار إليه بـ`ReversalEntryId`.
            .Where(e => e.Status != JournalEntryStatus.Reversed
                        && !db.JournalEntries.Any(x => x.ReversalEntryId == e.Id))
            .Select(e => new
            {
                TouchesNetVat = netId != null && e.Lines.Any(l => l.AccountId == netId),
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
