using Brqosh.Domain.Companies;
namespace Brqosh.Domain.Accounting;

/// <summary>حالة القيد في دورة حياته المحاسبية.</summary>
public enum JournalEntryStatus
{
    /// <summary>مسودة — قابلة للتعديل والحذف.</summary>
    Draft = 0,

    /// <summary>مرحّل — أثّر في الأرصدة، لا يُعدَّل ولا يُحذف (يُعكس بقيد عكسي فقط).</summary>
    Posted = 1,

    /// <summary>معكوس — أُلغي أثره بقيد عكسي مرتبط.</summary>
    Reversed = 2,
}

/// <summary>
/// قيد يومية وفق نظام القيد المزدوج.
/// القاعدة الذهبية: مجموع المدين = مجموع الدائن، دائماً وبلا استثناء.
/// القيد المرحّل غير قابل للتعديل — التصحيح يكون بقيد عكسي (Audit-safe).
/// </summary>
public class JournalEntry : IBranchScoped
{
    private readonly List<JournalLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }

    /// <summary>الرقم التسلسلي للقيد ضمن السنة المالية (يُمنح عند الترحيل).</summary>
    public string? EntryNumber { get; private set; }

    public DateOnly EntryDate { get; private set; }
    public string Description { get; private set; }
    public JournalEntryStatus Status { get; private set; } = JournalEntryStatus.Draft;

    /// <summary>مرجع القيد العكسي إذا عُكس هذا القيد.</summary>
    public long? ReversalEntryId { get; private set; }

    /// <summary>المستند المصدر (فاتورة، سند، مستخلص...) الذي ولّد القيد آلياً — null للقيود اليدوية.</summary>
    public string? SourceDocument { get; private set; }

    public long? WorkflowInstanceId { get; private set; }

    /// <summary>قيد يدوي — لا مستند مصدر آلي.</summary>
    public bool IsManual => string.IsNullOrWhiteSpace(SourceDocument);

    /// <summary>قيد بين شركات المجموعة — يُستبعد عند التوحيد.</summary>
    public bool IsIntercompany { get; private set; }

    /// <summary>الشركة المقابلة في معاملة بين الشركات.</summary>
    public long? CounterpartyCompanyId { get; private set; }

    /// <summary>
    /// (CEEF-231 · O-32) <b>قيدٌ على مستوى المنشأة — لا فرعَ له بحكم طبيعته.</b>
    ///
    /// <para><b>وليس هذا نقصَ بيانات.</b> الإقفالُ السنويُّ إجراءٌ نظاميٌّ على الشخصية
    /// الاعتبارية، <b>والفرعُ بُعدٌ إداريٌّ لا كيانٌ يُقفل</b>. فوسمُه بفرعٍ يُسند حقوقَ
    /// ملكيةِ المنشأة كلِّها إلى فرعٍ واحد — <b>وهو الفرعُ الذي صادف أن يكون مختاراً في
    /// جلسة المحاسب</b>، لا فرعٌ له معنى.</para>
    /// </summary>
    public bool IsEntityLevel { get; private set; }

    /// <summary>
    /// يُعلن القيدَ على مستوى المنشأة — <b>فلا يوسمه واسمُ الفروع</b>.
    /// </summary>
    /// <remarks>
    /// <b>ويُعلَن قبل الحفظ لا بعده</b>: الوسمُ يقع في <c>SaveChanges</c>، وما وُسم
    /// لا يُنزع — <c>AssignBranch</c> يرفض المرحَّل، <b>والقيدُ الوحيدُ في العام الذي
    /// يجب ألّا يُوسَم هو الذي يُوسَم أوّلاً</b>.
    /// </remarks>
    public void MarkEntityLevel()
    {
        EnsureDraft("إعلان القيد على مستوى المنشأة");
        IsEntityLevel = true;
    }

    public IReadOnlyList<JournalLine> Lines => _lines.AsReadOnly();

    public decimal TotalDebit => _lines.Sum(l => l.Debit);
    public decimal TotalCredit => _lines.Sum(l => l.Credit);
    public bool IsBalanced => TotalDebit == TotalCredit;

    // EF Core
    private JournalEntry()
    {
        Description = null!;
    }

    private JournalEntry(long companyId, DateOnly entryDate, string description, string? sourceDocument)
    {
        CompanyId = companyId;
        EntryDate = entryDate;
        Description = description;
        SourceDocument = sourceDocument;
    }

    public static JournalEntry CreateDraft(long companyId, DateOnly entryDate, string description, string? sourceDocument = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("بيان القيد مطلوب.");
        return new JournalEntry(companyId, entryDate, description.Trim(), sourceDocument);
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        EnsureDraft("تعيين الفرع");
        BranchId = branchId;
    }

    /// <summary>
    /// (CEEF-231 · ط4-٢) <b>يختم الفرعَ إن لم يكن مختوماً — ولا يُبدّله أبداً.</b>
    /// </summary>
    /// <remarks>
    /// <para><b>ولماذا يُقبل بعد الترحيل، و<see cref="AssignBranch"/> لا يُقبل:</b>
    /// حارسُ المسودّة يمنع <b>تعديلَ</b> قيدٍ مرحَّل — وهو صوابٌ لا يُمَسّ. <b>وملءُ حقلٍ
    /// فارغٍ ليس تعديلاً</b>: لا رقمَ يتغيّر، ولا قيدَ ينقلب، ولا تقريرَ يُعاد. والقيدُ
    /// يُرحَّل في السطر الذي يسبق إضافتَه إلى السياق، <b>فلا موضعَ للختم قبله</b> إلا أن
    /// تُصلَح سبعٌ وثلاثون خدمة — <b>والثامنةُ والثلاثون تُكتب غداً بلا ختم</b>.</para>
    ///
    /// <para><b>ولا يُبدّل فرعاً قائماً</b>: خدمةٌ أسندته من مستندها أعلمُ به من فرع
    /// الجلسة. <b>وختمٌ يكتب فوق ما أُسند يجعل قيدَ فرعٍ يُنسب إلى فرعٍ آخر</b>.</para>
    /// </remarks>
    public void StampBranchIfUnset(long branchId)
    {
        if (BranchId is not null) return;
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");

        BranchId = branchId;
    }

    public void MarkSubmittedForApproval(long workflowInstanceId)
    {
        EnsureDraft("إرسال للموافقة");
        if (!IsManual)
            throw new AccountingException("سير العمل للقيود اليدوية فقط.");
        if (_lines.Count < 2)
            throw new AccountingException("القيد اليدوي يحتاج سطرين على الأقل قبل الإرسال.");
        WorkflowInstanceId = workflowInstanceId;
    }

    public void MarkRejected()
    {
        if (WorkflowInstanceId is null)
            throw new AccountingException("القيد ليس ضمن سير عمل.");
        WorkflowInstanceId = null;
    }

    public void MarkIntercompany(long counterpartyCompanyId)
    {
        EnsureDraft("تعيين معاملة بين الشركات");
        if (counterpartyCompanyId <= 0)
            throw new AccountingException("الشركة المقابلة غير صالحة.");
        if (counterpartyCompanyId == CompanyId)
            throw new AccountingException("لا يمكن أن تكون الشركة المقابلة هي نفس الشركة.");
        IsIntercompany = true;
        CounterpartyCompanyId = counterpartyCompanyId;
    }

    /// <summary>
    /// (CEEF-229 · م2-٨) سُلَّم الهللة — <b>ثابتةٌ صحيحةٌ بالبناء لا مفحوصةٌ بعد الوقوع</b>.
    ///
    /// <para><b>العطل المقيس:</b> <c>IsBalanced</c> يقارن قيمتَين في الذاكرة، وعمودا المبلغ
    /// <c>numeric(18,2)</c>. فقيدٌ سطراه مدينان بـ10.005 وسطرُه الدائن 20.01 <b>متوازنٌ في
    /// الذاكرة</b> — إذ تتجاهل مساواةُ <c>decimal</c> فرقَ السُّلَّم فـ<c>20.010m == 20.01m</c> —
    /// ثم يُقرِّب PostgreSQL <b>كل سطرٍ على حدة</b> فيُخزَّن 10.01 + 10.01 = 20.02 مقابل 20.01.
    /// أي أن الأستاذ يفقد توازنه بهللة، والقيدُ مرحَّلٌ نظاميّاً.</para>
    ///
    /// <para>القياس أُجري على PostgreSQL حيّة لا استنتاجاً:
    /// <c>SELECT 10.005::numeric(18,2) + 10.005::numeric(18,2)</c> ⟵ <c>20.02</c>.</para>
    ///
    /// <para><b>ولماذا لم يكشفه اختبار:</b> الاختبارات كلُّها على SQLite، وهو يخزّن
    /// <c>decimal</c> بدقّته الكاملة ويتجاهل <c>HasPrecision</c> — فالتقريب الذي يقع في
    /// الإنتاج لا يقع في الاختبار أبداً. ومدخلُه متاحٌ اليوم من شاشة القيد اليدويّ: الحقل
    /// يعرض بـ<c>N2</c> ويقبل ما يُكتب، والشاشة تُظهر «✓ متوازن» بالأخضر لقيدٍ سيُخزَّن مختلاً.</para>
    ///
    /// <para>المقارنة بالقيمة لا بالسُّلَّم عمداً: <c>10.0000m</c> مقبولة لأنها تساوي
    /// <c>10.00m</c> بعد التقريب — المرفوض هو ما <b>يتغيّر</b> بالتقريب.</para>
    /// </summary>
    private static void EnsureHalalaScale(decimal debit, decimal credit)
    {
        if (debit != decimal.Round(debit, 2) || credit != decimal.Round(credit, 2))
            throw new AccountingException(
                "مبلغ السطر يتجاوز خانتَي الهللة — قرِّب المبلغ قبل الترحيل.");
    }

    /// <summary>
    /// (CEEF-230 · ط1-٧) <b>يختم مركزَ التكلفة على سطور الأرباح والخسائر.</b>
    ///
    /// <para><b>وعلى تلك وحدَها:</b> مركزُ التكلفة مفهومُ <b>أداء</b> لا مركزٍ ماليّ.
    /// فتصنيفُ ذمّةٍ دائنةٍ أو ضريبةِ مدخلاتٍ على مركزٍ لا يُجيب سؤالاً يُسأل، ويُضخّم
    /// التقريرَ بأرقامٍ لا تُقرأ.</para>
    ///
    /// <para><b>والتمييزُ ببادئة الكود</b> (4 إيراد · 5 مصروف) — وهو ارتباطٌ بقالب الحسابات
    /// السعوديّ يُقرّ به هذا التعليق. <b>وهو اصطلاحُ هذا المستودع القائم</b>: الهويّاتُ
    /// الجبرية ومسبارُ القطاعات يقرآن الأكواد ببادئاتها. وبديلُه حملُ نوعِ الحساب على كلّ
    /// سطرٍ مُخزَّن، وهو تكرارٌ يفترق عن مصدره يوماً.</para>
    ///
    /// <para><b>ولا يُبدَّل ما ضُبط</b>: سطرٌ حمل مركزَه من مُدخِله أولى من ختمٍ عامّ.</para>
    /// </summary>
    public void StampCostCenter(long? costCenterId)
    {
        if (costCenterId is null) return;
        EnsureDraft("ختم مركز التكلفة");

        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            if (line.CostCenterId is not null) continue;
            if (line.AccountCode.Length == 0) continue;
            if (line.AccountCode[0] is not ('4' or '5')) continue;

            _lines[i] = line with { CostCenterId = costCenterId };
        }
    }

    /// <summary>
    /// يُضيف سطراً — <b>والمبلغان بالعملة الوظيفية دائماً</b>.
    /// </summary>
    /// <remarks>
    /// (CEEF-231 · O-34) <b>ووسائطُ العملة مذكرةٌ لا مبلغٌ ثانٍ</b>: تحفظ أصلَ المعاملة
    /// وسعرَ ترجمتها، <b>ولا تدخل الميزانَ</b>. وهويّةُ <b>هـ-٢٥</b> تربطها بالمبلغ
    /// المُرحَّل: <c>round(ForeignAmount × ExchangeRate, 2) = Debit + Credit</c>.
    ///
    /// <para><b>وبغير هذا القيد تصير المذكرةُ كذبةً موثَّقة</b>: مبلغٌ أجنبيٌّ وسعرٌ لا
    /// يُنتجان ما رُحِّل، <b>فيُراجَع القيدُ بعد سنةٍ فيُقرأ خطأً في الترجمة وهو خطأٌ في
    /// المذكرة</b> — أو العكس، وهو أسوأ.</para>
    /// </remarks>
    public void AddLine(Account account, decimal debit, decimal credit, string? lineDescription = null,
        long? costCenterId = null, string? currencyCode = null, decimal? exchangeRate = null,
        decimal? foreignAmount = null)
    {
        EnsureDraft("إضافة سطر");

        if (!account.IsPostable)
            throw new AccountingException($"الحساب '{account.NameAr}' حساب تجميعي أو موقوف — الترحيل عليه غير مسموح.");
        if (debit < 0 || credit < 0)
            throw new AccountingException("لا يجوز إدخال مبالغ سالبة — استخدم الطرف المقابل (مدين/دائن).");
        if (debit > 0 && credit > 0)
            throw new AccountingException("لا يجوز أن يكون السطر مديناً ودائناً في آنٍ واحد.");
        if (debit == 0 && credit == 0)
            throw new AccountingException("سطر القيد يجب أن يحمل مبلغاً مديناً أو دائناً.");
        EnsureHalalaScale(debit, credit);

        var currency = NormalizeCurrencyMemo(currencyCode, exchangeRate, foreignAmount, debit + credit);

        _lines.Add(new JournalLine(account.Id, account.Code, debit, credit, lineDescription?.Trim(), costCenterId,
            currency.Code, currency.Rate, currency.Amount));
    }

    /// <summary>
    /// يتحقّق من مذكرة العملة ويُطبّعها — <b>أو يرفضها</b>.
    /// </summary>
    /// <remarks>
    /// <b>ونصفُ مذكرةٍ مرفوضٌ كذلك</b>: عملةٌ بلا سعرٍ لا تُترجم، وسعرٌ بلا عملةٍ لا يعني
    /// شيئاً. <b>وسكوتٌ عن النصف يُنتج سطراً يبدو موثَّقاً وليس كذلك</b>.
    /// </remarks>
    private static (string? Code, decimal? Rate, decimal? Amount) NormalizeCurrencyMemo(
        string? currencyCode, decimal? exchangeRate, decimal? foreignAmount, decimal functionalAmount)
    {
        var code = currencyCode?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(code))
        {
            if (exchangeRate is not null || foreignAmount is not null)
                throw new AccountingException(
                    "سعرُ الصرف أو المبلغ الأجنبيّ بلا رمز عملة — والمذكرةُ الناقصة لا تُقرأ.");

            return (null, null, null);
        }

        if (code.Length != 3)
            throw new AccountingException($"رمزُ العملة '{code}' غيرُ صالح — ثلاثةُ أحرفٍ حسب ISO 4217.");
        if (exchangeRate is not > 0m)
            throw new AccountingException($"سعرُ صرف {code} يجب أن يكون موجباً.");
        if (foreignAmount is not > 0m)
            throw new AccountingException($"المبلغُ بعملة {code} يجب أن يكون موجباً.");

        // ‏هـ-٢٥: المُرحَّلُ هو المترجَم — **لا رقمٌ ثالثٌ مستقلّ**.
        var translated = Math.Round(foreignAmount.Value * exchangeRate.Value, 2, MidpointRounding.AwayFromZero);
        if (translated != functionalAmount)
            throw new AccountingException(
                $"المبلغُ المُرحَّل {functionalAmount:N2} لا يساوي ترجمةَ {foreignAmount:N2} {code} "
                + $"بسعر {exchangeRate:N6} وهي {translated:N2} — والمذكرةُ التي تخالف القيدَ أسوأُ من غيابها.");

        return (code, Math.Round(exchangeRate.Value, 6, MidpointRounding.AwayFromZero),
            Math.Round(foreignAmount.Value, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// سطرٌ في قيدٍ نظاميّ (إقفال سنويّ) — يقبل الحساب <b>الموقوف</b>.
    ///
    /// <para>(CEEF-229 · م0-١٣) الإقفال إجراءٌ نظاميّ لا ترحيلٌ تشغيليّ: حسابٌ أُوقف وعليه رصيد
    /// <b>يجب</b> أن يُصفَّر في قيد الإقفال، وإلا بقي رصيده معلّقاً واستحال الإقفال إلى الأبد.
    /// ومنعُ الترحيل التشغيليّ عليه يبقى قائماً في <see cref="AddLine"/>.</para>
    ///
    /// <para>ويبقى الحساب <b>التجميعيّ</b> مرفوضاً هنا أيضاً: الإيقاف حالةٌ عارضة، أمّا التجميع
    /// فبنيةٌ في الدليل لا تقبل رصيداً بحال.</para>
    /// </summary>
    public void AddClosingLine(Account account, decimal debit, decimal credit, string? lineDescription = null)
    {
        EnsureDraft("إضافة سطر إقفال");

        if (account.IsGroup)
            throw new AccountingException($"الحساب '{account.NameAr}' تجميعي — لا يقبل رصيداً.");
        if (debit < 0 || credit < 0)
            throw new AccountingException("لا يجوز إدخال مبالغ سالبة — استخدم الطرف المقابل (مدين/دائن).");
        if (debit > 0 && credit > 0)
            throw new AccountingException("لا يجوز أن يكون السطر مديناً ودائناً في آنٍ واحد.");
        if (debit == 0 && credit == 0)
            throw new AccountingException("سطر القيد يجب أن يحمل مبلغاً مديناً أو دائناً.");
        EnsureHalalaScale(debit, credit);

        _lines.Add(new JournalLine(account.Id, account.Code, debit, credit, lineDescription?.Trim(), null));
    }

    /// <summary>
    /// ترحيل القيد: يتحقق من التوازن ويمنح الرقم التسلسلي، وبعده يصبح القيد غير قابل للتعديل.
    /// </summary>
    public void Post(string entryNumber)
    {
        EnsureDraft("الترحيل");

        if (_lines.Count < 2)
            throw new AccountingException("القيد يجب أن يحتوي على سطرين على الأقل (طرف مدين وطرف دائن).");
        if (!IsBalanced)
            throw new AccountingException(
                $"القيد غير متوازن: مجموع المدين {TotalDebit:N2} لا يساوي مجموع الدائن {TotalCredit:N2}.");
        if (string.IsNullOrWhiteSpace(entryNumber))
            throw new AccountingException("الرقم التسلسلي للقيد مطلوب عند الترحيل.");

        EntryNumber = entryNumber.Trim();
        Status = JournalEntryStatus.Posted;
    }

    /// <summary>
    /// إنشاء قيد عكسي يلغي أثر هذا القيد — الطريقة الوحيدة لتصحيح قيد مرحّل.
    /// </summary>
    public JournalEntry CreateReversal(DateOnly reversalDate)
    {
        if (Status != JournalEntryStatus.Posted)
            throw new AccountingException("لا يُعكس إلا قيد مرحّل.");
        if (reversalDate < EntryDate)
            throw new AccountingException("تاريخ القيد العكسي لا يسبق تاريخ القيد الأصلي.");

        var reversal = new JournalEntry(CompanyId, reversalDate, $"قيد عكسي للقيد رقم {EntryNumber}", SourceDocument);
        foreach (var line in _lines)
        {
            reversal._lines.Add(new JournalLine(
                line.AccountId, line.AccountCode,
                Debit: line.Credit, Credit: line.Debit,
                line.Description, line.CostCenterId));
        }

        Status = JournalEntryStatus.Reversed;
        return reversal;
    }

    public void LinkReversalEntry(long reversalEntryId) => ReversalEntryId = reversalEntryId;

    private void EnsureDraft(string operation)
    {
        if (Status != JournalEntryStatus.Draft)
            throw new AccountingException(
                $"{operation} غير مسموح: القيد {(Status == JournalEntryStatus.Posted ? "مرحّل" : "معكوس")} — التصحيح يكون بقيد عكسي.");
    }
}

/// <summary>
/// سطر في قيد اليومية — طرف واحد فقط (مدين أو دائن).
///
/// <para>(CEEF-231 · O-34) <b>و<c>Debit</c>/<c>Credit</c> بالعملة الوظيفية دائماً</b> —
/// فالدفترُ النظاميُّ بالريال، <b>ولا يُقاس ميزانٌ بعملتين</b>. والحقولُ الثلاثةُ أدناه
/// <b>مذكرةٌ تحفظ الأصل</b>: بأيّ عملةٍ وقعت المعاملة، وبأيّ سعرٍ تُرجمت.</para>
///
/// <para><b>وبغيرها يُكتب مبلغُ الريال وحدَه</b>، فيستحيل بعد شهرٍ معرفةُ أنّ الألفَ
/// كانت مئتي دولارٍ بسعر ٣٫٧٥ — <b>ولا يُراجَع سعرٌ لا أثرَ له</b>.</para>
/// </summary>
/// <param name="CurrencyCode">رمزُ عملة المعاملة — <c>null</c> يعني العملةَ الوظيفية.</param>
/// <param name="ExchangeRate">سعرُ الصرف المستعمَل في الترجمة — <b>محفوظٌ لا مُعاد حسابه</b>.</param>
/// <param name="ForeignAmount">المبلغُ بعملة المعاملة — <b>موجبٌ دائماً</b>، والطرفُ يُقرأ من المدين/الدائن.</param>
public record JournalLine(
    long AccountId,
    string AccountCode,
    decimal Debit,
    decimal Credit,
    string? Description,
    long? CostCenterId,
    string? CurrencyCode = null,
    decimal? ExchangeRate = null,
    decimal? ForeignAmount = null);
