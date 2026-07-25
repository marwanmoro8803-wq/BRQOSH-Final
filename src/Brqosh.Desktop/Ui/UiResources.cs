using System.Windows;
using System.Windows.Media;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// حل موارد WPF الصحيح — UserControl.Resources فارغ محلياً؛
/// FindResource يبحث في Application.Resources المدمج.
/// </summary>
public static class UiResources
{
    public static Style StyleOf(FrameworkElement host, string key) =>
        (Style)host.FindResource(key);

  public static Brush BrushOf(FrameworkElement host, string key) =>
        (Brush)host.FindResource(key);
}
