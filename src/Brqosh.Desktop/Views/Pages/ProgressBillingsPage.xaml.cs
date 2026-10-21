using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Reporting;
using Brqosh.Desktop.Security;
using Brqosh.Infrastructure.Security;
using Brqosh.Domain.Construction;
using Brqosh.Infrastructure.Construction;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class ProgressBillingsPage : UserControl
{
    public record BillingRow(long Id, string BillingNumber, string ProjectName, string PeriodDescription,
        string BillingDate, decimal CurrentWorkAmount, decimal RetentionAmount,
        decimal NetBillableAmount, string StatusAr, ProgressBillingStatus Status, long? JournalEntryId);

    public ProgressBillingsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddBillingBtn, PermissionService.CanPostAccounting),
                (CancelBillingBtn, PermissionService.CanCancelOrReverse));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var billings = await db.ProgressBillings
            .Where(b => b.CompanyId == AppServices.CompanyId)
            .OrderByDescending(b => b.BillingDate)
            .Take(200)
            .ToListAsync();

        var projectIds = billings.Select(b => b.ProjectId).Distinct().ToList();
        var projects = await db.ConstructionProjects.Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.NameAr);

        Grid.ItemsSource = billings.Select(b => new BillingRow(
            b.Id,
            b.BillingNumber ?? "—",
            projects.GetValueOrDefault(b.ProjectId, "—"),
            b.PeriodDescription,
            b.BillingDate.ToString("yyyy-MM-dd"),
            b.CurrentWorkAmount,
            b.RetentionAmount,
            b.NetBillableAmount,
            DomainLabels.Show(ProgressBilling.StatusLabel(b.Status)),
            b.Status,
            b.JournalEntryId)).ToList();
        UiTestAutomation.PublishGridCount(Grid, billings.Count);

        var posted = billings.Count(b => b.Status == ProgressBillingStatus.Posted);
        BillingSummaryText.Text =
            $"مستخلصات: {billings.Count} · مرحّل: {posted} · أعمال: {billings.Sum(b => b.CurrentWorkAmount):N2} · محتجز: {billings.Sum(b => b.RetentionAmount):N2} · صافي: {billings.Sum(b => b.NetBillableAmount):N2}";
    }

    private async void OnAddBilling(object sender, RoutedEventArgs e)
    {
        if (new ProgressBillingDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private void OnView(object sender, RoutedEventArgs e) => UiFeedback.RunViewSafe(ViewSelected, "ProgressBillingsPage.View");

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e) => UiFeedback.RunViewSafe(ViewSelected, "ProgressBillingsPage.View");

    private void ViewSelected()
    {
        if (Grid.SelectedItem is not BillingRow row)
        {
            UiDialog.Show("اختر مستخلصاً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var details = $"المشروع: {row.ProjectName}\nالفترة: {row.PeriodDescription}\n" +
                      $"التاريخ: {row.BillingDate}\nالأعمال: {row.CurrentWorkAmount:N2}\n" +
                      $"المحتجز: {row.RetentionAmount:N2}\nالصافي: {row.NetBillableAmount:N2}\nالحالة: {row.StatusAr}";

        if (row.JournalEntryId is long entryId)
        {
            new JournalEntryViewDialog(entryId) { Owner = Window.GetWindow(this) }.ShowDialog();
            return;
        }

        UiDialog.Show(details, $"مستخلص {row.BillingNumber}", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnViewJournal(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not BillingRow row)
        {
            UiDialog.Show("اختر مستخلصاً من الجدول.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.JournalEntryId is long entryId)
            new JournalEntryViewDialog(entryId) { Owner = Window.GetWindow(this) }.ShowDialog();
        else
            UiDialog.Show("لا يوجد قيد محاسبي مرتبط.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnPrintPdf(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not BillingRow row)
        {
            UiDialog.Show("اختر مستخلصاً للطباعة.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.Status != ProgressBillingStatus.Posted)
        {
            UiDialog.Show("الطباعة متاحة للمستخلصات المرحّلة فقط.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var username = SecurityContext.ResolveUsername();
        await DocumentPrintHelper.ExportAndDeliverAsync(
            s => s.ExportProgressBillingAsync(AppServices.CompanyId, row.Id, username),
            Window.GetWindow(this),
            new DocumentDeliverOptions(OfferSaveAs: true));
    }

    private async void OnCancelBilling(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not BillingRow row)
        {
            UiDialog.Show("اختر مستخلصاً للإلغاء.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.Status != ProgressBillingStatus.Posted)
        {
            UiDialog.Show("لا يُلغى إلا مستخلص مرحّل.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (UiDialog.Show($"إلغاء المستخلص {row.BillingNumber}؟ سيتم عكس القيد.", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var cancelDate = UiDialogs.PromptAccountingDate(
            Window.GetWindow(this)!, "تاريخ الإلغاء", "تاريخ إلغاء المستخلص وقيد العكس:");
        if (cancelDate is null)
            return;

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<ProgressBillingCancellationService>().CancelAsync(
                AppServices.CompanyId, row.Id, cancelDate.Value, username);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
