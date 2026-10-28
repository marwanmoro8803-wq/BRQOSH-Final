using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
namespace Brqosh.Domain.Treasury;

/// <summary>قيد بنكي مُسوّى مع كشف الحساب البنكي.</summary>
public class ReconciledBankEntry : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long BankAccountId { get; private set; }
    public long JournalEntryId { get; private set; }

    /// <summary>
    /// ‏ <b>سطرُ القيد المُسوّى</b> — والصفرُ تسويةُ القيد كلِّه
    /// ‏كما كانت قبلُ (السجلّاتُ القائمة). <b>وصفرٌ لا NULL عمداً</b>: فالفهرسُ الفريدُ يمنع التكرارَ للحالين — وNULLان لا يتصادمان.
    /// </summary>
    public long JournalLineId { get; private set; }
    public DateOnly ReconciledDate { get; private set; }

    private ReconciledBankEntry() { }

    private ReconciledBankEntry(long companyId, long bankAccountId, long journalEntryId, long journalLineId, DateOnly reconciledDate)
    {
        CompanyId = companyId;
        BankAccountId = bankAccountId;
        JournalEntryId = journalEntryId;
        JournalLineId = journalLineId;
        ReconciledDate = reconciledDate;
    }

    public static ReconciledBankEntry Create(
        long companyId, long bankAccountId, long journalEntryId, DateOnly reconciledDate, long journalLineId = 0)
    {
        if (bankAccountId <= 0 || journalEntryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(bankAccountId));
        if (journalLineId < 0)
            throw new ArgumentOutOfRangeException(nameof(journalLineId));

        return new ReconciledBankEntry(companyId, bankAccountId, journalEntryId, journalLineId, reconciledDate);
    }
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
