using System.ComponentModel;
using System.Windows;

namespace Brqosh.Desktop.Views;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>security-licensing-tenancy#7</c>) <b>مفتاحُ استعادة المدير يُعرض مرّةً</b> — عند التهيئة، وبعد كلّ
/// استعادةٍ أو إصدارٍ جديد. <b>ولا تُغلق النافذةُ قبل تأكيد الحفظ</b>: مفتاحٌ لم يُحفظ كأن لم يكن، ولا يُعاد عرضُه.
/// </summary>
public partial class RecoveryKeyWindow : Window
{
    public RecoveryKeyWindow(string recoveryKey)
    {
        InitializeComponent();
        KeyBox.Text = recoveryKey;
    }

    private void OnCopy(object sender, RoutedEventArgs e) => Clipboard.SetText(KeyBox.Text);

    private void OnSavedChanged(object sender, RoutedEventArgs e) =>
        ContinueButton.IsEnabled = SavedCheck.IsChecked == true;

    private void OnContinue(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (SavedCheck.IsChecked != true)
            e.Cancel = true;
        base.OnClosing(e);
    }
}
