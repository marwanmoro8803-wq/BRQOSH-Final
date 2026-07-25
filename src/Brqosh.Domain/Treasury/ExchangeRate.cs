using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Treasury;

/// <summary>سعر صرف مخزّن — وحدات العملة الهدف لكل 1 وحدة من العملة المصدر.</summary>
public class ExchangeRate
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string FromCurrencyCode { get; private set; }
    public string ToCurrencyCode { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public decimal Rate { get; private set; }
    public string? Source { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ExchangeRate()
    {
        FromCurrencyCode = null!;
        ToCurrencyCode = null!;
    }

    public static ExchangeRate Create(
        long companyId,
        string fromCurrencyCode,
        string toCurrencyCode,
        DateOnly effectiveDate,
        decimal rate,
        string? source = null)
    {
        if (companyId <= 0)
            throw new AccountingException("معرّف الشركة مطلوب.");
        if (rate <= 0)
            throw new AccountingException("سعر الصرف يجب أن يكون موجباً.");

        var from = NormalizeCurrency(fromCurrencyCode);
        var to = NormalizeCurrency(toCurrencyCode);
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            throw new AccountingException("عملة المصدر والهدف متطابقتان — لا حاجة لسعر صرف.");

        return new ExchangeRate
        {
            CompanyId = companyId,
            FromCurrencyCode = from,
            ToCurrencyCode = to,
            EffectiveDate = effectiveDate,
            Rate = Math.Round(rate, 6, MidpointRounding.AwayFromZero),
            Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    public static string NormalizeCurrency(string code)
    {
        var c = (code ?? "").Trim().ToUpperInvariant();
        if (c.Length != 3 || !c.All(char.IsLetter))
            throw new AccountingException("رمز العملة يجب أن يكون ISO 4217 من 3 أحرف.");
        return c;
    }
}
