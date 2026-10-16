using Brqosh.Desktop.Ui;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Crm;
using Brqosh.Desktop.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views.Pages;

public partial class CrmOpportunitiesPage : UserControl
{
    public record OppRow(long Id, string Title, string CustomerName, decimal Amount, string StageLabel, string ExpectedClose);

    public CrmOpportunitiesPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser, (AddBtn, PermissionService.CanPost));
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var customers = await db.Customers
            .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
            .ToDictionaryAsync(c => c.Id, c => c.NameAr);
        var rows = await db.CrmOpportunities
            .Where(o => o.CompanyId == AppServices.CompanyId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(200)
            .ToListAsync();
        Grid.ItemsSource = rows.Select(o => new OppRow(
            o.Id,
            o.Title,
            o.CustomerId is long cid && customers.TryGetValue(cid, out var n) ? n : "—",
            o.Amount,
            StageLabel(o.Stage),
            o.ExpectedCloseDate?.ToString("yyyy-MM-dd") ?? "—")).ToList();
    }

    private static string StageLabel(CrmOpportunityStage stage) => stage switch
    {
        CrmOpportunityStage.Qualification => LocaleManager.T("تأهيل", "Qualification"),
        CrmOpportunityStage.Proposal => LocaleManager.T("عرض", "Proposal"),
        CrmOpportunityStage.Negotiation => LocaleManager.T("تفاوض", "Negotiation"),
        CrmOpportunityStage.Won => LocaleManager.T("فوز", "Won"),
        CrmOpportunityStage.Lost => LocaleManager.T("خسارة", "Lost"),
        _ => stage.ToString()
    };

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (new CrmOpportunityDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }
}
