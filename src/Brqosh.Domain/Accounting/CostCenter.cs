namespace Brqosh.Domain.Accounting;

/// <summary>مركز تكلفة — يُربط ببنود القيود لتحليل المصروفات والإيرادات.</summary>
public class CostCenter : ISoftDeletable
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? DeletedAtUtc { get; private set; }

    private CostCenter()
    {
        Code = null!;
        NameAr = null!;
    }

    private CostCenter(long companyId, string code, string nameAr)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
    }

    public static CostCenter Create(long companyId, string code, string nameAr)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("كود مركز التكلفة مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم مركز التكلفة مطلوب.");

        return new CostCenter(companyId, code.Trim().ToUpperInvariant(), nameAr.Trim());
    }

    public void Update(string nameAr)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم مركز التكلفة مطلوب.");
        NameAr = nameAr.Trim();
    }

    public void Deactivate()
    {
        if (DeletedAtUtc is not null) return;
        DeletedAtUtc = DateTime.UtcNow;
        IsActive = false;
    }
}
