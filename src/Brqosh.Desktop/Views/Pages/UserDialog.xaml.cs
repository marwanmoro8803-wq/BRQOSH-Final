using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Desktop.Views.Pages;

public partial class UserDialog : Window
{
    private readonly AppUser? _existing;

    public UserDialog() => InitializeComponent();

    public UserDialog(AppUser existing) : this()
    {
        _existing = existing;
        UsernameBox.Text = existing.Username;
        DisplayBox.Text = existing.DisplayNameAr;
        MobileBox.Text = existing.MobilePhoneE164 ?? "";
        RoleCombo.SelectedIndex = (int)existing.Role;
        ActiveBox.IsChecked = existing.IsActive;
        UsernameBox.IsEnabled = false;
        Title = "تعديل مستخدم";
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_existing is null)
        {
            UsernameBox.IsEnabled = true;
            Title = "مستخدم جديد";
        }
    }

    private static UserRole RoleFromIndex(int index) => index switch
    {
        0 => UserRole.Administrator,
        1 => UserRole.Accountant,
        2 => UserRole.Cashier,
        _ => UserRole.Viewer,
    };

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            using var facade = new DesktopAccountingFacade();
            var service = facade.GetService<UserManagementService>();

            if (_existing is null)
            {
                if (string.IsNullOrWhiteSpace(PasswordBox.Password))
                    throw new AccountingException("كلمة المرور مطلوبة للمستخدم الجديد.");
                await service.CreateAsync(AppServices.CompanyId, UsernameBox.Text, PasswordBox.Password,
                    DisplayBox.Text, RoleFromIndex(RoleCombo.SelectedIndex), MobileBox.Text);
            }
            else
            {
                await service.UpdateAsync(AppServices.CompanyId, _existing.Id, DisplayBox.Text,
                    RoleFromIndex(RoleCombo.SelectedIndex), ActiveBox.IsChecked == true,
                    string.IsNullOrWhiteSpace(PasswordBox.Password) ? null : PasswordBox.Password,
                    MobileBox.Text);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex is AccountingException ae ? ae.Message : ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
