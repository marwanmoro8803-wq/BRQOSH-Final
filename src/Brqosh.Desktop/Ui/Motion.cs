using System.Windows;
using System.Windows.Media.Animation;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// («دفتر الحبر والنحاس») <b>بوّابةُ الحركة الواحدة.</b> الحركةُ الوحيدة في البرنامج كانت لمعةَ الشعار؛ وكلُّ حركةٍ تُضاف تمرّ من هنا فتحترم
/// إعدادَ ويندوز «إظهار الرسوم المتحرّكة» (<see cref="SystemParameters.ClientAreaAnimation"/>) — ومن يعطّله لضعف جهازه أو حساسيّةٍ أو تركيز
/// يرى الحالةَ النهائيّة فوراً.
///
/// <para>القواعد (من مجلس التصميم): نصفُ ثانيةٍ سقفٌ أبعدُ منه لا يُحتمل في شاشةٍ تُستعمل عشراتِ المرّات يوميّاً — فالدخولُ ١٦٠ms؛
/// منحنى ease-out رباعيّ (يبدأ سريعاً ويستقرّ) بلا ارتداد؛ وتتحرّك <c>Opacity</c> وحدَها (لا Width/Height/Margin فلا إعادةَ قياسٍ للتخطيط)؛
/// ولا حركةَ داخل صفوف الجداول ولا حقول الأرقام ولا أخطاء التحقّق.</para>
/// </summary>
internal static class Motion
{
    /// <summary>مصدرُ القرار — يُستبدل في الاختبار؛ الافتراضُ إعدادُ ويندوز الحيّ.</summary>
    internal static Func<bool> EnabledProbe { get; set; } = () => SystemParameters.ClientAreaAnimation;

    internal static bool Enabled => EnabledProbe();

    /// <summary>مدّةُ دخول صفحةٍ أو لوحةٍ.</summary>
    internal static readonly Duration Enter = new(TimeSpan.FromMilliseconds(160));

    /// <summary>ظهورٌ تدريجيّ للعنصر؛ إن عُطّلت الحركةُ يبقى كما هو (معتمٌ تماماً لا شفّاف).</summary>
    internal static void FadeIn(UIElement element)
    {
        if (!Enabled)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 1;
            return;
        }

        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = Enter,
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop,
        });
    }
}
