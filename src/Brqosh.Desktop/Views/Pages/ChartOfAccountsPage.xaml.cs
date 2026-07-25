using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brqosh.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;

namespace Brqosh.Desktop.Views.Pages;

public partial class ChartOfAccountsPage : UserControl
{
    private List<Account> _accounts = [];

    public ChartOfAccountsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddAccountBtn, PermissionService.CanPostAccounting),
                (EditBtn, PermissionService.CanPostAccounting),
                (DeactivateBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        _accounts = await db.Accounts
            .Where(a => a.CompanyId == AppServices.CompanyId)
            .OrderBy(a => a.Code)
            .ToListAsync();
        RenderTree(SearchBox.Text.Trim());
        var active = _accounts.Count(a => a.IsActive);
        CoaSummaryText.Text =
            $"حسابات: {_accounts.Count:N0} · نشطة: {active:N0} · معروض: {AccountsTree.Items.Count:N0}";
    }

    private void RenderTree(string filter)
    {
        AccountsTree.Items.Clear();

        var visible = string.IsNullOrEmpty(filter)
            ? _accounts
            : _accounts.Where(a => a.Code.Contains(filter) || a.NameAr.Contains(filter)).ToList();

        if (!string.IsNullOrEmpty(filter))
        {
            foreach (var account in visible)
                AccountsTree.Items.Add(BuildNode(account));
            return;
        }

        var childrenByParent = _accounts.ToLookup(a => a.ParentId);
        foreach (var root in childrenByParent[null])
            AccountsTree.Items.Add(BuildBranch(root, childrenByParent));
    }

    private TreeViewItem BuildBranch(Account account, ILookup<long?, Account> childrenByParent)
    {
        var node = BuildNode(account);
        foreach (var child in childrenByParent[account.Id])
            node.Items.Add(BuildBranch(child, childrenByParent));
        return node;
    }

    private TreeViewItem BuildNode(Account account)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            Text = account.Code,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x76, 0x6E)),
            MinWidth = 64,
        });
        panel.Children.Add(new TextBlock { Text = account.NameAr, Margin = new Thickness(8, 0, 0, 0) });
        if (account.IsGroup)
            panel.Children.Add(Badge("تجميعي", 0x64, 0x74, 0x8B));
        if (account.IsSystem)
            panel.Children.Add(Badge("نظامي", 0xC7, 0x9A, 0x34));
        if (!account.IsActive)
            panel.Children.Add(Badge("موقوف", 0xDC, 0x26, 0x26));

        return new TreeViewItem { Header = panel, Tag = account };
    }

    private static Border Badge(string text, byte r, byte g, byte b) => new()
    {
        Background = new SolidColorBrush(Color.FromArgb(0x22, r, g, b)),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(6, 1, 6, 1),
        Margin = new Thickness(8, 0, 0, 0),
        Child = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(r, g, b)),
        },
    };

    private Account? SelectedAccount =>
        (AccountsTree.SelectedItem as TreeViewItem)?.Tag as Account;

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => RenderTree(SearchBox.Text.Trim());

    private async void OnAddAccount(object sender, RoutedEventArgs e)
    {
        var parent = SelectedAccount;
        var dialog = new AccountDialog(_accounts, parent) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, RoutedEventArgs e)
    {
        var account = SelectedAccount;
        if (account is null)
        {
            MessageBox.Show("اختر حساباً من الشجرة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = AccountDialog.ForEdit(_accounts, account);
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnDeactivate(object sender, RoutedEventArgs e)
    {
        var account = SelectedAccount;
        if (account is null)
        {
            MessageBox.Show("اختر حساباً من الشجرة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"إيقاف الحساب {account.Code} — {account.NameAr}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var entity = await db.Accounts.SingleAsync(a => a.Id == account.Id);
            entity.Deactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
