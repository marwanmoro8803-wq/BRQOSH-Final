using Brqosh.Domain.Shared;
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
        if (!code.All(AsciiDigits.Is))
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

    /// <summary>
    /// إيقاف الحساب عن الترحيل الجديد — <b>وليس حذفه</b>.
    ///
    /// <para>(CEEF-229 · م0-١٣) كانت هذه الدالة تضبط <c>DeletedAtUtc</c> أيضاً، أي أن «إيقاف حساب»
    /// كان <b>حذفاً</b>. والأثر لم يكن اختفاءه من القوائم فحسب: الحساب يسقط خلف فلتر الحذف العام،
    /// فيرمي <c>YearEndCloseService.AccountAsync</c> استثناءً خاماً عند أوّل إقفالٍ سنويّ يشمله —
    /// و<b>لا مخرج</b>، إذ لم يكن للحساب دالة تُعيد تفعيله أصلاً (بخلاف <c>AppUser</c>). فمنشأةٌ
    /// أوقفت حساباً عليه حركة كانت تفقد الإقفال السنويّ إلى الأبد إلا بتعديل قاعدة الإنتاج يدوياً.</para>
    ///
    /// <para>الفصل الآن صريح: الإيقاف يمنع الترحيل الجديد ويُبقي الحساب مرئياً وقابلاً للإقفال،
    /// والحذف الناعم شيءٌ آخر لا تفعله هذه الدالة.</para>
    /// </summary>
    public void Deactivate()
    {
        if (IsSystem)
            throw new AccountingException($"الحساب النظامي '{NameAr}' لا يمكن إيقافه.");
        IsActive = false;
    }

    /// <summary>
    /// إعادة تفعيل حساب موقوف. غيابُها كان يجعل الإيقاف قراراً بلا رجعة — ولا مبرّر لذلك:
    /// الإيقاف حالةٌ تشغيلية لا واقعةٌ محاسبية.
    /// </summary>
    public void Reactivate() => IsActive = true;

    public void Rename(string nameAr, string nameEn)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم الحساب العربي مطلوب.");
        NameAr = nameAr.Trim();
        NameEn = (nameEn ?? string.Empty).Trim();
    }
}
