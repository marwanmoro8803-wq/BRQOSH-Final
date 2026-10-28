using System.Windows;
using Brqosh.Desktop.Automation;
using System.Windows.Controls;
using Brqosh.Application.Accounting;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class PostingAccountsPage : UserControl
{
    public record RoleRow(string Role, string Label, string DefaultCode, string Account, string CustomLabel, string AccountCode);
    public record AccountChoice(string Code, string Label);

    private static readonly Dictionary<string, (string Ar, string En)> RoleNames = new(StringComparer.Ordinal)
    {
        ["Purchases"] = ("المشتريات", "Purchases"),
        ["MiscellaneousExpenses"] = ("مصروفات متنوعة", "Miscellaneous expenses"),
        ["GosiExpense"] = ("مصروف التأمينات الاجتماعية", "GOSI expense"),
        ["SalariesPayable"] = ("رواتب مستحقة", "Salaries payable"),
        ["GosiPayable"] = ("التأمينات الاجتماعية المستحقة", "GOSI payable"),
        ["SalesDiscounts"] = ("خصومات المبيعات", "Sales discounts"),
        ["PurchaseDiscountsEarned"] = ("خصومات المشتريات المكتسبة", "Purchase discounts earned"),
        ["Allowances"] = ("البدلات", "Allowances"),
        ["EndOfServiceExpense"] = ("مصروف نهاية الخدمة", "End-of-service expense"),
        ["EndOfServiceProvision"] = ("مخصص نهاية الخدمة", "End-of-service provision"),
        ["MaintenanceExpense"] = ("مصروف الصيانة", "Maintenance expense"),
        ["DirectLabour"] = ("العمالة المباشرة", "Direct labour"),
        ["ManufacturingOverhead"] = ("تكاليف التصنيع غير المباشرة", "Manufacturing overhead"),
        ["ProjectCosts"] = ("تكاليف المشاريع", "Project costs"),
        ["DepreciationExpense"] = ("مصروف الإهلاك", "Depreciation expense"),
    };

    public PostingAccountsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AssignBtn, PermissionService.CanManageUsers),
                (ResetBtn, PermissionService.CanManageUsers),
                (AccountBox, PermissionService.CanManageUsers));
            await LoadAccountsAsync();
            await ReloadAsync();
        };
    }

    private static string RoleLabel(string role) =>
        RoleNames.TryGetValue(role, out var n) ? (LocaleManager.IsEnglish ? n.En : n.Ar) : role;

    private async Task LoadAccountsAsync()
    {
        await using var db = AppServices.CreateDb();
        AccountBox.ItemsSource = await db.Accounts
            .Where(a => a.CompanyId == AppServices.CompanyId && a.IsActive && !a.IsGroup)
            .OrderBy(a => a.Code)
            .Select(a => new AccountChoice(a.Code, a.Code + " — " + a.NameAr))
            .ToListAsync();
    }

    private async Task ReloadAsync()
    {
        using var facade = new DesktopAccountingFacade();
        var list = await facade.GetService<IPostingRoleService>().ListAsync(AppServices.CompanyId);
        var rows = list.Select(r => new RoleRow(r.Role, RoleLabel(r.Role), r.DefaultCode,
                r.AccountNameAr is null ? r.AccountCode : r.AccountCode + " — " + r.AccountNameAr,
                r.IsCustom ? LocaleManager.T("نعم", "Yes") : "", r.AccountCode))
            .OrderBy(r => r.Label).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count, pageKey: "posting_accounts");
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is RoleRow row) AccountBox.SelectedValue = row.AccountCode;
    }

    private async void OnAssign(object sender, RoutedEventArgs e)
    {
        if (!PermissionService.CanManageUsers(AppServices.CurrentUser)) return;
        if (Grid.SelectedItem is not RoleRow row || AccountBox.SelectedValue is not string code)
        {
            UiDialog.Show("اختر دوراً وحساباً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<IPostingRoleService>().SetAsync(AppServices.CompanyId, row.Role, code);
            await ReloadAsync();
        }
        catch (Exception)
        {
            UiDialog.Show("تعذّر حفظ حساب الترحيل.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnReset(object sender, RoutedEventArgs e)
    {
        if (!PermissionService.CanManageUsers(AppServices.CurrentUser)) return;
        if (Grid.SelectedItem is not RoleRow row)
        {
            UiDialog.Show("اختر دوراً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (UiDialog.Show("إرجاع الحساب الافتراضي لهذا الدور؟", "BRQOSH", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            await facade.GetService<IPostingRoleService>().ResetAsync(AppServices.CompanyId, row.Role);
            await ReloadAsync();
        }
        catch (Exception)
        {
            UiDialog.Show("تعذّر حفظ حساب الترحيل.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}