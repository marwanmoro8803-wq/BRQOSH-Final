using System.Windows;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views;

public partial class LoginWindow : Window
{
    public AppUser? AuthenticatedUser { get; internal set; }
    private bool _busy;
    private AppUser? _mfaPendingUser;

    public LoginWindow()
    {
        InitializeComponent();
        PasswordFieldHelper.Wire(PasswordBox, PasswordPlainBox, PasswordRevealToggle);
        PasswordFieldHelper.Wire(RegPasswordBox, RegPasswordPlainBox, RegPasswordRevealToggle);
        PasswordFieldHelper.Wire(RegConfirmPasswordBox, RegConfirmPlainBox, RegConfirmRevealToggle);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var prefs = LoginPreferencesStore.Load();
        RememberMeBox.IsChecked = prefs.RememberMe;
        if (prefs.RememberMe && !string.IsNullOrWhiteSpace(prefs.Username))
            UsernameBox.Text = prefs.Username;
        UsernameBox.Focus();
    }

    private void OnLoginTab(object sender, RoutedEventArgs e) => SetAuthMode(login: true);

    private void OnRegisterTab(object sender, RoutedEventArgs e) => SetAuthMode(login: false);

    private void SetAuthMode(bool login)
    {
        LoginPanel.Visibility = login ? Visibility.Visible : Visibility.Collapsed;
        RegisterPanel.Visibility = login ? Visibility.Collapsed : Visibility.Visible;
        LoginTabButton.Tag = login ? "active" : null;
        RegisterTabButton.Tag = login ? null : "active";
        ErrorText.Visibility = Visibility.Collapsed;
        RegisterErrorText.Visibility = Visibility.Collapsed;
        RegisterSuccessText.Visibility = Visibility.Collapsed;
    }

    private void OnForgotPassword(object sender, RoutedEventArgs e)
    {
        var dialog = new ForgotPasswordWindow(UsernameBox.Text) { Owner = this };
        dialog.ShowDialog();
    }

    private async void OnLogin(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        ErrorText.Visibility = Visibility.Collapsed;

        try
        {
            var username = UsernameBox.Text;
            LoginLockoutTracker.ThrowIfLocked(AppServices.CompanyId, username);

            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var auth = facade.GetService<UserAuthService>();
            var user = await auth.AuthenticateAsync(
                AppServices.CompanyId, username, PasswordBox.Password,
                string.IsNullOrWhiteSpace(MfaBox.Text) ? null : MfaBox.Text.Trim());

            if (user is null && _mfaPendingUser is not null)
            {
                ErrorText.Text = "رمز المصادقة غير صحيح.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            if (user is null)
            {
                LoginLockoutTracker.RegisterFailure(AppServices.CompanyId, username);
                var probe = await db.AppUsers.FirstOrDefaultAsync(u =>
                    u.CompanyId == AppServices.CompanyId && u.Username == username.Trim().ToLowerInvariant() && u.IsActive);
                if (probe is not null && probe.RequiresMfa && VerifyPasswordOnly(PasswordBox.Password, probe.PasswordHash))
                {
                    _mfaPendingUser = probe;
                    MfaLabel.Visibility = Visibility.Visible;
                    MfaBox.Visibility = Visibility.Visible;
                    MfaBox.Focus();
                    ErrorText.Text = "أدخل رمز المصادقة الثنائية.";
                    ErrorText.Visibility = Visibility.Visible;
                    return;
                }

                ErrorText.Text = "اسم المستخدم أو كلمة المرور غير صحيحة.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            LoginLockoutTracker.RegisterSuccess(AppServices.CompanyId, username);
            LoginPreferencesStore.Save(new LoginPreferences
            {
                RememberMe = RememberMeBox.IsChecked == true,
                Username = RememberMeBox.IsChecked == true ? username.Trim() : null,
            });

            AuthenticatedUser = user;
            AppServices.CurrentUser = user;
            DialogResult = true;
            Close();
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

    private async void OnSendRegistrationOtp(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        RegisterErrorText.Visibility = Visibility.Collapsed;
        RegisterSuccessText.Visibility = Visibility.Collapsed;
        RegOtpHintText.Visibility = Visibility.Collapsed;

        try
        {
            await using var db = AppServices.CreateDb();
            var result = await AppServices.CreateOtpService(db).SendRegistrationCodeAsync(
                AppServices.CompanyId, RegUsernameBox.Text, RegMobileBox.Text);

            RegOtpHintText.Text = OtpHintFormatter.Format(result);
            RegOtpHintText.Visibility = Visibility.Visible;
            RegOtpBox.Focus();
        }
        catch (Exception ex)
        {
            RegisterErrorText.Text = ex.Message;
            RegisterErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OnRegister(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        RegisterErrorText.Visibility = Visibility.Collapsed;
        RegisterSuccessText.Visibility = Visibility.Collapsed;

        try
        {
            if (!string.Equals(RegPasswordBox.Password, RegConfirmPasswordBox.Password, StringComparison.Ordinal))
                throw new InvalidOperationException("كلمة المرور وتأكيدها غير متطابقين.");
            if (string.IsNullOrWhiteSpace(RegOtpBox.Text))
                throw new InvalidOperationException("أدخل رمز التحقق.");

            await using var db = AppServices.CreateDb();
            await AppServices.CreateOtpService(db).RegisterVerifiedUserAsync(
                AppServices.CompanyId,
                RegUsernameBox.Text,
                RegDisplayBox.Text,
                RegMobileBox.Text,
                RegPasswordBox.Password,
                RegOtpBox.Text.Trim());

            RegisterSuccessText.Text = "تم إنشاء طلب الحساب بنجاح. سيفعّله مدير النظام قريباً — يمكنك العودة لتسجيل الدخول بعد التفعيل.";
            RegisterSuccessText.Visibility = Visibility.Visible;
            SetAuthMode(login: true);
            UsernameBox.Text = RegUsernameBox.Text.Trim();
        }
        catch (Exception ex)
        {
            RegisterErrorText.Text = ex.Message;
            RegisterErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            _busy = false;
        }
    }

    private static bool VerifyPasswordOnly(string password, string storedHash) =>
        UserAuthService.VerifyPassword(password, storedHash);
}
