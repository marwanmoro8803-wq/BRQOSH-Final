using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Treasury;

public enum ChequeType
{
    /// <summary>شيك وارد من عميل.</summary>
    Received = 0,

    /// <summary>شيك صادر لمورد.</summary>
    Issued = 1,
}

public enum ChequeStatus
{
    /// <summary>في الحافظة / صادر ولم يُصرف بعد.</summary>
    InPortfolio = 0,

    /// <summary>وارد فقط — مُودَع بالبنك ولم يُحصَّل.</summary>
    Deposited = 1,

    /// <summary>مُحصَّل / مُصرف.</summary>
    Cleared = 2,

    /// <summary>مرتجع / مرفوض.</summary>
    Bounced = 3,

    /// <summary>ملغى قبل التحصيل.</summary>
    Cancelled = 4,
}

/// <summary>
/// شيك وارد أو صادر — دورة حياة كاملة مع قيود آلية على 1103/2103.
/// </summary>
public class Cheque : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public ChequeType Type { get; private set; }
    public ChequeStatus Status { get; private set; } = ChequeStatus.InPortfolio;

    public string ChequeNumber { get; private set; }
    public DateOnly ChequeDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; }

    public long BankAccountId { get; private set; }
    public long? CustomerId { get; private set; }
    public long? SupplierId { get; private set; }

    public long? PortfolioJournalEntryId { get; private set; }
    public long? SettlementJournalEntryId { get; private set; }
    public long? SecondarySettlementJournalEntryId { get; private set; }

    private Cheque()
    {
        ChequeNumber = null!;
        Description = null!;
    }

    private Cheque(long companyId, ChequeType type, string chequeNumber, DateOnly chequeDate,
        decimal amount, string description, long bankAccountId, DateOnly? dueDate,
        long? customerId, long? supplierId)
    {
        CompanyId = companyId;
        Type = type;
        ChequeNumber = chequeNumber;
        ChequeDate = chequeDate;
        Amount = amount;
        Description = description;
        BankAccountId = bankAccountId;
        DueDate = dueDate;
        CustomerId = customerId;
        SupplierId = supplierId;
    }

    public static Cheque CreateReceived(long companyId, string chequeNumber, DateOnly chequeDate,
        decimal amount, string description, long bankAccountId, long customerId, DateOnly? dueDate = null)
    {
        ValidateCommon(chequeNumber, amount, description, bankAccountId);
        if (customerId <= 0)
            throw new AccountingException("العميل مطلوب للشيك الوارد.");

        return new Cheque(companyId, ChequeType.Received, chequeNumber.Trim(), chequeDate,
            Math.Round(amount, 2, MidpointRounding.AwayFromZero), description.Trim(),
            bankAccountId, dueDate, customerId, null);
    }

    public static Cheque CreateIssued(long companyId, string chequeNumber, DateOnly chequeDate,
        decimal amount, string description, long bankAccountId, long supplierId, DateOnly? dueDate = null)
    {
        ValidateCommon(chequeNumber, amount, description, bankAccountId);
        if (supplierId <= 0)
            throw new AccountingException("المورد مطلوب للشيك الصادر.");

        return new Cheque(companyId, ChequeType.Issued, chequeNumber.Trim(), chequeDate,
            Math.Round(amount, 2, MidpointRounding.AwayFromZero), description.Trim(),
            bankAccountId, dueDate, null, supplierId);
    }

    /// <summary>تسجيل الشيك في الحافظة: وارد مدين 1103 دائن عملاء / صادر مدين موردون دائن 2103.</summary>
    public JournalEntry PostToPortfolio(string docLabel, Account debitAccount, Account creditAccount)
    {
        EnsureStatus(ChequeStatus.InPortfolio, "تسجيل الشيك");
        if (PortfolioJournalEntryId is not null)
            throw new AccountingException("الشيك مسجّل مسبقاً في الحافظة.");

        var entry = JournalEntry.CreateDraft(
            CompanyId, ChequeDate,
            $"{docLabel} — شيك رقم {ChequeNumber}",
            sourceDocument: $"Cheque:{ChequeNumber}");

        entry.AddLine(debitAccount, Amount, 0m, Description);
        entry.AddLine(creditAccount, 0m, Amount, Description);
        return entry;
    }

    /// <summary>
    /// <b>تاريخُ الحدث يُمرّر — ولا يُشتقّ من
    /// <c>ChequeDate</c>.</b>
    ///
    /// <para>كانت قيودُ الدورة الخمسة تُبنى بتاريخ الشيك نفسِه، <b>والخدمةُ
    /// تفحص الفترةَ بالتاريخ الذي أدخلَه المستخدم</b>. <b>فالحارسُ يقيس
    /// تاريخاً لا يبلغ الدفتر أبداً.</b></para>
    ///
    /// <para><b>وقُيس</b>: شيكٌ بتاريخ 2026-04-05، <b>ثمّ أُقفِلت فترةُ أبريل</b>،
    /// ثمّ أُودِع في 2026-07-10 (فترةٌ مفتوحة) ⟵ <b>قُبِل، والقيدُ دخل
    /// <c>2026-04-05</c> وهي <c>Closed</c></b>. فليس العطلُ تاريخاً خاطئاً فحسب —
    /// <b>بل نفاذٌ من حارس الفترة المقفلة</b>، وهو الضمانُ الذي بُني
    /// لأجله ح-١.
    ///
    /// <para><b>وتاريخُ الشيك ليس تاريخَ حدثِه</b>: شيكٌ مؤجّلٌ يُستلم في
    /// شهرٍ ويُودَع في آخر ويُحصّل في ثالث — <b>ومطابقةُ كشف البنك
    /// تبحث عن حركةِ الشهر الذي وقعت فيه</b>.</para>
    /// </summary>
    public JournalEntry PostDeposit(DateOnly depositDate, Account bankGlAccount, Account chequesUnderCollection)
    {
        if (Type != ChequeType.Received)
            throw new AccountingException("الإيداع للشيكات الواردة فقط.");
        EnsureStatus(ChequeStatus.InPortfolio, "إيداع الشيك");

        var entry = BuildSettlementEntry(depositDate,
            $"إيداع شيك رقم {ChequeNumber}",
            bankGlAccount, chequesUnderCollection);
        Status = ChequeStatus.Deposited;
        return entry;
    }

    /// <summary>تحصيل شيك وارد أو صرف شيك صادر. يُرجع null إذا كان التحصيل تأكيداً فقط (بعد الإيداع).</summary>
    public JournalEntry? PostClear(DateOnly clearDate, Account bankGlAccount, Account clearingAccount)
    {
        if (Type == ChequeType.Received)
        {
            if (Status is not ChequeStatus.InPortfolio and not ChequeStatus.Deposited)
                throw new AccountingException("لا يمكن تحصيل الشيك في حالته الحالية.");

            if (Status == ChequeStatus.Deposited)
            {
                Status = ChequeStatus.Cleared;
                return null;
            }

            var entry = BuildSettlementEntry(clearDate,
                $"تحصيل شيك رقم {ChequeNumber}",
                bankGlAccount, clearingAccount);
            Status = ChequeStatus.Cleared;
            return entry;
        }

        EnsureStatus(ChequeStatus.InPortfolio, "صرف الشيك الصادر");
        var clearEntry = BuildSettlementEntry(clearDate,
            $"صرف شيك رقم {ChequeNumber}",
            clearingAccount, bankGlAccount);
        Status = ChequeStatus.Cleared;
        return clearEntry;
    }

    /// <summary>ارتجاع شيك وارد أو إلغاء شيك صادر قبل الصرف.</summary>
    public JournalEntry PostBounceOrCancel(DateOnly eventDate, Account debitAccount, Account creditAccount, bool isCancel)
    {
        if (Type == ChequeType.Issued && isCancel)
        {
            EnsureStatus(ChequeStatus.InPortfolio, "إلغاء الشيك");
            Status = ChequeStatus.Cancelled;
            return BuildSettlementEntry(eventDate,
                $"إلغاء شيك رقم {ChequeNumber}",
                debitAccount, creditAccount);
        }

        if (Type == ChequeType.Issued)
            throw new AccountingException("إلغاء الشيك الصادر مسموح فقط قبل الصرف.");

        if (Status is ChequeStatus.Cancelled or ChequeStatus.Bounced)
            throw new AccountingException("لا يمكن ارتجاع الشيك في حالته الحالية.");

        var wasCreditedToBank = Status is ChequeStatus.Deposited or ChequeStatus.Cleared;
        Status = ChequeStatus.Bounced;
        return BuildSettlementEntry(eventDate,
            $"ارتجاع شيك رقم {ChequeNumber}",
            debitAccount, creditAccount, allowSecondary: wasCreditedToBank);
    }

    public void LinkPortfolioJournal(long journalEntryId) => PortfolioJournalEntryId = journalEntryId;
    public void LinkSettlementJournal(long journalEntryId) => SettlementJournalEntryId = journalEntryId;
    public void LinkSecondarySettlementJournal(long journalEntryId) => SecondarySettlementJournalEntryId = journalEntryId;

    private JournalEntry BuildSettlementEntry(DateOnly date, string description,
        Account debitAccount, Account creditAccount, bool allowSecondary = false)
    {
        if (SettlementJournalEntryId is null)
            return CreateMovementEntry(date, description, debitAccount, creditAccount);

        if (allowSecondary && SecondarySettlementJournalEntryId is null)
            return CreateMovementEntry(date, description, debitAccount, creditAccount);

        throw new AccountingException("الشيك لديه قيود تسوية كافية مسبقاً.");
    }

    private JournalEntry CreateMovementEntry(DateOnly date, string description,
        Account debitAccount, Account creditAccount)
    {
        var entry = JournalEntry.CreateDraft(
            CompanyId, date, description,
            sourceDocument: $"ChequeSettlement:{ChequeNumber}");

        entry.AddLine(debitAccount, Amount, 0m, Description);
        entry.AddLine(creditAccount, 0m, Amount, Description);
        return entry;
    }

    private void EnsureStatus(ChequeStatus expected, string operation)
    {
        if (Status != expected)
            throw new AccountingException($"{operation} غير مسموح — حالة الشيك الحالية: {StatusLabel(Status)}.");
    }

    private static void ValidateCommon(string chequeNumber, decimal amount, string description, long bankAccountId)
    {
        if (string.IsNullOrWhiteSpace(chequeNumber))
            throw new AccountingException("رقم الشيك مطلوب.");
        if (amount <= 0)
            throw new AccountingException("مبلغ الشيك يجب أن يكون أكبر من صفر.");
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("بيان الشيك مطلوب.");
        if (bankAccountId <= 0)
            throw new AccountingException("الحساب البنكي مطلوب.");
    }

    public static string StatusLabel(ChequeStatus status) => status switch
    {
        ChequeStatus.InPortfolio => "في الحافظة",
        ChequeStatus.Deposited => "مُودَع",
        ChequeStatus.Cleared => "مُحصَّل",
        ChequeStatus.Bounced => "مرتجع",
        ChequeStatus.Cancelled => "ملغاة",
        _ => status.ToString(),
    };

    public static string TypeLabel(ChequeType type) => type == ChequeType.Received ? "وارد" : "صادر";
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
