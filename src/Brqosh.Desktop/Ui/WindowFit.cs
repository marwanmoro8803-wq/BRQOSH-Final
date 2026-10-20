using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (UI-AUDIT · P1) <b>لا نافذةَ أطولُ من الشاشة.</b>
///
/// <para><b>ما كان:</b> نحو أربعين نافذة حوارٍ بارتفاعٍ ثابت (560–760) بلا تمرير، ونافذةُ الدخول
/// <c>NoResize</c>، ومعالجُ الإعداد <c>MinHeight=680</c>. وشاشةُ لابتوب 1366×768 بتكبير 125%
/// تعطي نحو 614 وحدةً صالحة — فيُقصّ رأسُ النافذة أو ذيلُها <b>ومعه زرُّ الحفظ</b>.</para>
///
/// <para><b>والإصلاحُ آليّةٌ واحدة لا أربعون نسخة:</b> معالجٌ على مستوى الصنف <see cref="Window"/>
/// يعمل عند <c>Loaded</c> لكلّ نافذةٍ في التطبيق:</para>
/// <list type="number">
/// <item>يحصر <c>MinHeight/MinWidth/MaxHeight/MaxWidth</c> في مساحة العمل، ويُعيد النافذةَ داخلها.</item>
/// <item><b>وفقط إن كانت أطولَ من الشاشة فعلاً</b> وجذرُها <see cref="Grid"/> صفُّه الأخير <c>Auto</c>
/// (صفّ الحفظ/الإلغاء — نمطُ كلّ حواراتنا): ينقل الصفَّ الأخير خارج التمرير، ويلفّ بقيّة النموذج في
/// <see cref="ScrollViewer"/> <b>بارتفاعه المصمَّم</b> — فتبقى صفوفُ <c>*</c> وجداولُها محدودةَ الارتفاع
/// (لا يُكسر ترشيدُ الصفوف virtualization) ويظهر الحفظ والإلغاء دائماً.</item>
/// </list>
/// <para>على شاشةٍ تتّسع للتصميم لا يتغيّر شيءٌ إطلاقاً — فلا خطرَ على من لا يشكو.</para>
/// <para>للاستثناء: <c>ui:WindowFit.IsEnabled="False"</c>.</para>
/// </summary>
public static class WindowFit
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(WindowFit), new PropertyMetadata(true));

    public static bool GetIsEnabled(DependencyObject o) => (bool)o.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject o, bool value) => o.SetValue(IsEnabledProperty, value);

    /// <summary>علامةٌ أنّ الجسم لُفّ — حتى لا يُلفّ مرّتين.</summary>
    internal const string ScrollHostName = "WindowFitScrollHost";

    private static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;
        _registered = true;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window w && GetIsEnabled(w))
                    Fit(w, SystemParameters.WorkArea);
            }));
    }

    /// <summary>يحصر النافذة في <paramref name="workArea"/> ويُمرّر جسمَها عند الحاجة.</summary>
    public static void Fit(Window window, Rect workArea)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (workArea.Height <= 0 || workArea.Width <= 0)
            return;

        window.MinHeight = Math.Min(window.MinHeight, workArea.Height);
        window.MinWidth = Math.Min(window.MinWidth, workArea.Width);

        if (window.WindowState == WindowState.Maximized)
            return;

        var tooTall = window.ActualHeight > workArea.Height + 0.5;
        if (tooTall)
            TryScrollBody(window);

        window.MaxHeight = Math.Min(window.MaxHeight, workArea.Height);
        window.MaxWidth = Math.Min(window.MaxWidth, workArea.Width);
        if (window.SizeToContent != SizeToContent.Manual && tooTall)
            window.SizeToContent = window.SizeToContent == SizeToContent.WidthAndHeight
                ? SizeToContent.Width : SizeToContent.Manual;
        if (!double.IsNaN(window.Height) && window.Height > workArea.Height)
            window.Height = workArea.Height;
        if (!double.IsNaN(window.Width) && window.Width > workArea.Width)
            window.Width = workArea.Width;
        if (tooTall && double.IsNaN(window.Height))
            window.Height = workArea.Height;

        // ‏أعِدها داخل المساحة — CenterOwner مع مالكٍ قرب الحافّة يدفعها خارجها.
        var height = Math.Min(window.ActualHeight, workArea.Height);
        var width = Math.Min(window.ActualWidth, workArea.Width);
        if (window.Top + height > workArea.Bottom)
            window.Top = workArea.Bottom - height;
        if (window.Top < workArea.Top)
            window.Top = workArea.Top;
        if (window.Left + width > workArea.Right)
            window.Left = workArea.Right - width;
        if (window.Left < workArea.Left)
            window.Left = workArea.Left;
    }

    /// <summary>
    /// يلفّ جسمَ النافذة في تمريرٍ ويُبقي صفَّ الأزرار خارجه. يُرجع <c>false</c> بلا تغييرٍ
    /// إن كان الجذرُ يمرّر نفسَه أصلاً.
    /// </summary>
    /// <remarks>
    /// الأنماط: <c>Grid</c> صفُّه الأخير <c>Auto</c> · <c>StackPanel</c> آخرُ أبنائه لوحةُ أزرار ·
    /// <c>Border</c> يُطبَّق على ابنه فيبقى الإطارُ ثابتاً · وغيرُ ذلك يُلفّ كلُّه بارتفاعه المصمَّم.
    /// </remarks>
    internal static bool TryScrollBody(Window window)
    {
        if (window.Content is not FrameworkElement content)
            return false;

        if (content is Border { Child: FrameworkElement inner } border)
        {
            if (inner is ScrollViewer || ContainsScrollHost(border))
                return false;
            border.Child = null;
            border.Child = MakeScrollable(inner);
            return true;
        }

        if (content is ScrollViewer || content.Name == ScrollHostName || ContainsScrollHost(content))
            return false;

        window.Content = null;
        window.Content = MakeScrollable(content);
        return true;
    }

    /// <summary>العنصرُ يجب أن يكون منفصلاً عن أبيه قبل النداء (يُعاد تركيبه هنا).</summary>
    internal static FrameworkElement MakeScrollable(FrameworkElement body)
    {
        var designedHeight = body.ActualHeight;

        if (body is Grid grid && grid.RowDefinitions.Count >= 2 && grid.RowDefinitions[^1].Height.IsAuto)
        {
            var rows = grid.RowDefinitions;
            var footerRow = rows.Count - 1;
            var children = grid.Children.Cast<UIElement>().ToList();
            var footer = children.Where(c => Grid.GetRow(c) == footerRow).ToList();
            var spansIntoFooter = children.Any(c => Grid.GetRow(c) < footerRow && Grid.GetRow(c) + Grid.GetRowSpan(c) > footerRow);
            if (footer.Count > 0 && !spansIntoFooter)
            {
                var bodyHeight = designedHeight - rows[^1].ActualHeight;
                foreach (var f in footer)
                    grid.Children.Remove(f);
                rows.RemoveAt(footerRow);
                if (bodyHeight > 0)
                    grid.Height = bodyHeight;
                return Host(grid, footer);
            }
        }

        if (body is StackPanel { Orientation: Orientation.Vertical } stack && stack.Children.Count >= 2
            && stack.Children[^1] is FrameworkElement last && ContainsButton(last))
        {
            stack.Children.Remove(last);
            return Host(stack, [last]);
        }

        // ‏لا صفَّ أزرارٍ يُعرف: يُمرَّر الجسمُ كلُّه بارتفاعه المصمَّم (فتبقى صفوفُ * محدودة).
        if (designedHeight > 0)
            body.Height = designedHeight;
        return new ScrollViewer
        {
            Name = ScrollHostName,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = body,
        };
    }

    private static Grid Host(FrameworkElement body, IReadOnlyList<UIElement> footer)
    {
        var margin = body.Margin;
        body.Margin = new Thickness(0);
        var scroll = new ScrollViewer
        {
            Name = ScrollHostName,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = body,
        };

        var host = new Grid { Margin = margin };
        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(scroll, 0);
        host.Children.Add(scroll);
        foreach (var f in footer)
        {
            Grid.SetRow(f, 1);
            host.Children.Add(f);
        }
        return host;
    }

    private static bool ContainsButton(DependencyObject root)
    {
        if (root is Button)
            return true;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            if (ContainsButton(child))
                return true;
        return false;
    }

    private static bool ContainsScrollHost(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer { Name: ScrollHostName })
                return true;
        }
        return false;
    }
}
