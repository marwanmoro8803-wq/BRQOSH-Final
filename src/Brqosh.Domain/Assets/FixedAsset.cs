using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Tax;

namespace Brqosh.Domain.Assets;

public enum DepreciationMethod
{
    StraightLine = 0,

    /// <summary>
    /// (جاهزيّةُ الإطلاق · <c>treasury-payroll-assets#11</c>) <b>القسطُ المتناقصُ المضاعَف</b> — ضعفُ نسبة الخطّ المستقيم على القيمة
    /// الدفتريّة كلَّ شهر، <b>ويتحوّل إلى الخطّ المستقيم متى زاد قسطُه على الباقي من العمر</b> (العرفُ المحاسبيّ، فيُستنفد القابلُ
    /// للإهلاك بنهاية العمر)، ولا ينزل عن الخردة. وهو من الطرق التي يقبلها IAS 16 §62.
    /// </summary>
    DecliningBalance = 1,
}

/// <summary>أصل ثابت — يُهلك شهرياً بخط مستقيم أو بقسطٍ متناقص (<see cref="Assets.DepreciationMethod"/>).</summary>
public class FixedAsset : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public DateOnly AcquisitionDate { get; private set; }
    public decimal AcquisitionCost { get; private set; }
    public decimal SalvageValue { get; private set; }
    public int UsefulLifeMonths { get; private set; }
    public decimal AccumulatedDepreciation { get; private set; }
    public long GlAssetAccountId { get; private set; }
    public long GlDepreciationAccountId { get; private set; }
    public long? AcquisitionJournalEntryId { get; private set; }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٠٠ · <c>treasury-payroll-assets#12</c>) <b>آخرُ يومٍ أُهلك قبل الدفاتر</b> — لأصلٍ قائمٍ قبل بدء التشغيل
    /// يُدخَل برصيده الافتتاحيّ (<see cref="CreateOpening"/>). <b>وفارغُه لأصلٍ يُقتنى في الدفاتر</b>: إهلاكُه من شهر اقتنائه.
    ///
    /// <para>وكان الأصلُ القائمُ لا يُدخَل إلا اقتناءً جديداً بتكلفته كلِّها — <b>فيستدرك التشغيلُ الأوّلُ كلَّ شهرٍ منذ اقتنائه الأصليّ</b>،
    /// وقد أُهلك في الدفاتر السابقة، ويُحمّله على سنة التشغيل.</para>
    /// </summary>
    public DateOnly? DepreciatedThrough { get; private set; }
    public long? DisposalJournalEntryId { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٥٧ · <c>treasury-payroll-assets#10</c>) <b>تاريخُ إلغاء الاقتناء</b> — أصلٌ أُدخل خطأً فعُكس قيدُه.
    /// <b>وفارغُه لأصلٍ قائمٍ أو مُستبعَد</b>: فالإلغاءُ غيرُ الاستبعاد — لا بيعَ ولا ربحَ ولا خسارة، والأصلُ كأن لم يكن.
    /// </summary>
    public DateOnly? CancelledOn { get; private set; }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٥٨ · <c>treasury-payroll-assets#12</c>) <b>أصلٌ يُشترى من مورّدٍ بفاتورته</b> — مسجَّلٌ ولم يُقتنَ بعد:
    /// <b>غيرُ قائمٍ</b> فلا يُهلَك ولا يُستبعَد، حتى تُرحَّل فاتورةُ الشراء المربوطةُ به فتُدين حسابَه وضريبةَ المدخلات والمورّد،
    /// ويصير قائماً بقيدها.
    /// </summary>
    public bool AwaitingInvoice { get; private set; }

    /// <summary>(ح-١٥٨) أصلٌ يُسجَّل لتقتنيه فاتورةُ مورّد — لا قيدَ له حتى تُرحَّل.</summary>
    public static FixedAsset CreateAwaitingInvoice(long companyId, string code, string nameAr, DateOnly acquisitionDate,
        decimal acquisitionCost, decimal salvageValue, int usefulLifeMonths,
        long glAssetAccountId, long glDepreciationAccountId,
        DepreciationMethod method = DepreciationMethod.StraightLine)
    {
        var asset = Create(companyId, code, nameAr, acquisitionDate, acquisitionCost, salvageValue,
            usefulLifeMonths, glAssetAccountId, glDepreciationAccountId, method);
        asset.AwaitingInvoice = true;
        asset.IsActive = false;
        return asset;
    }

    /// <summary>
    /// (ح-١٥٨) <b>فاتورةُ المورّد اقتنته</b> — بتاريخها، فالإهلاكُ من شهر الاقتناء الحقيقيّ؛ وتاريخٌ غيرُه يُرفض لا يُعدَّل صامتاً.
    /// </summary>
    public void AcquireByInvoice(long journalEntryId, DateOnly invoiceDate)
    {
        if (!AwaitingInvoice || CancelledOn is not null)
            throw new AccountingException($"الأصل {Code} لا ينتظر فاتورةَ مورّد — اقتُني أو أُلغي.");
        if (invoiceDate != AcquisitionDate)
            throw new AccountingException(
                $"تاريخُ فاتورة الأصل {invoiceDate:yyyy-MM-dd} غيرُ تاريخ اقتنائه {AcquisitionDate:yyyy-MM-dd} — وحِّدهما.");
        AwaitingInvoice = false;
        IsActive = true;
        AcquisitionJournalEntryId = journalEntryId;
    }

    // ── بيعُ الأصل (جاهزيّةُ الإطلاق · ح-١٦) ────────────────────────────────

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٦) <b>المشتري — ولم يكن للأصل مشترٍ قطّ.</b>
    ///
    /// <para>بيعُ أصلٍ من أصول النشاط <b>توريدٌ خاضعٌ نظاماً</b>، والاستبعادُ كان يُقيّد
    /// ضريبةَ مخرجاتٍ على <c>2106</c> <b>بلا مستندٍ خلفَها ولا طرفٍ ثانٍ</b>. فلا تُعرف
    /// جهةُ التوريد في الدفاتر أصلاً — <b>ولا في القيد إلّا سرداً</b>.</para>
    ///
    /// <para><b>ويبقى مسموحاً بالفراغ</b> لبيعٍ نقديٍّ لمجهول (خردة) — فيُفوتَر <b>مبسَّطةً</b>
    /// كما تفعل نقطةُ البيع بـ«عميل نقدي»، وهو مسارٌ نظاميٌّ لا تحايل.</para>
    /// </summary>
    public long? DisposalBuyerCustomerId { get; private set; }

    /// <summary>
    /// رقمُ فاتورة الضريبة الصادرة عن البيع — <b>وهو ما يربط القيدَ بالمستند الإلكترونيّ.</b>
    /// <b>وفارغٌ يعني أنّ البيعَ لم يُفوتَر</b>: خردةٌ بلا مقابل، أو منشأةٌ خارج نطاق الضريبة.
    /// </summary>
    public string? DisposalInvoiceNumber { get; private set; }

    /// <summary>
    /// المقابلُ قبل الضريبة. <b>ولم يكن يُحفظ في أيّ حقل</b> — فسؤالُ المراجع «بكم بيع هذا
    /// الأصل؟» كان جوابُه <b>سردَ قيدٍ</b> لا رقماً في سجلّ الأصل.
    /// </summary>
    public decimal? DisposalProceeds { get; private set; }

    /// <summary>
    /// فئةُ التوريد وقتَ البيع. <b>ولا تُشتقّ من المبلغ بعدَه</b>: صفرُ ضريبةٍ يحتمل
    /// <b>تصديراً صفريّاً</b> و<b>معفًى</b> و<b>خارجَ النطاق</b>، وثلاثتُها تختلف في الإقرار.
    /// </summary>
    public VatCategory? DisposalVatCategory { get; private set; }

    /// <summary>رمزُ سبب الإعفاء (BT-121) حين لا تكون الفئةُ أساسيّة — تشترطه الهيئة.</summary>
    public string? DisposalVatExemptionCode { get; private set; }

    public decimal BookValue => AcquisitionCost - AccumulatedDepreciation;

    /// <summary>(<c>treasury-payroll-assets#11</c>) طريقةُ الإهلاك — الخطُّ المستقيمُ لكلّ أصلٍ قائم.</summary>
    public DepreciationMethod Method { get; private set; }

    /// <summary>
    /// قسطُ شهرٍ كامل — <b>وللقسط المتناقص قسطُ الشهر القادم على القيمة الدفتريّة اليوم</b> (تقديرٌ للعرض؛ والتشغيلُ يحسبه شهراً شهراً).
    /// </summary>
    public decimal MonthlyDepreciation =>
        UsefulLifeMonths <= 0 ? 0m
        : Method == DepreciationMethod.DecliningBalance
            ? Math.Min(Math.Round(BookValue * 2m / UsefulLifeMonths, 2, MidpointRounding.AwayFromZero), Math.Max(0m, BookValue - SalvageValue))
            : Math.Round((AcquisitionCost - SalvageValue) / UsefulLifeMonths, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// (جاهزيّةُ الإطلاق · <c>treasury-payroll-assets#11</c>) <b>يحدّد طريقةَ الإهلاك — قبل أوّل إهلاكٍ وحدَه.</b> فتغييرُها بعده
    /// تغييرٌ في تقديرٍ محاسبيٍّ يُعالَج بأثرٍ لاحقٍ مقصود (IAS 8 §36)، لا بخانةٍ تُعيد كتابةَ ما أُهلك.
    /// </summary>
    public void UseDepreciationMethod(DepreciationMethod method)
    {
        if (!Enum.IsDefined(method))
            throw new AccountingException("طريقة الإهلاك غير معروفة.");
        if (method == Method)
            return;
        if (AccumulatedDepreciation != 0m || DepreciatedThrough is not null)
            throw new AccountingException("طريقة الإهلاك تُحدَّد قبل أول إهلاك — وتغييرها بعده تغييرٌ في التقدير لا تصحيح.");
        Method = method;
    }

    private FixedAsset()
    {
        Code = null!;
        NameAr = null!;
    }

    private FixedAsset(long companyId, string code, string nameAr, DateOnly acquisitionDate,
        decimal acquisitionCost, decimal salvageValue, int usefulLifeMonths,
        long glAssetAccountId, long glDepreciationAccountId)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        AcquisitionDate = acquisitionDate;
        AcquisitionCost = acquisitionCost;
        SalvageValue = salvageValue;
        UsefulLifeMonths = usefulLifeMonths;
        GlAssetAccountId = glAssetAccountId;
        GlDepreciationAccountId = glDepreciationAccountId;
    }

    public static FixedAsset Create(long companyId, string code, string nameAr, DateOnly acquisitionDate,
        decimal acquisitionCost, decimal salvageValue, int usefulLifeMonths,
        long glAssetAccountId, long glDepreciationAccountId,
        DepreciationMethod method = DepreciationMethod.StraightLine)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود الأصل مطلوب.");
        if (acquisitionCost <= 0)
            throw new AccountingException("تكلفة الاقتناء يجب أن تكون أكبر من صفر.");
        if (usefulLifeMonths <= 0)
            throw new AccountingException("العمر الإنتاجي بالأشهر مطلوب.");

        var asset = new FixedAsset(companyId, code.Trim(), nameAr.Trim(), acquisitionDate,
            Math.Round(acquisitionCost, 2, MidpointRounding.AwayFromZero),
            Math.Round(salvageValue, 2, MidpointRounding.AwayFromZero),
            usefulLifeMonths, glAssetAccountId, glDepreciationAccountId);
        asset.UseDepreciationMethod(method);
        return asset;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٠٠) <b>أصلٌ قائمٌ قبل بدء التشغيل — بمجمّع إهلاكه حتى نهاية الشهر السابق لتاريخ الرصيد الافتتاحيّ.</b>
    ///
    /// <para><b>والمجمّعُ لا يتجاوز القابلَ للإهلاك</b> (التكلفة ناقص القيمة المتبقية) ولا يقلّ عن الصفر، <b>والرصيدُ الافتتاحيُّ بعد شهر
    /// الاقتناء</b> — وإلّا فهو اقتناءٌ في الدفاتر لا رصيدٌ قبلها.</para>
    /// </summary>
    public static FixedAsset CreateOpening(long companyId, string code, string nameAr, DateOnly acquisitionDate,
        decimal acquisitionCost, decimal salvageValue, int usefulLifeMonths,
        long glAssetAccountId, long glDepreciationAccountId,
        decimal accumulatedDepreciation, DateOnly openingDate,
        DepreciationMethod method = DepreciationMethod.StraightLine)
    {
        var asset = Create(companyId, code, nameAr, acquisitionDate, acquisitionCost, salvageValue,
            usefulLifeMonths, glAssetAccountId, glDepreciationAccountId, method);

        var accumulated = Math.Round(accumulatedDepreciation, 2, MidpointRounding.AwayFromZero);
        if (accumulated < 0m || accumulated > asset.AcquisitionCost - asset.SalvageValue)
            throw new AccountingException(
                "مجمّعُ الإهلاك الافتتاحيّ بين الصفر والقابلِ للإهلاك (التكلفة ناقص القيمة المتبقية).");

        var openingMonth = new DateOnly(openingDate.Year, openingDate.Month, 1);
        if (openingMonth <= new DateOnly(acquisitionDate.Year, acquisitionDate.Month, 1))
            throw new AccountingException("تاريخُ الرصيد الافتتاحيّ بعد شهر الاقتناء — وإلّا فهو اقتناءٌ في الدفاتر.");

        asset.AccumulatedDepreciation = accumulated;
        asset.DepreciatedThrough = openingMonth.AddDays(-1);
        return asset;
    }

    /// <summary>
    /// (CEEF-232 · ع-٢) <b>يُرسمِل تحسيناً على الأصل — فتزيد تكلفتُه ويزيد قسطُه.</b>
    /// </summary>
    /// <remarks>
    /// <b>والقسطُ يُعاد حسابُه على التكلفة الجديدة بالعمر الأصليّ</b> — وهو ما يفعله
    /// <c>MonthlyDepreciation</c> بالبناء، فلا يلزم تدخّل. <b>والمُهلَك سابقاً لا يُمَسّ</b>:
    /// التحسينُ يقع اليومَ لا بأثرٍ رجعيّ، <b>وتعديلُ الماضي يُعيد فتحَ سنواتٍ أُقفلت</b>.
    ///
    /// <para><b>ولا يُرسمَل على أصلٍ مُستبعَد</b>: أصلٌ خرج من الدفاتر لا تُضاف إليه تكلفة —
    /// <b>وإضافتُها تُعيد إحياءَ أصلٍ ميّت</b> بقيمةٍ دفتريّةٍ مُختلَقة.
    /// </para>
    /// </remarks>
    public void Capitalize(decimal amount)
    {
        if (amount <= 0m)
            throw new AccountingException("مبلغ الرسملة يجب أن يكون أكبر من صفر.");
        if (!IsActive)
            throw new AccountingException("لا تُرسمَل تكلفةٌ على أصلٍ مُستبعَد.");

        AcquisitionCost = Math.Round(
            AcquisitionCost + amount, 2, MidpointRounding.AwayFromZero);
    }

    public void Rename(string nameAr)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم الأصل مطلوب.");
        NameAr = nameAr.Trim();
    }

    public void LinkAcquisitionJournal(long journalEntryId) => AcquisitionJournalEntryId = journalEntryId;

    public void LinkDisposalJournal(long journalEntryId) => DisposalJournalEntryId = journalEntryId;

    /// <summary>(ح-١٥٧) يُلغي الاقتناءَ — بعد عكس قيده؛ فيخرج الأصلُ من السجلّ القائم ولا يُهلَك ولا يُستبعَد.</summary>
    public void CancelAcquisition(DateOnly on)
    {
        if (CancelledOn is not null)
            throw new AccountingException("اقتناءُ الأصل مُلغًى مسبقاً.");
        if (!IsActive)
            throw new AccountingException("الأصل مستبعد — لا يُلغى اقتناؤه.");
        if (AccumulatedDepreciation != 0m && DepreciatedThrough is null)
            throw new AccountingException("الأصل أُهلك — يُعكس إهلاكُه أوّلاً.");
        IsActive = false;
        CancelledOn = on;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٦١) <b>يُعيد المُستبعَدَ إلى السجلّ</b> — بعد عكس قيد استبعاده. <b>وتبقى بياناتُ البيع</b> (المشتري والفاتورةُ
    /// والمقابل) أثراً لما عُكس، ويقرؤها الإشعارُ الدائن الذي يُلغي فاتورتَه؛ فبيعُه ثانيةً بفاتورةٍ يُرفض باسم الأولى.
    /// </summary>
    public void UndoDisposal()
    {
        if (IsActive || DisposalJournalEntryId is null || CancelledOn is not null)
            throw new AccountingException($"الأصل {Code} غيرُ مُستبعَد — لا استبعادَ يُعكس.");
        IsActive = true;
        DisposalJournalEntryId = null;
    }

    public void MarkDisposed()
    {
        if (!IsActive)
            throw new AccountingException("الأصل مستبعد مسبقاً.");
        IsActive = false;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٦) <b>تسجيلُ البيع على سجلّ الأصل — لا في سرد القيد وحدَه.</b>
    ///
    /// <para><b>ولا يُسجَّل مرّتين</b>: الأصلُ يُباع مرّةً واحدة، ومحاولةُ تسجيلِ بيعٍ ثانٍ
    /// تعني أنّ استبعاداً كُرِّر — <b>وذاك يُمنع هنا لا يُصحَّح لاحقاً</b>، لأنّ الرقمَ
    /// الثاني كان سيطمس الأوّلَ بلا أثر.</para>
    ///
    /// <para><b>وسببُ الإعفاء مطلوبٌ لغير الأساسيّة</b> — <c>BR-KSA-CL-06</c>: فئةٌ
    /// <c>Z</c>/<c>E</c>/<c>O</c> بلا رمزٍ من قائمة السعودية <b>ترفضها الهيئة</b>.
    /// <b>ويُمنع هنا لا عند التوليد</b>: المولّدُ يستهلك عدّاداً تسلسليّاً قبل أن يفحص،
    /// ورقمٌ استُهلك لمستندٍ مرفوضٍ يُلوّث سلسلةَ البصمات بعده.</para>
    /// </summary>
    public void RecordDisposalSale(
        long? buyerCustomerId, string invoiceNumber, decimal proceeds,
        VatCategory vatCategory, string? vatExemptionCode)
    {
        if (DisposalInvoiceNumber is not null)
            throw new AccountingException($"بيعُ الأصل {Code} مسجَّلٌ مسبقاً بالفاتورة {DisposalInvoiceNumber}.");
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new AccountingException("رقم فاتورة بيع الأصل مطلوب.");
        if (proceeds <= 0)
            throw new AccountingException("لا تُسجَّل فاتورةُ بيعٍ لأصلٍ بلا مقابل.");
        if (VatExemptionReason.IsRequiredFor(vatCategory) && string.IsNullOrWhiteSpace(vatExemptionCode))
            throw new AccountingException(
                $"فئةُ التوريد «{vatCategory}» تستلزم رمزَ سبب إعفاء (BT-121) — وبدونه تُردّ الفاتورة.");

        DisposalBuyerCustomerId = buyerCustomerId;
        DisposalInvoiceNumber = invoiceNumber.Trim();
        DisposalProceeds = proceeds;
        DisposalVatCategory = vatCategory;
        DisposalVatExemptionCode = vatExemptionCode?.Trim();
    }

    /// <summary>
    /// (CEEF-229 · م0-١٥) الاستبعاد مسموح في أيّ وقت من عمر الأصل.
    ///
    /// <para>كان هنا شرطٌ يرفض الاستبعاد ما دامت القيمة الدفترية أعلى من قيمة الخردة — أي
    /// <b>ما دام الأصل لم يُستهلَك بالكامل</b>. وهو يمنع أكثر حالات الاستبعاد وقوعاً: بيع سيارة
    /// بعد سنتين من عمرٍ إنتاجيّ عشر، أو استبعاد جهاز تلف. ورسالتُه تقترح علاجاً يُفسد الدفاتر:
    /// «شغّل الإهلاك أولاً» — أي تشغيلُه ستّاً وتسعين مرّة، فيُحمَّل إهلاكُ ثمان سنوات على شهرٍ
    /// واحد وتنحرف قوائم دخل تلك السنوات كلها.</para>
    ///
    /// <para><b>ولا مبرّر محاسبيّ له:</b> قيد الاستبعاد يوازن بالبناء في كلتا الحالتين — إذ
    /// يُخرج التكلفة كاملةً ويُقفل مجمّع الإهلاك مهما بلغ، والفرق يقع على الربح (4106) أو
    /// الخسارة (5215). فبيعٌ بأقلّ من القيمة الدفترية <b>خسارةٌ تُقيَّد</b> لا عمليةٌ تُمنَع.
    /// وحضورُ فرع الربح في المُرحِّل بلا مسارٍ يبلغه كان أوضح دليل على أن الحارس زائد.</para>
    ///
    /// <para><b>حدٌّ مسجَّل لا مُصلَح هنا:</b> الربح/الخسارة يُحسب على القيمة الدفترية <b>وقت آخر
    /// إهلاك مُرحَّل</b>. ولأن الإهلاك يُشغَّل بزرّ لا بالزمن، فأشهرُ ما بين آخر تشغيل وتاريخ
    /// الاستبعاد لا تُهلَك — فتُبالَغ القيمة الدفترية وتُبالَغ الخسارة. عيبٌ في محرّك الإهلاك
    /// (م3-٩) لا في الاستبعاد، ولا يُصلحه إبقاءُ حارسٍ يمنع العملية أصلاً.</para>
    /// </summary>
    public void EnsureReadyForDisposal()
    {
        if (!IsActive)
            throw new AccountingException("الأصل غير نشط.");
    }

    public decimal ApplyDepreciation() => ApplyDepreciation(1);

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٩٢) <b>يُنقص المجمَّعَ بما أُهلك في قيدٍ عُكس</b> — وإلّا بقي السجلُّ أقلَّ قيمةً من الأستاذ، ولا يُهلَك الشهرُ
    /// ثانيةً بمبلغه. <b>ولا على أصلٍ مُستبعَد</b>: الاستبعادُ أقفل المجمَّعَ كلَّه.
    /// </summary>
    public void UndoDepreciation(decimal amount)
    {
        if (!IsActive)
            throw new AccountingException("لا يُعكس إهلاكُ أصلٍ مُستبعَد.");
        if (amount <= 0m || amount > AccumulatedDepreciation)
            throw new AccountingException("مبلغُ الإهلاك المعكوس لا يطابق مجمَّعَ الأصل.");

        AccumulatedDepreciation -= amount;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٥٨ · treasury-payroll-assets#11) ‏<b>أقساطُ عدّة أشهرٍ دفعةً — ولا تتجاوز القابلَ للإهلاك.</b>
    /// </summary>
    public decimal ApplyDepreciation(int months)
    {
        var monthly = MonthlyDepreciation;
        if (months <= 0 || monthly <= 0 || BookValue <= SalvageValue)
            return 0m;

        var amount = Math.Min(monthly * months, BookValue - SalvageValue);
        AccumulatedDepreciation += amount;
        return amount;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٥٨ · treasury-payroll-assets#11) ‏<b>الأشهرُ المستحقّةُ حتى شهر التشغيل</b> — من الشهر
    /// التالي لآخر شهرٍ أُهلك، <b>وإن لم يُهلَك قطّ فمن شهر الاقتناء</b> (اتّفاقيّةُ الشهر الكامل).
    ///
    /// <para>‏<b>وكان كلُّ تشغيلٍ شهراً واحداً</b>: أصلٌ يُقتنى بعد شهر التشغيل يُهلَك فيه، وشهرٌ لم يُشغَّل لا يُستدرَك
    /// أبداً، وتشغيلُ شهرٍ سابقٍ بعد لاحقه يُحمّله ثانيةً. <b>وصفرٌ هنا يعني لا شيءَ مستحقّاً</b> — أصلٌ لم يُقتنَ
    /// بعد، أو شهرٌ أُهلك قبلُ.</para>
    ///
    /// <para>(ح-١٠٠) <b>ولأصلٍ قائمٍ قبل الدفاتر فمن الشهر التالي لـ<see cref="DepreciatedThrough"/></b> — ما أُهلك قبلها لا يُهلَك ثانيةً.</para>
    /// </summary>
    public int MonthsDue(DateOnly? lastDepreciatedMonth, DateOnly runDate) =>
        Math.Max(0, MonthIndex(runDate) - FirstDueMonth(lastDepreciatedMonth) + 1);

    private static int MonthIndex(DateOnly d) => d.Year * 12 + d.Month;

    private int FirstDueMonth(DateOnly? lastDepreciatedMonth)
    {
        var first = MonthIndex(AcquisitionDate);
        if (DepreciatedThrough is { } through)
            first = Math.Max(first, MonthIndex(through) + 1);
        if (lastDepreciatedMonth is { } last)
            first = Math.Max(first, MonthIndex(last) + 1);
        return first;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · <c>treasury-payroll-assets#11</c> · قرارُ المالك 2026-10-20) <b>إهلاكُ تشغيلٍ — وشهرُ الاقتناء بأيّامه.</b>
    ///
    /// <para><b>ما كان:</b> اتّفاقيّةُ الشهر الكامل — أصلٌ يُقتنى في التاسعِ والعشرين يُحمَّل شهرَه كلَّه. <b>والآن</b>: إن دخل شهرُ
    /// الاقتناء هذا التشغيلَ حُمِّل بنسبة أيّامه الباقية (من يوم الاقتناء إلى آخر الشهر)، وبقيّةُ الأشهر كاملة. <b>والنقصُ لا يضيع</b>:
    /// الحدُّ «القيمةُ الدفتريّةُ ناقصَ الخردة» يُبقي الأصلَ يُهلَك بعد عمره الاسميّ حتى يُستنفد، فيأخذ الشهرُ الأخيرُ الباقي.</para>
    ///
    /// <para>والأصلُ القائمُ قبل الدفاتر (<see cref="DepreciatedThrough"/>) لا يدخل شهرُ اقتنائه أيَّ تشغيل، فلا يُنسَّب.</para>
    /// </summary>
    public decimal ApplyDepreciationDue(DateOnly? lastDepreciatedMonth, DateOnly runDate, bool throughRunDay = false)
    {
        if (Method == DepreciationMethod.DecliningBalance)
            return ApplyDecliningBalanceDue(lastDepreciatedMonth, runDate, throughRunDay);

        var months = MonthsDue(lastDepreciatedMonth, runDate);
        var monthly = MonthlyDepreciation;
        if (months <= 0 || monthly <= 0 || BookValue <= SalvageValue)
            return 0m;

        decimal portion = months;
        if (FirstDueMonth(lastDepreciatedMonth) == MonthIndex(AcquisitionDate))
        {
            var days = DateTime.DaysInMonth(AcquisitionDate.Year, AcquisitionDate.Month);
            portion -= (decimal)(AcquisitionDate.Day - 1) / days;
        }

        // ‏(قرارُ المالك 2026-10-20) ‏**شهرُ الاستبعاد بأيّامه المنقضية** — يومُ الاستبعاد محسوبٌ، وما بعده لا يُهلَك.
        if (throughRunDay)
            portion -= (decimal)(DateTime.DaysInMonth(runDate.Year, runDate.Month) - runDate.Day)
                       / DateTime.DaysInMonth(runDate.Year, runDate.Month);

        var amount = Math.Min(Math.Round(monthly * portion, 2, MidpointRounding.AwayFromZero), BookValue - SalvageValue);
        AccumulatedDepreciation += amount;
        return amount;
    }

    /// <summary>
    /// (جاهزيّةُ الإطلاق · <c>treasury-payroll-assets#11</c>) <b>القسطُ المتناقصُ شهراً شهراً</b> — فالقسطُ ينقص كلَّ شهرٍ بنقص الدفتريّة،
    /// والاستدراكُ مجموعُ الأشهر لا شهرٌ مضروبٌ في عددها. ولكلّ شهر: الأكبرُ من (الدفتريّة × ٢ ÷ العمر) و(القابلُ ÷ الباقي من العمر)،
    /// <b>والباقي من العمر من تاريخ الاقتناء</b> لا من عدّادٍ مخزون — فلا يختلّ بعكس تشغيلةٍ أو باستدراك؛ وشهرُ الاقتناء بأيّامه كالخطّ المستقيم.
    /// </summary>
    private decimal ApplyDecliningBalanceDue(DateOnly? lastDepreciatedMonth, DateOnly runDate, bool throughRunDay)
    {
        if (UsefulLifeMonths <= 0)
            return 0m;

        var acquisitionMonth = MonthIndex(AcquisitionDate);
        var book = BookValue;
        var total = 0m;
        for (var month = FirstDueMonth(lastDepreciatedMonth); month <= MonthIndex(runDate); month++)
        {
            var depreciable = book - SalvageValue;
            if (depreciable <= 0m)
                break;

            var remainingLife = UsefulLifeMonths - (month - acquisitionMonth);
            var amount = remainingLife > 0
                ? Math.Max(book * 2m / UsefulLifeMonths, depreciable / remainingLife)
                : depreciable;
            // ‏أيّامُ الشهر المُهلَكة: من يوم الاقتناء (في شهر الاقتناء) إلى يوم الاستبعاد (في شهر الاستبعاد).
            var monthYear = (month - 1) / 12;
            var monthNumber = (month - 1) % 12 + 1;
            var daysInMonth = DateTime.DaysInMonth(monthYear, monthNumber);
            var startDay = month == acquisitionMonth ? AcquisitionDate.Day : 1;
            var endDay = throughRunDay && month == MonthIndex(runDate) ? runDate.Day : daysInMonth;
            if (startDay > 1 || endDay < daysInMonth)
                amount *= (decimal)(endDay - startDay + 1) / daysInMonth;

            amount = Math.Min(Math.Round(amount, 2, MidpointRounding.AwayFromZero), depreciable);
            book -= amount;
            total += amount;
        }

        AccumulatedDepreciation += total;
        return total;
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
