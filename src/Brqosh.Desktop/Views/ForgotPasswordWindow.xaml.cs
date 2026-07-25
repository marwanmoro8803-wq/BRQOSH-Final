using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Desktop.Views;

public partial class ForgotPasswordWindow : Window
{
    private bool _busy;

    public ForgotPasswordWindow(string? initialUsername = null)
    {
        InitializeComponent();
        PasswordFieldHelper.Wire(NewPasswordBox, NewPasswordPlainBox, NewPasswordRevealToggle);
        PasswordFieldHelper.Wire(ConfirmPasswordBox, ConfirmPlainBox, ConfirmRevealToggle);
        if (!string.IsNullOrWhiteSpace(initialUsername))
            UsernameBox.Text = initialUsername;
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private async void OnSendOtp(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        ErrorText.Visibility = Visibility.Collapsed;
        SuccessText.Visibility = Visibility.Collapsed;
        OtpHintText.Visibility = Visibility.Collapsed;

        try
        {
            await using var db = AppServices.CreateDb();
            var result = await AppServices.CreateOtpService(db).SendRecoveryCodeAsync(
                AppServices.CompanyId, UsernameBox.Text);

            OtpHintText.Text = OtpHintFormatter.Format(result);
            OtpHintText.Visibility = Visibility.Visible;
            OtpBox.Focus();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OnResetPassword(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        ErrorText.Visibility = Visibility.Collapsed;
        SuccessText.Visibility = Visibility.Collapsed;

        try
        {
            if (!string.Equals(NewPasswordBox.Password, ConfirmPasswordBox.Password, StringComparison.Ordinal))
                throw new InvalidOperationException("كلمة المرور وتأكيدها غير متطابقين.");
            if (string.IsNullOrWhiteSpace(OtpBox.Text))
                throw new InvalidOperationException("أدخل رمز التحقق.");

            await using var db = AppServices.CreateDb();
            await AppServices.CreateOtpService(db).ResetPasswordAsync(
                AppServices.CompanyId,
                UsernameBox.Text,
                OtpBox.Text.Trim(),
                NewPasswordBox.Password);

            SuccessText.Text = "تم تعيين كلمة المرور بنجاح. يمكنك تسجيل الدخول الآن.";
            SuccessText.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            _busy = false;
        }
    }
}
