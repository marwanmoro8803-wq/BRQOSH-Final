using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class BranchesPage : UserControl
{
    public record Row(long Id, string Code, string NameAr, bool IsHeadquarters, string HeadquartersAr, string ActiveAr);

    private List<Branch> _branches = [];

    public BranchesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (NewBtn, PermissionService.CanPostAccounting),
                (DeactivateBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        _branches = await db.Branches
            .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
            .OrderBy(b => b.Code)
            .ToListAsync();

        var rows = _branches.Select(b => new Row(
            b.Id, b.Code, b.NameAr, b.IsHeadquarters,
            b.IsHeadquarters ? "نعم" : "—",
            b.IsActive ? "نعم" : "لا")).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        var hq = rows.Count(r => r.IsHeadquarters);
        BranchSummaryText.Text = $"فروع نشطة: {rows.Count} · رئيسي: {hq}";
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new BranchDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnDeactivate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Row row) return;
        if (row.IsHeadquarters)
        {
            UiFeedback.ShowWarning("لا يمكن إيقاف الفرع الرئيسي.");
            return;
        }

        if (MessageBox.Show($"إيقاف الفرع {row.NameAr}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var branch = await db.Branches.SingleAsync(b => b.Id == row.Id);
            branch.Deactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
