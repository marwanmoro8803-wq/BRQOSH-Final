using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (مراجعةُ الحوارات · سجلُّ الأعطال) <b>نافذةُ الرسائل بتصميم البرنامج لا بتصميم النظام.</b>
///
/// <para><b>ما كان:</b> كلُّ رسالةٍ — ومنها رسالةُ العطل — تمرّ بـ<c>MessageBox.Show</c> وهي نافذةُ Win32 رماديّة: لا شعارَ ولا ألوانَ
/// ولا خطَّ البرنامج، ولا اتجاهَ يتبع اللغة تماماً، ولا يُنسخ نصُّها بسهولة.</para>
///
/// <para><b>الآن:</b> شريطٌ كحليٌّ بشعار الدرع الذهبيّ واسم الرسالة، وشريطٌ جانبيٌّ بلون النوع (خطأ · تنبيه · سؤال · معلومة)، ونصٌّ
/// قابلٌ للتحديد والنسخ، وأزرارٌ بنمط البرنامج مترجمةٌ وفق اللغة، واتجاهٌ يتبع اللغة (RTL/LTR). والتواقيعُ والنتائجُ كـ<c>MessageBox</c>
/// حرفاً — فالنتيجةُ <see cref="MessageBoxResult"/> نفسُها، ولا يتغيّر سلوكٌ. وحيث لا تطبيقَ يعمل (اختبارٌ، أو إقلاعٌ مبكّر)
/// يُستعمل <c>MessageBox</c> كما كان.</para>
/// </summary>
internal sealed class BrandedMessageWindow : Window
{
    private MessageBoxResult _result = MessageBoxResult.None;
    private readonly MessageBoxResult _cancelResult;

    private BrandedMessageWindow(string text, string caption, MessageBoxButton buttons, MessageBoxImage icon, MessageBoxResult defaultResult)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.Height;
        Width = 520;
        MaxHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FlowDirection = LocaleManager.Flow;
        FontFamily = (FontFamily)(TryFind("AppFont") ?? new FontFamily("Segoe UI"));
        Background = Brush("SurfaceBrush", "#FFFFFF");
        BorderBrush = Brush("PrimaryBrush", "#6B4420");
        BorderThickness = new Thickness(1);
        Title = string.IsNullOrWhiteSpace(caption) ? "BRQOSH" : caption;
        _cancelResult = CancelResultFor(buttons);

        var accent = AccentFor(icon);
        var root = new DockPanel();

        // الشريط الكحليّ: الدرعُ الذهبيّ + عنوان الرسالة. يسحب النافذة.
        var header = new Border { Background = Brush("PrimaryBrush", "#6B4420"), Height = 44, Cursor = Cursors.SizeAll };
        header.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        if (TryFind("BrqoshMarkGeometry") is Geometry mark)
            headerRow.Children.Add(new Path { Data = mark, Fill = new SolidColorBrush(Color.FromRgb(0xC9, 0xA2, 0x27)), Stretch = Stretch.Uniform, Width = 18, Height = 22, Margin = new Thickness(0, 0, 10, 0) });
        headerRow.Children.Add(new TextBlock { Text = Title, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        header.Child = headerRow;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // الأزرار
        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(18, 4, 18, 16) };
        DockPanel.SetDock(buttonRow, Dock.Bottom);
        root.Children.Add(buttonRow);
        foreach (var (label, result, primary) in ButtonsFor(buttons, defaultResult))
        {
            var button = new Button
            {
                Content = label,
                MinWidth = 88,
                Padding = new Thickness(18, 7, 18, 7),
                Margin = new Thickness(8, 0, 0, 0),
                IsDefault = result == DefaultFor(buttons, defaultResult),
                IsCancel = result == _cancelResult && buttons != MessageBoxButton.OK,
            };
            if (TryFind(primary ? "PrimaryButton" : "SecondaryButton") is Style style)
                button.Style = style;
            button.Click += (_, _) => { _result = result; Close(); };
            buttonRow.Children.Add(button);
        }

        // الجسم: شريطٌ جانبيٌّ بلون النوع + نصٌّ قابلٌ للتحديد.
        var body = new DockPanel { Margin = new Thickness(0, 18, 0, 10) };
        body.Children.Add(new Border { Width = 4, Background = accent, Margin = new Thickness(18, 2, 14, 2), CornerRadius = new CornerRadius(2) });
        var textBox = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Brush("TextPrimaryBrush", "#1B2733"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13.5,
            Padding = new Thickness(0, 0, 18, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 380,
            FocusVisualStyle = null,
        };
        body.Children.Add(textBox);
        root.Children.Add(body);

        Content = root;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _cancelResult != MessageBoxResult.None) { _result = _cancelResult; Close(); }
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && string.IsNullOrEmpty(textBox.SelectedText))
                Clipboard.SetText(text);
        };
    }

    /// <summary>يعرض الرسالة بنمط البرنامج ويُعيد ما يُعيده <c>MessageBox.Show</c>.</summary>
    public static MessageBoxResult Show(
        Window? owner, string text, string caption, MessageBoxButton buttons, MessageBoxImage icon, MessageBoxResult defaultResult)
    {
        var app = System.Windows.Application.Current;
        if (app is null || !app.Dispatcher.CheckAccess() || app.Resources is null)
            return Fallback(owner, text, caption, buttons, icon, defaultResult);

        try
        {
            owner ??= app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? app.MainWindow;
            var window = new BrandedMessageWindow(text, caption, buttons, icon, defaultResult);
            if (owner is { IsLoaded: true } && !ReferenceEquals(owner, window))
                window.Owner = owner;
            else
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.ShowDialog();
            return window._result == MessageBoxResult.None ? window._cancelResult : window._result;
        }
        catch (InvalidOperationException)
        {
            // ‏نافذةٌ لا تُفتح الآن (إغلاقُ التطبيق مثلاً) — لا تُضيّع الرسالة.
            return Fallback(owner, text, caption, buttons, icon, defaultResult);
        }
    }

    private static MessageBoxResult Fallback(Window? owner, string text, string caption, MessageBoxButton b, MessageBoxImage i, MessageBoxResult d) =>
        owner is null ? MessageBox.Show(text, caption, b, i, d) : MessageBox.Show(owner, text, caption, b, i, d);

    private static object? TryFind(string key) => System.Windows.Application.Current?.TryFindResource(key);

    private static Brush Brush(string key, string fallbackHex) =>
        TryFind(key) as Brush ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex));

    private static Brush AccentFor(MessageBoxImage icon) => icon switch
    {
        MessageBoxImage.Error => Brush("DangerBrush", "#C0392B"),
        MessageBoxImage.Warning => new SolidColorBrush(Color.FromRgb(0xC9, 0xA2, 0x27)),
        MessageBoxImage.Question => Brush("PrimaryBrush", "#6B4420"),
        _ => Brush("InfoBrush", "#2A6F9E"),
    };

    private static MessageBoxResult CancelResultFor(MessageBoxButton b) => b switch
    {
        MessageBoxButton.OK => MessageBoxResult.OK,
        MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
        MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
        MessageBoxButton.YesNo => MessageBoxResult.None,   // كـMessageBox: Esc لا يُغلق نعم/لا إلا بالإغلاق = لا
        _ => MessageBoxResult.None,
    };

    private static MessageBoxResult DefaultFor(MessageBoxButton b, MessageBoxResult requested)
    {
        var order = ButtonsFor(b, MessageBoxResult.None).Select(x => x.Result).ToList();
        return requested != MessageBoxResult.None && order.Contains(requested) ? requested : order[0];
    }

    private static IEnumerable<(string Label, MessageBoxResult Result, bool Primary)> ButtonsFor(MessageBoxButton b, MessageBoxResult d)
    {
        var yes = LocaleManager.T("نعم", "Yes");
        var no = LocaleManager.T("لا", "No");
        var ok = LocaleManager.T("موافق", "OK");
        var cancel = LocaleManager.T("إلغاء", "Cancel");
        switch (b)
        {
            case MessageBoxButton.OK:
                yield return (ok, MessageBoxResult.OK, true);
                break;
            case MessageBoxButton.OKCancel:
                yield return (ok, MessageBoxResult.OK, true);
                yield return (cancel, MessageBoxResult.Cancel, false);
                break;
            case MessageBoxButton.YesNo:
                yield return (yes, MessageBoxResult.Yes, true);
                yield return (no, MessageBoxResult.No, false);
                break;
            default:
                yield return (yes, MessageBoxResult.Yes, true);
                yield return (no, MessageBoxResult.No, false);
                yield return (cancel, MessageBoxResult.Cancel, false);
                break;
        }
    }
}
