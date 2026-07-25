using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;
using Brqosh.Desktop.Security;

namespace Brqosh.Desktop.Views.Pages;

public partial class UsersPage : UserControl
{
    public record Row(long Id, string Username, string DisplayNameAr, string RoleAr, string StatusAr);

    private List<AppUser> _users = [];

    public UsersPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanManageUsers));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        using var facade = new DesktopAccountingFacade();
        _users = (await facade.GetService<UserManagementService>().ListAsync(AppServices.CompanyId)).ToList();
        var rows = _users.Select(u => new Row(
            u.Id, u.Username, u.DisplayNameAr,
            AppUser.RoleLabel(u.Role),
            u.IsActive ? "نشط" : "موقوف")).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        var active = rows.Count(r => r.StatusAr == "نشط");
        UserSummaryText.Text = $"مستخدمون: {rows.Count} · نشط: {active} · موقوف: {rows.Count - active}";
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new UserDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        var user = _users.Single(u => u.Id == row.Id);
        if (new UserDialog(user) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }
}
