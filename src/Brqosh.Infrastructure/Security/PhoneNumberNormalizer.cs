using System.Text.RegularExpressions;

namespace Brqosh.Infrastructure.Security;

public static class PhoneNumberNormalizer
{
    private static readonly Regex DigitsOnly = new(@"\D", RegexOptions.Compiled);

    public static bool TryNormalizeSaudi(string input, out string e164, out string? error)
    {
        e164 = "";
        error = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            error = "رقم الجوال مطلوب.";
            return false;
        }

        var digits = DigitsOnly.Replace(input.Trim(), "");
        if (digits.StartsWith("966", StringComparison.Ordinal))
            digits = digits[3..];
        if (digits.StartsWith('0'))
            digits = digits[1..];

        if (digits.Length != 9 || !digits.StartsWith('5'))
        {
            error = "أدخل رقم جوال سعودي صالح (مثال: 05xxxxxxxx).";
            return false;
        }

        e164 = $"+966{digits}";
        return true;
    }

    public static string Mask(string phoneE164)
    {
        if (phoneE164.Length < 6)
            return "***";
        return $"{phoneE164[..4]}***{phoneE164[^2..]}";
    }
}
