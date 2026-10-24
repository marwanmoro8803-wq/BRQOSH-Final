using Brqosh.Desktop.Ui;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Security;
using Brqosh.Domain.Construction;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Automation;

namespace Brqosh.Desktop.Views.Pages;

public partial class ContractsPage : UserControl
{
    public record ContractRow(long Id, string ContractNumber, string ProjectName, string ContractDate,
        decimal ContractValue, string StatusAr, ConstructionContractStatus Status);

    public ContractsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPostAccounting),
                (CompleteBtn, PermissionService.CanPostAccounting),
                (CancelBtn, PermissionService.CanCancelOrReverse));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var contracts = await db.ConstructionContracts
            .Where(c => c.CompanyId == AppServices.CompanyId)
            .OrderByDescending(c => c.ContractDate)
            .Take(200)
            .ToListAsync();

        var projectIds = contracts.Select(c => c.ProjectId).Distinct().ToList();
        var projects = await db.ConstructionProjects.Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.NameAr);

        var rows = contracts.Select(c => new ContractRow(
            c.Id,
            c.ContractNumber,
            projects.GetValueOrDefault(c.ProjectId, "—"),
            c.ContractDate.ToString("yyyy-MM-dd"),
            c.ContractValue,
            DomainLabels.Show(ConstructionContract.StatusLabel(c.Status)),
            c.Status)).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        var active = contracts.Count(c => c.Status == ConstructionContractStatus.Active);
        var totalValue = contracts.Where(c => c.Status == ConstructionContractStatus.Active)
            .Sum(c => c.ContractValue);
        ContractSummaryText.Text =
            $"عقود: {contracts.Count} · نشط: {active} · قيمة نشطة: {totalValue:N2} {CurrencyLabel.Short}";
    }

    private async void OnAddContract(object sender, RoutedEventArgs e)
    {
        if (new ContractDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnComplete(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not ContractRow row)
        {
            UiFeedback.ShowInfo("اختر عقداً.");
            return;
        }

        if (row.Status != ConstructionContractStatus.Active)
        {
            UiFeedback.ShowWarning("يُستكمل العقد النشط فقط.");
            return;
        }

        try
        {
            SecurityContext.EnsureCanPostAccounting();
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var contract = await db.ConstructionContracts.SingleAsync(c => c.Id == row.Id);
            contract.Complete();
            await db.SaveChangesAsync();
            await facade.GetService<AuditService>().LogAsync(AppServices.CompanyId, SecurityContext.ResolveUsername(),
                "استكمال عقد", "ConstructionContract", row.ContractNumber, "");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
        }
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not ContractRow row)
        {
            UiFeedback.ShowInfo("اختر عقداً.");
            return;
        }

        if (row.Status is ConstructionContractStatus.Completed or ConstructionContractStatus.Cancelled)
        {
            UiFeedback.ShowWarning("لا يمكن إلغاء العقد في حالته الحالية.");
            return;
        }

        if (!UiFeedback.Confirm($"إلغاء العقد {row.ContractNumber}؟"))
            return;

        try
        {
            SecurityContext.EnsureCanCancelOrReverse();
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var contract = await db.ConstructionContracts.SingleAsync(c => c.Id == row.Id);
            contract.Cancel();
            await db.SaveChangesAsync();
            await facade.GetService<AuditService>().LogAsync(AppServices.CompanyId, SecurityContext.ResolveUsername(),
                "إلغاء عقد", "ConstructionContract", row.ContractNumber, "");
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowError(ex.Message);
        }
    }
}
