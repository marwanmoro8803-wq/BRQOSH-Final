using System.Windows;
using Brqosh.Infrastructure.Setup;

namespace Brqosh.Desktop.Views;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>installer-deployment-ops#13</c>) <b>اتّفاقيّةُ الترخيص قبل الإعداد</b> — تُعرض حتى يقبل المستخدمُ نصَّها
/// بعينه (<see cref="EulaAcceptance"/>)، و«لا أوافق» يُغلق البرنامج.
/// </summary>
public partial class EulaWindow : Window
{
    private readonly string _text;

    /// <param name="readOnly">
    /// ‏للاطّلاع بعد القبول (من «تفضيلات البرنامج»): لا خانةَ قبولٍ ولا «أوافق» — زرُّ إغلاقٍ وحده، ولا يُسجَّل قبولٌ جديد.
    /// </param>
    public EulaWindow(string text, bool readOnly = false)
    {
        InitializeComponent();
        _text = text;
        TextBox.Text = text;
        if (readOnly)
        {
            AgreeCheck.Visibility = Visibility.Collapsed;
            AcceptButton.Visibility = Visibility.Collapsed;
            DeclineButton.Content = "إغلاق";
        }
    }

    private void OnAgreeChanged(object sender, RoutedEventArgs e) => AcceptButton.IsEnabled = AgreeCheck.IsChecked == true;

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        EulaAcceptance.Accept(_text);
        DialogResult = true;
    }

    private void OnDecline(object sender, RoutedEventArgs e) => DialogResult = false;
}
