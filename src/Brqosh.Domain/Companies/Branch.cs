using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;

namespace Brqosh.Domain.Companies;

/// <summary>فرع منشأة — أساس multi-branch؛ الكيانات التشغيلية تُربط لاحقاً بـ BranchId اختياري.</summary>
public class Branch
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public bool IsHeadquarters { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    private Branch()
    {
        Code = null!;
        NameAr = null!;
    }

    private Branch(long companyId, string code, string nameAr, bool isHeadquarters)
    {
        CompanyId = companyId;
        Code = code;
        NameAr = nameAr;
        IsHeadquarters = isHeadquarters;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Branch Create(long companyId, string code, string nameAr, bool isHeadquarters = false)
    {
        if (companyId <= 0) throw new AccountingException("المنشأة مطلوبة للفرع.");
        if (string.IsNullOrWhiteSpace(code)) throw new AccountingException("كود الفرع مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr)) throw new AccountingException("اسم الفرع مطلوب.");
        return new Branch(companyId, code.Trim().ToUpperInvariant(), nameAr.Trim(), isHeadquarters);
    }

    public void Deactivate()
    {
        if (IsHeadquarters)
            throw new AccountingException("لا يمكن إيقاف الفرع الرئيسي.");
        IsActive = false;
    }

    public void Activate() => IsActive = true;

    public void MarkAsHeadquarters() => IsHeadquarters = true;

    public void ClearHeadquarters() => IsHeadquarters = false;
}
