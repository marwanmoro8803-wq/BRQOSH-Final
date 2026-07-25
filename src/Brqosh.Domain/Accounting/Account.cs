namespace Brqosh.Domain.Accounting;

/// <summary>
/// حساب في دليل الحسابات (شجرة الحسابات).
/// الحسابات الرئيسية (تجميعية) لا تقبل قيوداً مباشرة؛ الترحيل يتم على الحسابات الفرعية فقط.
/// </summary>
public class Account : ISoftDeletable
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }

    /// <summary>رقم الحساب الهرمي (مثل 1101 نقدية بالصندوق تحت 11 الأصول المتداولة).</summary>
    public string Code { get; private set; }

    public string NameAr { get; private set; }
    public string NameEn { get; private set; }
    public AccountType Type { get; private set; }

    /// <summary>الحساب الأب في الشجرة — null للحسابات الجذرية.</summary>
    public long? ParentId { get; private set; }

    /// <summary>
    /// حساب تجميعي (رئيسي) يُستخدم للتبويب فقط — الترحيل عليه ممنوع.
    /// </summary>
    public bool IsGroup { get; private set; }

    /// <summary>حساب نظامي أنشأه النظام (مثل ضريبة القيمة المضافة) — لا يُحذف ولا يُعاد تصنيفه.</summary>
    public bool IsSystem { get; private set; }

    public bool IsActive { get; private set; } = true;
    public DateTime? DeletedAtUtc { get; private set; }

    // EF Core
    private Account()
    {
        Code = null!;
        NameAr = null!;
        NameEn = null!;
    }

    private Account(string code, string nameAr, string nameEn, AccountType type, long? parentId, bool isGroup, bool isSystem)
    {
        Code = code;
        NameAr = nameAr;
        NameEn = nameEn;
        Type = type;
        ParentId = parentId;
        IsGroup = isGroup;
        IsSystem = isSystem;
    }

    public static Account Create(
        string code,
        string nameAr,
        string nameEn,
        AccountType type,
        long? parentId = null,
        bool isGroup = false,
        bool isSystem = false,
        long companyId = 0)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("رقم الحساب مطلوب.");
        if (!code.All(char.IsDigit))
            throw new AccountingException($"رقم الحساب يجب أن يكون أرقاماً فقط: '{code}'.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم الحساب العربي مطلوب.");

        return new Account(code.Trim(), nameAr.Trim(), (nameEn ?? string.Empty).Trim(), type, parentId, isGroup, isSystem)
        {
            CompanyId = companyId,
        };
    }

    /// <summary>هل يقبل هذا الحساب ترحيل قيود مباشرة؟</summary>
    public bool IsPostable => !IsGroup && IsActive;

    public void Deactivate()
    {
        if (IsSystem)
            throw new AccountingException($"الحساب النظامي '{NameAr}' لا يمكن إيقافه.");
        if (DeletedAtUtc is not null) return;
        DeletedAtUtc = DateTime.UtcNow;
        IsActive = false;
    }

    public void Rename(string nameAr, string nameEn)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم الحساب العربي مطلوب.");
        NameAr = nameAr.Trim();
        NameEn = (nameEn ?? string.Empty).Trim();
    }
}
