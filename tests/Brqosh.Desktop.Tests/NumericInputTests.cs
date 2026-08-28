using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-228 · A-2) حقول المبالغ والكميات — التحليل الذي يرفض ما لا يصحّ.
///
/// العطل الأصليّ: كل حقل ماليّ في البرنامج كان يحلّل بـ
/// <c>decimal.TryParse(text, NumberStyles.Number, InvariantCulture, …)</c>.
/// و<c>AllowThousands</c> يقبل الفاصلة <b>ولا يتحقّق من حجم المجموعة</b> — مقيسٌ على
/// .NET 9: <c>"1,5"</c> ← <b>15</b>، و<c>"1234,5"</c> ← <b>12345</b>. أي أن من يكتب
/// الفاصلة العشرية بالطريقة الأوروبية يُرحَّل له شيكٌ بعشرة أضعاف المبلغ، بلا رسالة خطأ.
///
/// والاتجاه المعاكس: <c>"٥٠٠"</c> كانت تُرفض كليّاً — مستخدمٌ يكتب بلوحة مفاتيح عربية
/// يرى «مبلغ غير صالح» على رقمٍ صحيح كتبه بلغته.
///
/// هذا الحارس يقيس الناتج بالأرقام، ولا يفحص أن الاستدعاء موجود في الملف.
/// </summary>
public class NumericInputTests
{
    // ── ما يجب أن يُرفض: فاصلة في موضع لا تصحّ فيه كفاصلة مجموعات ──────────

    [Theory]
    [InlineData("1,5")]        // كانت 15 — عشرة أضعاف
    [InlineData("1234,5")]     // كانت 12345
    [InlineData("12,34")]      // كانت 1234
    [InlineData("1,5,5")]      // كانت 155
    [InlineData("1,23")]
    [InlineData("1,2345")]
    [InlineData("1234,567,8")]
    public void MisplacedGroupSeparator_IsRejected(string text)
    {
        Assert.False(NumericInput.TryParse(text, out _));
    }

    /// <summary>البرهان الرقميّ الصريح على العطل الذي كان.</summary>
    [Fact]
    public void OnePointFive_IsNeverFifteen()
    {
        var accepted = NumericInput.TryParse("1,5", out var value);

        Assert.False(accepted);
        Assert.NotEqual(15m, value);
    }

    // ── ما يجب أن يُقبل ────────────────────────────────────────────────────

    [Theory]
    [InlineData("1.5", 1.5)]
    [InlineData("1234.5", 1234.5)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("1,234,567.89", 1234567.89)]
    [InlineData("0", 0)]
    [InlineData("0.01", 0.01)]
    [InlineData("-250.75", -250.75)]
    [InlineData("  4312.50  ", 4312.50)]
    public void WellFormedNumbers_ParseExactly(string text, double expected)
    {
        Assert.True(NumericInput.TryParse(text, out var value), $"رُفض نصّ سليم: «{text}»");
        Assert.Equal((decimal)expected, value);
    }

    // ── الأرقام والفواصل العربية ───────────────────────────────────────────

    [Theory]
    [InlineData("٥٠٠", 500)]            // أرقام عربية-هندية — كانت تُرفض
    [InlineData("١٢٣٤٫٥٦", 1234.56)]    // مع الفاصلة العشرية العربية ٫ (U+066B)
    [InlineData("١٬٢٣٤٫٥٦", 1234.56)]   // ومعها فاصلة الآلاف العربية ٬ (U+066C)
    [InlineData("۵۰۰", 500)]            // أرقام فارسية
    public void ArabicDigitsAndSeparators_AreUnderstood(string text, double expected)
    {
        Assert.True(NumericInput.TryParse(text, out var value), $"رُفض رقم عربيّ سليم: «{text}»");
        Assert.Equal((decimal)expected, value);
    }

    /// <summary>الأرقام العربية تخضع لنفس التشدّد — لا مسار متساهل من الباب الخلفيّ.</summary>
    [Fact]
    public void ArabicDigits_WithMisplacedSeparator_AreRejectedToo()
    {
        Assert.False(NumericInput.TryParse("١٬٥", out _));
    }

    // ── اللصق من مستند عربي أو من Excel بواجهة RTL ─────────────────────────

    [Theory]
    [InlineData("⁦" + "1234.56" + "⁩")]  // محرفا العزل LRI/PDI
    [InlineData("‏" + "1234.56")]             // RLM
    [InlineData("1234.56‎")]                  // LRM
    [InlineData("1 234.56")]                  // مسافة غير فاصلة
    public void InvisibleDirectionMarks_DoNotBreakParsing(string text)
    {
        Assert.True(NumericInput.TryParse(text, out var value), "محرف اتجاه غير مرئيّ أفشل التحليل");
        Assert.Equal(1234.56m, value);
    }

    // ── حالات الحدود ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("12abc")]
    [InlineData(".")]
    [InlineData("1.2.3")]
    [InlineData("--5")]
    public void Garbage_IsRejected(string? text)
    {
        Assert.False(NumericInput.TryParse(text, out _));
    }

    [Fact]
    public void PositiveAndNonNegative_EnforceTheirBounds()
    {
        Assert.False(NumericInput.TryParsePositive("0", out _));
        Assert.False(NumericInput.TryParsePositive("-1", out _));
        Assert.True(NumericInput.TryParsePositive("0.01", out _));

        Assert.True(NumericInput.TryParseNonNegative("0", out _));
        Assert.False(NumericInput.TryParseNonNegative("-0.01", out _));
    }

    /// <summary>
    /// حارسٌ للحارس: لو انكسر التطبيع فصار كلّ شيء يُرفض، لسقطت الاختبارات أعلاه —
    /// وهذا يؤكّد أن المسار الإيجابيّ حيّ فعلاً ولا يمرّ الحارس بالرفض الشامل.
    /// </summary>
    [Fact]
    public void AcceptancePath_IsAlive()
    {
        Assert.True(NumericInput.TryParse("1", out var one));
        Assert.Equal(1m, one);
    }
}
