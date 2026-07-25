using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-223، O-25) الترجمة الإنجليزية يجب أن تكون **إنجليزية**.
///
/// `I18nCoverageTests` كان يتحقّق أن لكل نصّ عربيّ في XAML **مدخلاً** في السجلّ،
/// ولا يتحقّق أن المدخل مترجَم فعلاً. والمولّد يستبدل كلمةً بكلمة من قاموس، فيُنتج
/// لكل عبارة غير مغطّاة بالكامل خليطاً ركيكاً يراه المستخدم:
///
///   «12 حرفاً على الأقل مع حرف كبير…»  ←  «12 حرفاً on الأقل with حرف كبير…»
///   «© BRQOSH ERP — جميع الحقوق محفوظة» ←  «© BRQOSH ERP  جميع الحقوق محفوظة»
///
/// أسوأ من غياب الترجمة: نصٌّ مكسور بلغتين. المقياس عند كتابة هذا الحارس:
/// **188 من 799 (24%)**. عولجت بـ`assets/i18n/ui-overrides.json` — عبارات كاملة
/// بخطّ اليد يدمجها المولّد أخيراً فتغلب قاموس الكلمات.
/// </summary>
public class BilingualRegistryQualityTests
{
    private static bool HasArabic(string text) =>
        text.Any(ch => ch is >= '؀' and <= 'ۿ');

    /// <summary>لا مدخل «إنجليزيّ» يحوي حرفاً عربياً.</summary>
    [Fact]
    public void EveryEnglishValue_IsFreeOfArabic()
    {
        var broken = UiBilingualRegistry.Map
            .Where(kv => HasArabic(kv.Value))
            .Select(kv => $"«{kv.Key}»\n      → «{kv.Value}»")
            .ToList();

        Assert.True(broken.Count == 0,
            $"مداخل إنجليزية لا تزال تحوي عربياً ({broken.Count} من {UiBilingualRegistry.Map.Count}) — " +
            "أضِف العبارة كاملةً إلى assets/i18n/ui-overrides.json ثم أعد التوليد:\n  - " +
            string.Join("\n  - ", broken.Take(40)));
    }

    /// <summary>ولا مدخل يعيد النصّ العربيّ نفسه — أي «لا ترجمة» متنكّرة في صورة ترجمة.</summary>
    [Fact]
    public void NoEntry_EchoesItsArabicSource()
    {
        var echoed = UiBilingualRegistry.Map
            .Where(kv => string.Equals(kv.Key, kv.Value, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .ToList();

        Assert.True(echoed.Count == 0,
            $"مداخل تُرجع المصدر العربيّ كما هو ({echoed.Count}):\n  - " +
            string.Join("\n  - ", echoed.Take(40)));
    }

    /// <summary>حارس للحارس: السجلّ ممتلئ — وإلا مرّ الاختباران على قاموس فارغ.</summary>
    [Fact]
    public void Registry_IsPopulated()
    {
        Assert.True(UiBilingualRegistry.Map.Count >= 700,
            $"عدد مداخل السجلّ {UiBilingualRegistry.Map.Count} — يُتوقع 700+.");
    }
}
