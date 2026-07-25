using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Platform;

public class BillingPlan
{
    public long Id { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }
    public int MaxCompanies { get; private set; }
    public bool IsActive { get; private set; } = true;

    private BillingPlan()
    {
        Code = null!;
        NameAr = null!;
    }

    public static BillingPlan Create(string code, string nameAr, int maxCompanies = 1)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        if (c.Length == 0)
            throw new AccountingException("رمز الخطة مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم الخطة مطلوب.");
        if (maxCompanies < 1)
            throw new AccountingException("الحد الأقصى للشركات يجب أن يكون ≥ 1.");

        return new BillingPlan
        {
            Code = c,
            NameAr = nameAr.Trim(),
            MaxCompanies = maxCompanies,
        };
    }
}
