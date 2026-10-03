namespace Brqosh.Desktop.Ui;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٩٦) <b>قراءةُ مهلة السداد من حقلها</b> — للعميل والمورد معاً، فلا تفترق النافذتان في معنى الحقل.
/// <b>وفارغُه صفرٌ — نقداً</b> كما كان كلُّ عميلٍ ومورّدٍ قبله؛ وغيرُ العدد الصحيح يُقال باسمه. <b>وحدُّ السنة في المجال</b>
/// (<c>SetPaymentTerms</c>) لا هنا — فلا يُكرَّر الحارس.
/// </summary>
public static class PaymentTermsInput
{
    public static int Parse(string? text)
    {
        var trimmed = (text ?? "").Trim();
        if (trimmed.Length == 0)
            return 0;

        if (!NumericInput.TryParseNonNegative(trimmed, out var days) || days != decimal.Truncate(days) || days > int.MaxValue)
            throw new ArgumentException("مهلة السداد يجب أن تكون عدداً صحيحاً من الأيام.");

        return (int)days;
    }
}
