using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Domain.Construction;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class ProjectsPage : UserControl
{
    public record ProjectRow(long Id, string Code, string NameAr, string CustomerName,
        decimal ContractValue, decimal RetentionPercent, decimal BoqTotal, string StatusAr);

    private List<ConstructionProject> _projects = [];
    private IReadOnlyDictionary<long, string> _customers = new Dictionary<long, string>();

    public ProjectsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddProjectBtn, PermissionService.CanPostAccounting),
                (AddCostBtn, PermissionService.CanPostAccounting),
                (DeactivateBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        _projects = await db.ConstructionProjects
            .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
            .OrderBy(p => p.Code)
            .Take(500)
            .ToListAsync();

        var customerIds = _projects.Where(p => p.CustomerId is long id).Select(p => p.CustomerId!.Value).Distinct().ToList();
        _customers = await db.Customers.Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);

        Render(SearchBox.Text.Trim());
    }

    private void Render(string filter)
    {
        var visible = string.IsNullOrEmpty(filter)
            ? _projects
            : _projects.Where(p => p.Code.Contains(filter) || p.NameAr.Contains(filter)).ToList();

        Grid.ItemsSource = visible.Select(p => new ProjectRow(
            p.Id, p.Code, p.NameAr,
            p.CustomerId is long cid ? _customers.GetValueOrDefault(cid, "—") : "—",
            p.ContractValue, p.RetentionPercent, p.BoqTotal,
            DomainLabels.Show(ConstructionProject.StatusLabel(p.Status)))).ToList();
        UiTestAutomation.PublishGridCount(Grid, visible.Count);
        RenderKpis(visible);
    }

    private void RenderKpis(IReadOnlyList<ConstructionProject> visible)
    {
        KpiGrid.Children.Clear();
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("مشاريع نشطة", "Active projects"), "PJ", visible.Count, "#0D9488", KpiValueKind.Count));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("قيمة العقود", "Contract value"), "CV",
            visible.Sum(p => p.ContractValue), "#2563EB", KpiValueKind.Currency));
        KpiGrid.Children.Add(KpiMetricCardBuilder.Create(this,
            LocaleManager.T("إجمالي BOQ", "BOQ total"), "BQ",
            visible.Sum(p => p.BoqTotal), "#7C3AED", KpiValueKind.Currency));
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => Render(SearchBox.Text.Trim());

    private async void OnAddProject(object sender, RoutedEventArgs e)
    {
        if (new ProjectDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnAddCost(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not ProjectRow row)
        {
            UiFeedback.ShowWarning("حدد مشروعاً أولاً لتسجيل التكلفة.");
            return;
        }

        if (new ProjectCostDialog(row.Id, row.NameAr) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is not ProjectRow row) return;
        var project = _projects.Single(p => p.Id == row.Id);
        if (new ProjectDialog(project) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnDeactivate(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not ProjectRow row) return;
        if (UiDialog.Show($"إيقاف المشروع {row.NameAr}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var project = await db.ConstructionProjects.SingleAsync(p => p.Id == row.Id);
            project.Deactivate();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
