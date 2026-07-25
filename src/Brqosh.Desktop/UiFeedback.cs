using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Brqosh.Desktop;

/// <summary>
/// ملاحظات واجهة: Toast للأحداث الروتينية؛ MessageBox فقط للتأكيدات والأخطاء الحرجة عند عدم توفر النافذة.
/// </summary>
internal static class UiFeedback
{
    private static Border? _toastHost;
    private static TextBlock? _toastText;
    private static DispatcherTimer? _hideTimer;

    public static void AttachToastHost(Panel overlayRoot)
    {
        _toastHost = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 12, 18, 12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 28),
            Visibility = Visibility.Collapsed,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12, ShadowDepth = 2, Opacity = 0.35,
            },
            MaxWidth = 560,
            Child = _toastText = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
            },
        };
        overlayRoot.Children.Add(_toastHost);
    }

    public static void ShowSuccess(string message) => ShowToast(message, "#059669");
    public static void ShowInfo(string message) => ShowToast(message, "#2563EB");
    public static void ShowWarning(string message) => ShowToast(message, "#D97706");
    public static void ShowError(string message) => ShowToast(message, "#DC2626");

    /// <summary>تنبيه يحتاج قراراً — يبقى MessageBox.</summary>
    public static bool Confirm(string message, string title = "BRQOSH") =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public static async Task RunSafeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private static void ShowToast(string message, string hexBackground)
    {
        if (_toastHost is null || _toastText is null)
        {
            MessageBox.Show(message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _toastText.Text = message;
        _toastHost.Background = (Brush)new BrushConverter().ConvertFromString(hexBackground)!;
        _toastHost.Visibility = Visibility.Visible;

        _hideTimer?.Stop();
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            _toastHost.Visibility = Visibility.Collapsed;
        };
        _hideTimer.Start();
    }
}
