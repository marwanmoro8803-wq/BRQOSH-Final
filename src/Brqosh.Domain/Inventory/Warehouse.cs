using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Inventory;

/// <summary>مستودع تخزين — كل حركة مخزنية مرتبطة بمستودع.</summary>
public class Warehouse : IBranchScoped, ISoftDeletable
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? DeletedAtUtc { get; private set; }

    // EF Core
    private Warehouse()
    {
        Code = null!;
        NameAr = null!;
    }

    private Warehouse(long companyId, string code, string nameAr, bool isDefault)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        IsDefault = isDefault;
    }

    public static Warehouse Create(long companyId, string code, string nameAr, bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود المستودع مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم المستودع مطلوب.");

        return new Warehouse(companyId, code.Trim().ToUpperInvariant(), nameAr.Trim(), isDefault);
    }

    public void Update(string nameAr) => NameAr = nameAr.Trim();
    public void SetAsDefault() => IsDefault = true;
    public void ClearDefault() => IsDefault = false;
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
