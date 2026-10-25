using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Brqosh.Desktop.Themes;

/// <summary>
/// («دفتر الحبر والنحاس») <b>لونُ النصّ الافتراضيّ يتبع الثيم.</b> لا نافذةَ في البرنامج تحدّد <c>Foreground</c>، فكلُّ <c>TextBlock</c> بلا لونٍ صريحٍ
/// يرثه من النافذة — أي من <c>SystemColors.WindowText</c> الأسود. وفي الوضع الداكن (وهو الأصل عند أوّل تشغيل) صارت تسمياتُ حقول الفلترة
/// («من» · «إلى» · «حتى تاريخ» · «المشروع:» …) في أكثر من ثلاثين صفحة <b>سوداءَ على خلفيةٍ داكنة بتباين ١٫٢:١</b> — قِيس بمسحٍ يبني كلَّ الشاشات في الثيمين.
///
/// <para>الحلُّ مركزيّ لا صفحةً صفحة: كلُّ نافذةٍ تحصل عند تحميلها على <c>TextPrimaryBrush</c> مرجعاً <b>ديناميكيّاً</b> (فيتبع تبديلَ الثيم)،
/// ما لم تحدّد نافذةٌ لوناً بنفسها. وهو نمطُ <c>WindowFit.Register</c> نفسُه.</para>
/// </summary>
public static class ThemeDefaults
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;
        _registered = true;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window window)
                    ApplyTo(window);
            }));
    }

    /// <summary>يربط لونَ النصّ الافتراضيَّ بالثيم على جذر الشاشة إن لم يحدّد له لوناً بيده.</summary>
    public static void ApplyTo(FrameworkElement root)
    {
        if (root.ReadLocalValue(TextElement.ForegroundProperty) != DependencyProperty.UnsetValue)
            return;

        root.SetResourceReference(TextElement.ForegroundProperty, "TextPrimaryBrush");
    }
}
