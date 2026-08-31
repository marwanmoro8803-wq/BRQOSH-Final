using System.Windows;
using System.Windows.Controls;
using Brqosh.Infrastructure.Construction;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

using Brqosh.Infrastructure.Security;
using Brqosh.Desktop.Security;

namespace Brqosh.Desktop.Views.Pages;

public partial class RetentionsPage : UserControl
{
    private List<RetentionSummaryQuery.RetentionRow> _rows = [];

    public RetentionsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (ReleaseCustomerBtn, PermissionService.CanPostAccounting),
                (ReleaseSubBtn, PermissionService.CanPostAccounting));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        _rows = (await new RetentionSummaryQuery(db).ListAsync(AppServices.CompanyId)).ToList();
        Grid.ItemsSource = _rows;
        UiTestAutomation.PublishGridCount(Grid, _rows.Count);
        RetentionSummaryText.Text =
            $"مشاريع: {_rows.Count} · محتجز عميل: {_rows.Sum(r => r.CustomerRetentionHeld):N2} · محتجز باطن: {_rows.Sum(r => r.SubcontractRetentionHeld):N2}";
    }

    private async void OnReleaseCustomer(object sender, RoutedEventArgs e) =>
        await ReleaseAsync(RetentionReleaseType.CustomerRelease, r => r.CustomerRetentionAvailable);

    private async void OnReleaseSubcontract(object sender, RoutedEventArgs e) =>
        await ReleaseAsync(RetentionReleaseType.SubcontractRelease, r => r.SubcontractRetentionAvailable);

    private async Task ReleaseAsync(
        RetentionReleaseType type, Func<RetentionSummaryQuery.RetentionRow, decimal> maxAmount)
    {
        if (Grid.SelectedItem is not RetentionSummaryQuery.RetentionRow row)
        {
            UiDialog.Show("اختر مشروعاً.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var max = maxAmount(row);
        if (max <= 0)
        {
            UiDialog.Show("لا يوجد محتجز للإفراج.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new RetentionReleaseDialog(row.ProjectName, max) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
            return;

        if (dialog.Amount > max)
        {
            UiDialog.Show($"المبلغ يتجاوز المحتجز ({max:N2}).", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<RetentionReleaseService>().ReleaseAsync(
                AppServices.CompanyId, row.ProjectId, type, dialog.Amount,
                DateOnly.FromDateTime(DateTime.Today), "إفراج محتجز", username);

            UiDialog.Show("تم ترحيل قيد الإفراج.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
