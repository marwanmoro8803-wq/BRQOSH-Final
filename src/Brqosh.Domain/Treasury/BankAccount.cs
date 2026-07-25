using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Treasury;

/// <summary>
/// حساب بنكي تشغيلي — مرتبط بحساب فرعي في دليل الحسابات تحت 1102 (النقدية لدى البنوك).
/// </summary>
public class BankAccount : IBranchScoped, ISoftDeletable
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public string BankName { get; private set; }
    public string AccountNumber { get; private set; }
    public string Iban { get; private set; }
    public long GlAccountId { get; private set; }
    public string CurrencyCode { get; private set; } = "SAR";
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? DeletedAtUtc { get; private set; }

    private BankAccount()
    {
        Code = null!;
        NameAr = null!;
        BankName = null!;
        AccountNumber = null!;
        Iban = null!;
    }

    private BankAccount(long companyId, string code, string nameAr, string bankName,
        string accountNumber, string iban, long glAccountId, bool isDefault)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        BankName = bankName;
        AccountNumber = accountNumber;
        Iban = iban;
        GlAccountId = glAccountId;
        CurrencyCode = "SAR";
        IsDefault = isDefault;
    }

    public static BankAccount Create(long companyId, string code, string nameAr, string bankName,
        string accountNumber, string iban, long glAccountId, bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود الحساب البنكي مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم الحساب البنكي مطلوب.");
        if (string.IsNullOrWhiteSpace(bankName))
            throw new AccountingException("اسم البنك مطلوب.");
        if (glAccountId <= 0)
            throw new AccountingException("حساب الأستاذ العام للبنك مطلوب.");

        return new BankAccount(companyId, code.Trim().ToUpperInvariant(), nameAr.Trim(), bankName.Trim(),
            (accountNumber ?? "").Trim(), (iban ?? "").Trim().ToUpperInvariant(), glAccountId, isDefault);
    }

    public void Update(string nameAr, string bankName, string accountNumber, string iban, bool isDefault)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم الحساب البنكي مطلوب.");
        if (string.IsNullOrWhiteSpace(bankName))
            throw new AccountingException("اسم البنك مطلوب.");

        NameAr = nameAr.Trim();
        BankName = bankName.Trim();
        AccountNumber = (accountNumber ?? string.Empty).Trim();
        Iban = (iban ?? string.Empty).Trim().ToUpperInvariant();
        IsDefault = isDefault;
    }

    public void SetCurrency(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 3 || !code.Trim().All(char.IsLetter))
            throw new AccountingException("كود العملة يجب أن يكون 3 حروف (مثل SAR أو USD).");
        CurrencyCode = code.Trim().ToUpperInvariant();
    }

    public void ClearDefault() => IsDefault = false;
    public void SetDefault() => IsDefault = true;
    public void Deactivate()
    {
        if (DeletedAtUtc is not null) return;
        DeletedAtUtc = DateTime.UtcNow;
        IsActive = false;
    }
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
