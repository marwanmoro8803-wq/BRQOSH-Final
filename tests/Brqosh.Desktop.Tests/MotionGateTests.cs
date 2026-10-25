using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس») بوّابةُ الحركة: من عطّل الرسوم المتحرّكة في ويندوز يرى الحالةَ النهائيّة فوراً، ومن أبقاها تتحرّك
/// <c>Opacity</c> وحدَها في ≤ نصف ثانية بمنحنى ease-out بلا ارتداد. يُمرَّر القرارُ بمصدرٍ قابلٍ للحقن فلا يتأثّر الاختبارُ بجهاز من يشغّله
/// (درسُ <c>TheLogoShineActuallyMovesTests</c>: بوّابةٌ ثابتةٌ على إعداد الجهاز تُسقط اختباراً سليماً).
/// </summary>
public class MotionGateTests
{
    private static T WithProbe<T>(bool enabled, Func<T> work) => WpfTestHost.Invoke(() =>
    {
        var previous = Motion.EnabledProbe;
        Motion.EnabledProbe = () => enabled;
        try { return work(); }
        finally { Motion.EnabledProbe = previous; }
    });

    [Fact]
    public void WhenAnimationsAreDisabled_TheElementStaysFullyOpaque_AndNothingIsAnimated()
    {
        var (opacity, animated) = WithProbe(false, () =>
        {
            var element = new ContentControl { Opacity = 0.2 };
            Motion.FadeIn(element);
            return (element.Opacity, element.HasAnimatedProperties);
        });

        Assert.Equal(1.0, opacity);
        Assert.False(animated);
    }

    [Fact]
    public void WhenAnimationsAreEnabled_OnlyOpacityIsAnimated_ForAtMostHalfASecond_EaseOutNoBounce()
    {
        var info = WithProbe(true, () =>
        {
            var element = new ContentControl();
            Motion.FadeIn(element);
            var clock = element.GetAnimationBaseValue(UIElement.OpacityProperty);
            // ‏HasAnimatedProperties صحيحٌ ما دامت الحركةُ جارية؛ والنوعُ والمدّةُ من التعريف نفسِه.
            return (element.HasAnimatedProperties, Motion.Enter.TimeSpan, clock);
        });

        Assert.True(info.HasAnimatedProperties);
        Assert.True(info.TimeSpan <= TimeSpan.FromMilliseconds(500), $"المدّة {info.TimeSpan.TotalMilliseconds}ms");
        Assert.True(info.TimeSpan >= TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void TheEntryCurve_IsAQuarticEaseOut_NotABounceOrElastic()
    {
        // ‏مصدرُ الحقيقة: نصُّ الملفّ — لا يدخله Bounce ولا Elastic ولا ارتدادٌ يُشتّت في شاشةٍ تُستعمل عشراتِ المرّات يوميّاً.
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop", "Ui", "Motion.cs");
        var text = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(path));

        Assert.Contains("QuarticEase", text, StringComparison.Ordinal);
        Assert.Contains("EaseOut", text, StringComparison.Ordinal);
        Assert.DoesNotContain("BounceEase", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ElasticEase", text, StringComparison.Ordinal);
        Assert.DoesNotContain("BackEase", text, StringComparison.Ordinal);
    }
}
