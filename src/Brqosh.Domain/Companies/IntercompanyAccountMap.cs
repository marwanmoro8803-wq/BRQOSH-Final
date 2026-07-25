using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Companies;

/// <summary>حسابات دليل موحّدة تُزال معاملاتها بين شركات المجموعة عند التجميع.</summary>
public class IntercompanyAccountMap
{
    public long Id { get; private set; }
    public long CompanyGroupId { get; private set; }
    public string AccountCode { get; private set; }

    private IntercompanyAccountMap()
    {
        AccountCode = null!;
    }

    public static IntercompanyAccountMap Create(long companyGroupId, string accountCode)
    {
        if (companyGroupId <= 0)
            throw new AccountingException("معرّف المجموعة مطلوب.");
        var code = (accountCode ?? "").Trim();
        if (code.Length == 0 || !code.All(char.IsDigit))
            throw new AccountingException("رمز الحساب يجب أن يكون أرقاماً فقط.");

        return new IntercompanyAccountMap
        {
            CompanyGroupId = companyGroupId,
            AccountCode = code,
        };
    }
}
