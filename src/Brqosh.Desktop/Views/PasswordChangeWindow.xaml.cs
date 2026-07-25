using System.Windows;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views;

public partial class PasswordChangeWindow : Window
{
    private readonly AppUser _user;

    public AppUser? UpdatedUser { get; private set; }

    public PasswordChangeWindow(AppUser user)
    {
        _user = user;
        InitializeComponent();
        CurrentPasswordBox.Focus();
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (NewPasswordBox.Password != ConfirmPasswordBox.Password)
        {
            ErrorText.Text = "كلمة المرور الجديدة غير متطابقة.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            await facade.GetService<UserAuthService>().ChangePasswordAsync(_user.Id, CurrentPasswordBox.Password, NewPasswordBox.Password);
            UpdatedUser = await db.AppUsers.SingleAsync(u => u.Id == _user.Id);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
