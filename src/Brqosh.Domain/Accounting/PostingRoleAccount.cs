using System.Reflection;

namespace Brqosh.Domain.Accounting;

/// <summary>
/// ‏حسابُ دورِ ترحيلٍ لكلّ منشأة (دور ← رمز حساب): يُعبَّأ من <see cref="SaudiChartOfAccounts.PostingRoleCodes"/>
/// ‏عند إنشاء الدليل، ويُعدَّل من شاشة الإعداد. ‏وما لا صفَّ له يُرحَّل على الرمز الافتراضيّ كما كان.
/// </summary>
public class PostingRoleAccount
{
    /// <summary>‏الأدوارُ وأرقامُها الافتراضيّة — اسمُ الثابت هو اسمُ الدور.</summary>
    public static IReadOnlyDictionary<string, string> Defaults { get; } =
        typeof(SaudiChartOfAccounts.PostingRoleCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!, StringComparer.Ordinal);

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Role { get; private set; } = "";
    public string AccountCode { get; private set; } = "";

    private PostingRoleAccount() { }

    public static PostingRoleAccount Create(long companyId, string role, string accountCode)
    {
        if (!Defaults.ContainsKey(role))
            throw new AccountingException($"دورُ الترحيل '{role}' غير معروف.");
        var entry = new PostingRoleAccount { CompanyId = companyId, Role = role };
        entry.Assign(accountCode);
        return entry;
    }

    public void Assign(string accountCode)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
            throw new AccountingException("رمز الحساب مطلوب.");
        AccountCode = accountCode.Trim();
    }
}
