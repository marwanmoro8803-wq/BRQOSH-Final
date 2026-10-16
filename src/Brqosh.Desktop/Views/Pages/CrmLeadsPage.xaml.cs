using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Crm;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Crm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop.Views.Pages;

public partial class CrmLeadsPage : UserControl
{
    public record LeadRow(long Id, string Name, string Phone, string Email, string Source, string StatusLabel);

    public CrmLeadsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBtn, PermissionService.CanPost),
                (ConvertBtn, PermissionService.CanPost));
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var leads = await db.CrmLeads
            .Where(l => l.CompanyId == AppServices.CompanyId && l.IsActive)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(200)
            .ToListAsync();
        LeadsGrid.ItemsSource = leads.Select(l => new LeadRow(
            l.Id, l.Name, l.Phone, l.Email, l.Source, StatusLabel(l.Status))).ToList();
        EmptyStateHelper.Sync(LeadsGrid, leads.Count, pageKey: "crm_leads");
    }

    private static string StatusLabel(CrmLeadStatus status) => status switch
    {
        CrmLeadStatus.New => LocaleManager.T("جديد", "New"),
        CrmLeadStatus.Qualified => LocaleManager.T("مؤهّل", "Qualified"),
        CrmLeadStatus.Converted => LocaleManager.T("مُحوَّل", "Converted"),
        CrmLeadStatus.Disqualified => LocaleManager.T("مرفوض", "Rejected"),
        _ => status.ToString()
    };

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new CrmLeadDialog { Owner = Window.GetWindow(this) }.ShowDialog() != true)
            return;
        await ReloadAsync();
    }

    private async void OnConvert(object sender, RoutedEventArgs e)
    {
        if (LeadsGrid.SelectedItem is not LeadRow row) return;
        using var scope = AppServices.CreateInfrastructureScope();
        var crm = scope.ServiceProvider.GetRequiredService<CrmService>();
        await crm.ConvertLeadAsync(AppServices.CompanyId, row.Id);
        UiDialog.Show("تم تحويل العميل المحتمل إلى عميل في دليل العملاء.", "CRM",
            MessageBoxButton.OK, MessageBoxImage.Information);
        await ReloadAsync();
    }
}
