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
    /// قيدٌ على مستوى المنشأة (كالإقفال السنويّ) لا فرعَ له: الفرعُ بُعدٌ إداريٌّ لا يُقفل،
    /// ووسمُه بفرعٍ يُسند حقوقَ ملكية المنشأة كلَّها إلى فرعِ الجلسة. انظر docs/architecture/adr/0001-journal-entry.md.
    /// </summary>
    public bool IsEntityLevel { get; private set; }

    /// <summary>يُعلن القيدَ على مستوى المنشأة — قبل الحفظ، لأنّ وسمَ الفرع يقع في SaveChanges ولا يُنزع.</summary>
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
    /// يختم الفرعَ إن كان فارغاً ولا يُبدّل فرعاً قائماً. يُقبل بعد الترحيل لأنّ ملءَ حقلٍ فارغٍ ليس تعديلاً
    /// (القيدُ يُرحَّل قبل إضافته إلى السياق)، وفرعٌ أسندته خدمةٌ من مستندها أعلمُ من فرع الجلسة.
    /// </summary>
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
    /// المبالغ بالهللة (منزلتان) عند البناء: PostgreSQL يُقرِّب كلَّ سطرٍ على حدة إلى numeric(18,2)،
    /// فقيدٌ متوازنٌ في الذاكرة بـ10.005 قد يُخزَّن مختلّاً بهللة (وSQLite في الاختبارات لا يُقرِّب فلا يكشفه).
    /// يُرفض ما يتغيّر بالتقريب فقط؛ و10.0000 مقبولة.
    /// </summary>
    private static void EnsureHalalaScale(decimal debit, decimal credit)
    {
        if (debit != decimal.Round(debit, 2) || credit != decimal.Round(credit, 2))
            throw new AccountingException(
                "مبلغ السطر يتجاوز خانتَي الهللة — قرِّب المبلغ قبل الترحيل.");
    }

    /// <summary>
    /// يختم مركزَ التكلفة على سطور الإيرادات والمصروفات (بادئة 4/5) وحدَها — مركزُ التكلفة مفهومُ أداء —
    /// ولا يُبدّل مركزاً ضبطه المُدخِل.
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
    /// يُضيف سطراً — المبلغان بالعملة الوظيفية دائماً. ووسائطُ العملة مذكرةٌ لا تدخل الميزان،
    /// ويجب أن تُنتج المُرحَّل: round(ForeignAmount × ExchangeRate, 2) = Debit + Credit.
    /// </summary>
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

    /// <summary>يتحقّق من مذكرة العملة ويُطبّعها — ونصفُ مذكرةٍ (عملةٌ بلا سعرٍ أو العكس) مرفوض.</summary>
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
    /// ‏ينقل <paramref name="amount"/> من سطور <paramref name="fromAccountId"/> (في الجانب المحدَّد) إلى
    /// <paramref name="to"/> — لحسابات الصنف/الفئة: يبني المُرحِّلُ القيدَ على الحساب العامّ ثمّ يُوجَّه نصيبُ
    /// الأصناف ذات الحساب الخاصّ إليه. ‏والقيدُ متوازنٌ قبلُ وبعدُ لأنّ الجانبَ لا يتغيّر. فرقُ تقريبٍ دون ريالٍ يُقصّ.
    /// </summary>
    public void RerouteAmount(long fromAccountId, Account to, decimal amount, bool debitSide)
    {
        EnsureDraft("توجيه مبلغ إلى حساب الصنف");
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0m || to.Id == fromAccountId) return;
        var source = _lines.Where(l => l.AccountId == fromAccountId && (debitSide ? l.Debit > 0 : l.Credit > 0)).ToList();
        var available = source.Sum(l => debitSide ? l.Debit : l.Credit);
        if (amount > available)
        {
            if (amount - available >= 1m)
                throw new AccountingException("المبلغ الموجَّه إلى حساب الصنف يتجاوز سطر الحساب العامّ في القيد.");
            amount = available;
        }
        foreach (var line in source)
        {
            if (amount <= 0m) break;
            var have = debitSide ? line.Debit : line.Credit;
            var take = Math.Min(have, amount);
            var idx = _lines.IndexOf(line);
            var left = have - take;
            if (left > 0m)
                _lines[idx] = line with
                {
                    Debit = debitSide ? left : 0m, Credit = debitSide ? 0m : left,
                    CurrencyCode = null, ExchangeRate = null, ForeignAmount = null,
                };
            else
                _lines.RemoveAt(idx);
            AddLine(to, debitSide ? take : 0m, debitSide ? 0m : take, line.Description, line.CostCenterId);
            amount -= take;
        }
    }

    /// <summary>
    /// سطرٌ في قيدٍ نظاميّ (إقفال سنويّ) يقبل الحسابَ الموقوف — فرصيدُه يجب أن يُصفَّر في الإقفال،
    /// ومنعُ الترحيل التشغيليّ عليه باقٍ في AddLine. والحسابُ التجميعيّ مرفوضٌ هنا أيضاً.
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

        // العكسُ يرث «على مستوى المنشأة» والفرعَ ووسمَ ما بين الشركات — وإلّا وسمه SaveChanges بفرع الجلسة
        // أو نجا من الاستبعاد عند التوحيد (انظر ADR 0001).
        reversal.IsEntityLevel = IsEntityLevel;

        reversal.BranchId = BranchId;

        reversal.IsIntercompany = IsIntercompany;
        reversal.CounterpartyCompanyId = CounterpartyCompanyId;

        foreach (var line in _lines)
        {
            // مذكرةُ العملة تُنقل كما هي ولا تُعكَس — المعكوسُ هو الطرفُ لا العملة.
            reversal._lines.Add(new JournalLine(
                line.AccountId, line.AccountCode,
                Debit: line.Credit, Credit: line.Debit,
                line.Description, line.CostCenterId,
                line.CurrencyCode, line.ExchangeRate, line.ForeignAmount));
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
/// سطر في قيد اليومية — طرف واحد (مدين أو دائن). Debit/Credit بالعملة الوظيفية دائماً،
/// والحقولُ الثلاثةُ الأخيرة مذكرةٌ تحفظ عملةَ المعاملة وسعرَ ترجمتها.
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
