using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Domain.Construction;
using Brqosh.Infrastructure.Construction;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class BoqPage : UserControl
{
    private record ProjectOption(long Id, string Name);

    public BoqPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
            (AddItemBtn, PermissionService.CanPostAccounting),
            (EditItemBtn, PermissionService.CanPostAccounting),
            (DeleteItemBtn, PermissionService.CanCancelOrReverse));
        await using var db = AppServices.CreateDb();
        ProjectBox.ItemsSource = await db.ConstructionProjects
            .Where(p => p.CompanyId == AppServices.CompanyId && p.IsActive)
            .OrderBy(p => p.Code)
            .Select(p => new ProjectOption(p.Id, $"{p.Code} — {p.NameAr}"))
            .ToListAsync();
    }

    private async void OnProjectChanged(object sender, SelectionChangedEventArgs e) => await LoadBoqAsync();

    private async Task LoadBoqAsync()
    {
        if (ProjectBox.SelectedItem is not ProjectOption project)
        {
            Grid.ItemsSource = null;
            UiTestAutomation.PublishGridCount(Grid, 0);
            BoqSummaryText.Text = "اختر مشروعاً لعرض بنود BOQ.";
            return;
        }

        await using var db = AppServices.CreateDb();
        var entity = await db.ConstructionProjects
            .SingleAsync(p => p.CompanyId == AppServices.CompanyId && p.Id == project.Id);

        var items = entity.BoqItems.ToList();
        Grid.ItemsSource = items;

        UiTestAutomation.PublishGridCount(Grid, items.Count);
        BoqSummaryText.Text =
            $"مشروع: {project.Name} · بنود: {items.Count} · إجمالي BOQ: {entity.BoqTotal:N2} ر.س";
    }

    private ProjectOption? SelectedProject =>
        ProjectBox.SelectedItem as ProjectOption;

    private async Task SaveItemsAsync(long projectId, List<BoqItem> items)
    {
        using var facade = new DesktopAccountingFacade();
        await facade.GetService<ConstructionProjectService>().ReplaceBoqAsync(AppServices.CompanyId, projectId, items);
        await LoadBoqAsync();
    }

    private async void OnAddItem(object sender, RoutedEventArgs e)
    {
        if (SelectedProject is not { } project)
        {
            UiDialog.Show("اختر المشروع أولاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new BoqItemDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var entity = await db.ConstructionProjects
                .SingleAsync(p => p.CompanyId == AppServices.CompanyId && p.Id == project.Id);

            var items = entity.BoqItems.ToList();
            items.Add(dialog.Result);
            await SaveItemsAsync(project.Id, items);
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnEditItem(object sender, RoutedEventArgs e) => await EditSelectedAsync();

    private async void OnGridDoubleClick(object sender, MouseButtonEventArgs e) => await EditSelectedAsync();

    private async Task EditSelectedAsync()
    {
        if (SelectedProject is not { } project)
        {
            UiDialog.Show("اختر المشروع أولاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (Grid.SelectedItem is not BoqItem selected)
        {
            UiDialog.Show("اختر بنداً للتعديل.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new BoqItemDialog(selected) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var entity = await db.ConstructionProjects
                .SingleAsync(p => p.CompanyId == AppServices.CompanyId && p.Id == project.Id);

            var items = entity.BoqItems
                .Where(i => i.ItemNumber != selected.ItemNumber)
                .ToList();
            items.Add(dialog.Result);
            await SaveItemsAsync(project.Id, items);
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnDeleteItem(object sender, RoutedEventArgs e)
    {
        if (SelectedProject is not { } project)
        {
            UiDialog.Show("اختر المشروع أولاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (Grid.SelectedItem is not BoqItem selected)
        {
            UiDialog.Show("اختر بنداً للحذف.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (UiDialog.Show($"حذف البند {selected.ItemNumber}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var entity = await db.ConstructionProjects
                .SingleAsync(p => p.CompanyId == AppServices.CompanyId && p.Id == project.Id);

            var items = entity.BoqItems
                .Where(i => i.ItemNumber != selected.ItemNumber)
                .ToList();
            await SaveItemsAsync(project.Id, items);
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
