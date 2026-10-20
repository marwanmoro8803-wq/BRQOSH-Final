using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Brqosh.Desktop.Ui;

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

    // (CEEF-229) ‏التوست يحمل عمى الحوارات نفسَه: نصٌّ عربيٌّ يُكتب في عنصرٍ يُنشأ في
    // ‏الكود، فلا يمرّ به XamlUiLocalizer ولا يشكو أحد. فالترجمةُ عند الاستدعاء، بالسجلّ
    // ‏نفسِه الذي يقرؤه UiDialog.
    public static void ShowSuccess(string message) => ShowToast(Localize(message), "#059669");
    public static void ShowInfo(string message) => ShowToast(Localize(message), "#2563EB");
    public static void ShowWarning(string message) => ShowToast(Localize(message), "#D97706");
    public static void ShowError(string message) => ShowToast(Localize(message), "#DC2626");

    private static string Localize(string message) =>
        LocaleManager.IsEnglish && !string.IsNullOrEmpty(message)
            ? Ui.UiMessages.Translate(message)   // ‏(ح-١٦٣) ‏حرفيّاً ثمّ بالنمط.
            : message;

    /// <summary>تنبيه يحتاج قراراً — يبقى MessageBox.</summary>
    public static bool Confirm(string message, string title = "BRQOSH") =>
        UiDialog.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

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

    /// <summary>
    /// (UI-AUDIT · عرض) <b>زرُّ «عرض» لا يصمت أبداً.</b>
    ///
    /// <para>كان «اختر صفاً أولاً» Toast يختفي بعد ثلاث ثوانٍ ونصف، والعطلُ داخل المعالج
    /// (سجلٌّ محذوف، مورّدٌ غير موجود) يذهب إلى المعالج العامّ — فيبدو الزرُّ ميتاً.
    /// والآن: نافذةُ رسالةٍ واضحة في الحالتين، والعطلُ يُكتب في سجلّ الأعطال قبل عرضه.</para>
    /// </summary>
    public static void ShowSelectionRequired(string message) => ShowMessage(message, MessageBoxImage.Information);

    /// <summary>رسالةٌ واضحة لسجلٍّ لم يعد موجوداً (حُذف أو تغيّر بعد تحميل الجدول).</summary>
    public static void ShowRecordMissing(string message) => ShowMessage(message, MessageBoxImage.Warning);

    /// <summary>
    /// (DialogLocalizationTests) <b>نافذةُ الرسالة الواحدة لهذا الملفّ</b> — ما يصلها نصٌّ مترجَمٌ عند مستدعيه (<c>LocaleManager.T</c>) أو تفصيلُ
    /// عطلٍ يُترجَم بـ<c>UiDialog.Localize</c>؛ فموضعٌ واحدٌ لا أربعة يزيد بها عدُّ النصوص غير القابلة للبحث وهي لا تُضيف نصّاً.
    /// </summary>
    private static void ShowMessage(string message, MessageBoxImage icon) =>
        UiDialog.Show(message, "BRQOSH", MessageBoxButton.OK, icon);

    /// <summary>
    /// ينفّذ إجراءَ عرضٍ ويعرض أيَّ عطلٍ في نافذة رسالةٍ (لا Toast) بعد كتابته في سجلّ الأعطال.
    /// </summary>
    public static async Task RunViewSafeAsync(Func<Task> action, string context)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowViewError(ex, context);
        }
    }

    /// <inheritdoc cref="RunViewSafeAsync"/>
    public static void RunViewSafe(Action action, string context)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ShowViewError(ex, context);
        }
    }

    private static void ShowViewError(Exception ex, string context)
    {
        var path = Brqosh.Infrastructure.Operations.CrashLog.Write(ex, context);
        var detail = ex.InnerException is null ? ex.Message : $"{ex.Message}\n\n{ex.InnerException.Message}";
        var hint = path is null ? string.Empty : "\n\n" + LocaleManager.T("سُجّل التفصيل في:", "Details were logged to:") + "\n" + path;
        ShowMessage(
            LocaleManager.T("تعذّر عرض المستند:", "Could not display the document:") + "\n" + UiDialog.Localize(detail) + hint,
            MessageBoxImage.Error);
    }

    private static void ShowToast(string message, string hexBackground)
    {
        if (_toastHost is null || _toastText is null)
        {
            ShowMessage(message, MessageBoxImage.Information);
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
