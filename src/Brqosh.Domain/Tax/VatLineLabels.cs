using System.Globalization;

namespace Brqosh.Domain.Tax;

/// <summary>
/// نصوصُ سطور الضريبة في القيود — <b>تُشتقّ من النسبة المستعملة في الحساب نفسِه</b> لا من «15%» مكتوبةً حرفاً،
/// فإن تغيّرت النسبةُ (أو السلطةُ في <see cref="VatJurisdictionProfiles"/>) تبعها النصُّ ولم يكذب القيد.
/// </summary>
public static class VatLineLabels
{
    public static string Percent(decimal rate) =>
        string.Create(CultureInfo.InvariantCulture, $"{rate * 100m:0.##}%");

    public static string Output(decimal rate) => $"ضريبة القيمة المضافة {Percent(rate)}";

    public static string Input(decimal rate) => $"ضريبة مدخلات قابلة للخصم {Percent(rate)}";
}
