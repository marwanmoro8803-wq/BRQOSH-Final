using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Treasury;

public enum ReceiptMethod
{
    /// <summary>نقداً — يُقيَّد على الصندوق.</summary>
    Cash = 0,

    /// <summary>تحويل بنكي / شبكة — يُقيَّد على البنك.</summary>
    Bank = 1,
}

public enum ReceiptVoucherStatus
{
    Draft = 0,
    Posted = 1,
    Reversed = 2,
}

/// <summary>
/// سند قبض — تحصيل من عميل. القيد الآلي: الصندوق/البنك (مدين) ← العملاء (دائن).
/// </summary>
public class ReceiptVoucher : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public long CustomerId { get; private set; }
    public string? VoucherNumber { get; private set; }
    public DateOnly VoucherDate { get; private set; }
    public decimal Amount { get; private set; }
    public ReceiptMethod Method { get; private set; }
    public string Description { get; private set; }

    /// <summary>مرجع خارجي: رقم الحوالة/الشيك أو مرجع الشبكة.</summary>
    public string Reference { get; private set; }

    public ReceiptVoucherStatus Status { get; private set; } = ReceiptVoucherStatus.Draft;
    public long? JournalEntryId { get; private set; }

    /// <summary>الحساب البنكي عند طريقة القبض بنك — يُقيَّد على حسابه الفرعي في الأستاذ.</summary>
    public long? BankAccountId { get; private set; }

    // EF Core
    private ReceiptVoucher()
    {
        Description = null!;
        Reference = null!;
    }

    private ReceiptVoucher(long companyId, long customerId, DateOnly voucherDate,
        decimal amount, ReceiptMethod method, string description, string reference)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        VoucherDate = voucherDate;
        Amount = amount;
        Method = method;
        Description = description;
        Reference = reference;
    }

    public static ReceiptVoucher CreateDraft(long companyId, long customerId, DateOnly voucherDate,
        decimal amount, ReceiptMethod method, string description, string reference = "",
        long? bankAccountId = null)
    {
        if (customerId <= 0)
            throw new AccountingException("العميل مطلوب لسند القبض.");
        if (amount <= 0)
            throw new AccountingException("مبلغ السند يجب أن يكون أكبر من صفر.");
        if (string.IsNullOrWhiteSpace(description))
            throw new AccountingException("بيان السند مطلوب.");

        var voucher = new ReceiptVoucher(companyId, customerId, voucherDate,
            Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            method, description.Trim(), (reference ?? "").Trim());
        voucher.AssignBankAccount(bankAccountId);
        return voucher;
    }

    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        if (Status != ReceiptVoucherStatus.Draft)
            throw new AccountingException("السند مرحّل — لا يُعدَّل، والتصحيح بقيد عكسي.");
        BranchId = branchId;
    }

    public void AssignBankAccount(long? bankAccountId)
    {
        BankAccountId = Method == ReceiptMethod.Cash ? null : bankAccountId;
    }

    /// <summary>الترحيل: مدين الصندوق/البنك، دائن العملاء.</summary>
    public JournalEntry Post(string voucherNumber, Account cashOrBankAccount, Account receivableAccount)
    {
        if (Status != ReceiptVoucherStatus.Draft)
            throw new AccountingException("السند مرحّل — لا يُعدَّل، والتصحيح بقيد عكسي.");
        if (string.IsNullOrWhiteSpace(voucherNumber))
            throw new AccountingException("رقم السند مطلوب عند الترحيل.");

        var entry = JournalEntry.CreateDraft(
            CompanyId, VoucherDate,
            $"سند قبض رقم {voucherNumber} — {Description}",
            sourceDocument: $"ReceiptVoucher:{voucherNumber}");

        entry.AddLine(cashOrBankAccount, Amount, 0m,
            Method == ReceiptMethod.Cash ? "تحصيل نقدي" : $"تحصيل بنكي {Reference}".Trim());
        entry.AddLine(receivableAccount, 0m, Amount, "تحصيل من ذمة العميل");

        VoucherNumber = voucherNumber.Trim();
        Status = ReceiptVoucherStatus.Posted;
        return entry;
    }

    public void LinkJournalEntry(long journalEntryId) => JournalEntryId = journalEntryId;

    public void MarkReversed()
    {
        if (Status != ReceiptVoucherStatus.Posted)
            throw new AccountingException("لا يُعكس إلا سند قبض مرحّل.");
        Status = ReceiptVoucherStatus.Reversed;
    }
}
