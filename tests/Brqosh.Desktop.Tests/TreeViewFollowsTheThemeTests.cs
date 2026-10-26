using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس») TreeView الافتراضيّ أبيضُ ناصعٌ في الثيم الداكن (شجرةُ الحسابات كانت مستطيلاً أبيض كاملاً — وُجد بتصوير النافذة الرئيسة الحقيقيّة
/// في ٧٣ شاشةً). فالأصل نمطٌ ضمنيٌّ يجعل خلفيّته شفّافةً فتظهر بطاقةُ الصفحة.
/// </summary>
public class TreeViewFollowsTheThemeTests
{
    [Fact]
    public void TheImplicitTreeViewStyle_IsTransparentOverTheCard()
    {
        var background = WpfTestHost.Invoke(() =>
        {
            var style = (Style)System.Windows.Application.Current.FindResource(typeof(TreeView));
            return style.Setters.OfType<Setter>().First(s => s.Property == Control.BackgroundProperty).Value;
        });

        Assert.Equal(Brushes.Transparent, background);
    }
}
