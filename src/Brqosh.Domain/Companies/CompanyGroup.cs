using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Companies;

/// <summary>مجموعة شركات — أساس التقارير الموحّدة وFX (مرحلة 7).</summary>
public class CompanyGroup
{
    public long Id { get; private set; }
    public string Code { get; private set; }
    public string NameAr { get; private set; }

    /// <summary>عملة التقرير الموحّد (ISO 4217).</summary>
    public string ReportingCurrencyCode { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private CompanyGroup()
    {
        Code = null!;
        NameAr = null!;
        ReportingCurrencyCode = "SAR";
    }

    public static CompanyGroup Create(string code, string nameAr, string reportingCurrencyCode = "SAR")
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AccountingException("رمز المجموعة مطلوب.");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new AccountingException("اسم المجموعة مطلوب.");

        return new CompanyGroup
        {
            Code = code.Trim().ToUpperInvariant(),
            NameAr = nameAr.Trim(),
            ReportingCurrencyCode = NormalizeCurrency(reportingCurrencyCode),
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    private static string NormalizeCurrency(string code)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        if (c.Length != 3 || !c.All(char.IsLetter))
            throw new AccountingException("رمز عملة التقرير يجب أن يكون ISO 4217 من 3 أحرف.");
        return c;
    }
}
