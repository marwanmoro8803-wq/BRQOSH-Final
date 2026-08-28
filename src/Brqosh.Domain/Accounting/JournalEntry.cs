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

    public void AddLine(Account account, decimal debit, decimal credit, string? lineDescription = null, long? costCenterId = null)
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

        _lines.Add(new JournalLine(account.Id, account.Code, debit, credit, lineDescription?.Trim(), costCenterId));
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

/// <summary>سطر في قيد اليومية — طرف واحد فقط (مدين أو دائن).</summary>
public record JournalLine(
    long AccountId,
    string AccountCode,
    decimal Debit,
    decimal Credit,
    string? Description,
    long? CostCenterId);
