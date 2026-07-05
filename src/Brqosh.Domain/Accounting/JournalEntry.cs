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
public class JournalEntry
{
    private readonly List<JournalLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }

    /// <summary>الرقم التسلسلي للقيد ضمن السنة المالية (يُمنح عند الترحيل).</summary>
    public string? EntryNumber { get; private set; }

    public DateOnly EntryDate { get; private set; }
    public string Description { get; private set; }
    public JournalEntryStatus Status { get; private set; } = JournalEntryStatus.Draft;

    /// <summary>مرجع القيد العكسي إذا عُكس هذا القيد.</summary>
    public long? ReversalEntryId { get; private set; }

    /// <summary>المستند المصدر (فاتورة، سند، مستخلص...) الذي ولّد القيد آلياً — null للقيود اليدوية.</summary>
    public string? SourceDocument { get; private set; }

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
    public JournalEntry CreateReversal(DateOnly reversalDate, long reversalEntryIdPlaceholder = 0)
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
        ReversalEntryId = reversalEntryIdPlaceholder;
        return reversal;
    }

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
