using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Brqosh.Desktop.Themes;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس») لونُ النصّ الافتراضيّ يتبع الثيم: تسمياتٌ بلا لونٍ صريحٍ كانت <b>سوداءَ على داكنٍ بتباين ١٫٢:١</b> في ثلاثين صفحةً (قِيس بمسحٍ يبني
/// كلَّ الشاشات). يُقاس هنا الأثرُ الفعليّ على <c>TextBlock</c> لا النصُّ المكتوب: وراثةُ اللون، وتبعُه لتبديل المظهر، وعدمُ دهس نافذةٍ حدّدت لونها.
/// </summary>
public class ThemeDefaultsTests
{
    private static Color Of(Brush brush) => ((SolidColorBrush)brush).Color;

    [Fact]
    public void ATextBlockWithoutAColour_InheritsTheThemeInk_InBothThemes()
    {
        var (light, dark) = WpfTestHost.Invoke(() =>
        {
            try
            {
                static Color InkOnAFreshScreen()
                {
                    // ‏شاشةٌ جديدةٌ في كلّ مرّة — كما يفعل تبديلُ الثيم (تُعاد الصفحة) — فالعنصرُ المنفصلُ عن نافذةٍ لا يتلقّى إشعارَ تغيّر الموارد.
                    var host = new Border();
                    var label = new TextBlock { Text = "من" };
                    host.Child = label;
                    ThemeDefaults.ApplyTo(host);
                    host.Measure(new Size(100, 100));
                    return Of(label.Foreground);
                }

                ThemeManager.Apply(false);
                var l = InkOnAFreshScreen();
                ThemeManager.Apply(true);
                var d = InkOnAFreshScreen();
                return (l, d);
            }
            finally
            {
                ThemeManager.Apply(false);
            }
        });

        Assert.Equal(Color.FromRgb(0x14, 0x14, 0x13), light);   // ‏حبرُ الفاتح
        Assert.Equal(Color.FromRgb(0xEE, 0xE8, 0xDA), dark);    // ‏حبرُ الداكن — لا الأسودُ الافتراضيّ
        Assert.NotEqual(Colors.Black, dark);
    }

    [Fact]
    public void AWindowThatSetItsOwnColour_IsNotOverridden()
    {
        var kept = WpfTestHost.Invoke(() =>
        {
            var host = new Border();
            TextElement.SetForeground(host, new SolidColorBrush(Colors.Red));
            ThemeDefaults.ApplyTo(host);
            var label = new TextBlock { Text = "x" };
            host.Child = label;
            return Of(label.Foreground);
        });

        Assert.Equal(Colors.Red, kept);
    }

    [Fact]
    public void TheClassHandler_AppliesItToEveryWindowWhenItLoads()
    {
        var applied = WpfTestHost.Invoke(() =>
        {
            ThemeDefaults.Register();
            var window = new Window();
            try
            {
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                return window.ReadLocalValue(TextElement.ForegroundProperty) != DependencyProperty.UnsetValue;
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(applied);
    }
}
