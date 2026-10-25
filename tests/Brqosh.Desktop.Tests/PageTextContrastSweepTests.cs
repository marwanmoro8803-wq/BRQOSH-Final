using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Brqosh.Desktop.Themes;

namespace Brqosh.Desktop.Tests;

/// <summary>(«دفتر الحبر والنحاس») مسحُ تباين كلّ نصٍّ ظاهرٍ ومفعَّلٍ في كلّ شاشةٍ تُبنى، بالثيمين: ٤٫٥:١ (٣:١ للكبير). كشف ٥٣ تسميةً سوداءَ على الداكن.</summary>
public class PageTextContrastSweepTests
{
    private static double L(Color c)
    {
        static double Ch(byte v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
    }

    private static double Ratio(Color a, Color b)
    {
        var x = L(a); var y = L(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private static Color? Solid(Brush? b) => b is SolidColorBrush { Color.A: > 200 } s ? s.Color : null;

    private static bool Shown(DependencyObject d)
    {
        for (var p = d; p is not null; p = VisualTreeHelper.GetParent(p) ?? LogicalTreeHelper.GetParent(p))
            if (p is UIElement { Visibility: not Visibility.Visible })
                return false;
        return true;
    }

    private static bool EnabledChain(DependencyObject d)
    {
        for (var p = d; p is not null; p = VisualTreeHelper.GetParent(p))
            if (p is UIElement { IsEnabled: false })
                return false;
        return true;
    }

    private static Color BackdropOf(DependencyObject d, Color fallback)
    {
        for (var p = VisualTreeHelper.GetParent(d); p is not null; p = VisualTreeHelper.GetParent(p))
        {
            Brush? b = p switch
            {
                Border br => br.Background,
                Panel pn => pn.Background,
                _ => null,
            };
            if (Solid(b) is Color col) return col;
        }
        return fallback;
    }

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        var n = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < n; i++)
            foreach (var x in Walk(VisualTreeHelper.GetChild(root, i)))
                yield return x;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sweep(bool dark)
    {
        var report = new List<string>();
        var texts = 0;
        var pages = 0;
        foreach (var type in PageConstructionTests.ConstructablePageTypes().Where(t => typeof(UserControl).IsAssignableFrom(t)))
        {
            WpfTestHost.Invoke(() =>
            {
                ThemeManager.Apply(dark);
                FrameworkElement? page = null;
                try
                {
                    page = (FrameworkElement)Activator.CreateInstance(type)!;
                    var host = new Border { Width = 1100, Height = 640, FlowDirection = FlowDirection.RightToLeft, Child = page };
                    host.SetResourceReference(Border.BackgroundProperty, "BackgroundBrush");
                    Brqosh.Desktop.Themes.ThemeDefaults.ApplyTo(host);
                    host.Measure(new Size(1100, 640));
                    host.Arrange(new Rect(0, 0, 1100, 640));
                    host.UpdateLayout();
                    pages++;
                    var pageBg = ((SolidColorBrush)System.Windows.Application.Current.FindResource("BackgroundBrush")).Color;
                    foreach (var el in Walk(host))
                    {
                        if (el is not TextBlock tb || string.IsNullOrWhiteSpace(tb.Text) || !Shown(tb) || !EnabledChain(tb)) continue;
                        if (tb.ActualWidth <= 0 || tb.ActualHeight <= 0) continue;
                        if (Solid(tb.Foreground) is not Color fg) continue;
                        texts++;
                        var bg = BackdropOf(tb, pageBg);
                        var r = Ratio(fg, bg);
                        var size = tb.FontSize;
                        var need = size >= 18 || (size >= 14 && tb.FontWeight.ToOpenTypeWeight() >= 700) ? 3.0 : 4.5;
                        if (r < need)
                            report.Add($"{(dark ? "D" : "L")}\t{type.Name}\t{r:F2}<{need}\tfg={fg}\tbg={bg}\t\"{(tb.Text.Length > 40 ? tb.Text[..40] : tb.Text)}\"");
                    }
                }
                finally
                {
                    WpfTestHost.Release(page);
                    ThemeManager.Apply(false);
                }
            });
        }

        Assert.True(texts > 1000, $"فُحص {texts} نصّاً فقط — تغيّر البناء؟");
        Assert.True(report.Count == 0, $"{(dark ? "الداكن" : "الفاتح")}: {report.Count} نصّاً دون التباين:{Environment.NewLine}" + string.Join(Environment.NewLine, report.Take(40)));
    }
}
