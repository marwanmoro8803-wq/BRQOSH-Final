namespace Brqosh.Domain.Accounting;

/// <summary>
/// (دفاترٌ بغير الريال) <b>قاعدةُ العملة والسعر</b> المشتركة بين المستندات: سعرُ الصرف هو «كم وحدةً من عملة الدفاتر في وحدةٍ من عملة المستند».
/// فعملةُ المستند = عملةُ الدفاتر ⇒ السعرُ <c>1</c> بالضرورة، وعملتان مختلفتان ⇒ لا يصحّ السعرُ <c>1</c> (وإلّا فُقد إفصاحُ العملة الأجنبية).
/// </summary>
public static class BooksCurrency
{
    public static void Validate(string currencyCode, decimal exchangeRate, string? booksCurrencyCode)
    {
        if (string.IsNullOrWhiteSpace(booksCurrencyCode))
            return;

        var same = string.Equals(currencyCode.Trim(), booksCurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase);
        if (same && exchangeRate != 1m)
            throw new AccountingException("مستندٌ بعملة الدفاتر نفسِها سعرُ صرفه ١.");
        if (!same && exchangeRate == 1m)
            throw new AccountingException(
                $"عملةُ المستند ({currencyCode.Trim().ToUpperInvariant()}) تخالف عملةَ الدفاتر ({booksCurrencyCode.Trim().ToUpperInvariant()}) فلا يصحّ سعرُ الصرف ١ — أدخل السعرَ الفعليّ.");
    }
}
