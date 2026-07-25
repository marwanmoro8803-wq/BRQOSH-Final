using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Automation;

namespace Brqosh.Desktop.Views.Pages;

public partial class CompanyGroupsPage : UserControl
{
    private long? _selectedGroupId;

    public CompanyGroupsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanManageUsers),
                (AddMapBtn, PermissionService.CanManageUsers),
                (RemoveMapBtn, PermissionService.CanManageUsers));
            await UiFeedback.RunSafeAsync(LoadAsync);
        };
    }

    private async void OnAdd(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(AddAsync);
    private async void OnGroupSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        await UiFeedback.RunSafeAsync(LoadMapsAsync);
    private async void OnAddMap(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(AddMapAsync);
    private async void OnRemoveMap(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(RemoveMapAsync);

    private async Task LoadAsync()
    {
        await using var db = AppServices.CreateDb();
        var rows = await db.CompanyGroups
            .OrderBy(g => g.Code)
            .Select(g => new
            {
                g.Id,
                g.Code,
                g.NameAr,
                g.ReportingCurrencyCode,
                CompanyCount = db.Companies.Count(c => c.CompanyGroupId == g.Id),
            })
            .ToListAsync();
        GroupsGrid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(GroupsGrid, rows.Count);
        var totalCompanies = rows.Sum(r => (int)r.CompanyCount);
        GroupSummaryText.Text = $"مجموعات: {rows.Count} · شركات مرتبطة: {totalCompanies}";
        if (rows.Count > 0 && GroupsGrid.SelectedItem is null)
            GroupsGrid.SelectedIndex = 0;
    }

    private async Task LoadMapsAsync()
    {
        if (GroupsGrid.SelectedItem is null)
        {
            _selectedGroupId = null;
            MapsGrid.ItemsSource = null;
            UiTestAutomation.PublishGridCount(MapsGrid, 0);
            return;
        }

        dynamic row = GroupsGrid.SelectedItem;
        _selectedGroupId = (long)row.Id;

        await using var db = AppServices.CreateDb();
        var maps = await db.IntercompanyAccountMaps
            .Where(m => m.CompanyGroupId == _selectedGroupId)
            .OrderBy(m => m.AccountCode)
            .Select(m => new { m.AccountCode })
            .ToListAsync();
        MapsGrid.ItemsSource = maps;
        UiTestAutomation.PublishGridCount(MapsGrid, maps.Count);
    }

    private async Task AddAsync()
    {
        try
        {
            var group = CompanyGroup.Create(CodeBox.Text, NameBox.Text, CurrencyBox.Text);
            await using var db = AppServices.CreateDb();
            db.CompanyGroups.Add(group);
            await db.SaveChangesAsync();
            ErrorText.Visibility = Visibility.Collapsed;
            CodeBox.Clear();
            NameBox.Clear();
            await LoadAsync();
            UiFeedback.ShowSuccess("تمت إضافة المجموعة.");
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private async Task AddMapAsync()
    {
        if (_selectedGroupId is null)
        {
            UiFeedback.ShowWarning("اختر مجموعة أولاً.");
            return;
        }

        try
        {
            var map = IntercompanyAccountMap.Create(_selectedGroupId.Value, AccountCodeBox.Text);
            await using var db = AppServices.CreateDb();
            db.IntercompanyAccountMaps.Add(map);
            await db.SaveChangesAsync();
            AccountCodeBox.Clear();
            await LoadMapsAsync();
            UiFeedback.ShowSuccess("تمت إضافة الحساب لخريطة IC.");
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
        }
    }

    private async Task RemoveMapAsync()
    {
        if (_selectedGroupId is null || MapsGrid.SelectedItem is null)
        {
            UiFeedback.ShowWarning("اختر حساباً من الخريطة.");
            return;
        }

        dynamic row = MapsGrid.SelectedItem;
        var code = (string)row.AccountCode;

        await using var db = AppServices.CreateDb();
        var map = await db.IntercompanyAccountMaps.SingleOrDefaultAsync(m =>
            m.CompanyGroupId == _selectedGroupId && m.AccountCode == code);
        if (map is null)
            return;

        db.IntercompanyAccountMaps.Remove(map);
        await db.SaveChangesAsync();
        await LoadMapsAsync();
    }
}
