using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Construction;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Construction;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class SubcontractorsPage : UserControl
{
    public record AgreementRow(long Id, string AgreementNumber, string ProjectName, string SupplierName,
        decimal ContractValue, decimal RetentionPercent, string StatusAr, SubcontractAgreementStatus Status);

    public SubcontractorsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddAgreementBtn, PermissionService.CanPostAccounting),
                (WithholdBtn, PermissionService.CanPostAccounting),
                (CompleteBtn, PermissionService.CanPostAccounting),
                (CancelBtn, PermissionService.CanCancelOrReverse));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var agreements = await db.SubcontractAgreements
            .Where(a => a.CompanyId == AppServices.CompanyId)
            .OrderByDescending(a => a.AgreementDate)
            .Take(200)
            .ToListAsync();

        var projectIds = agreements.Select(a => a.ProjectId).Distinct().ToList();
        var supplierIds = agreements.Select(a => a.SupplierId).Distinct().ToList();

        var projects = await db.ConstructionProjects.Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.NameAr);
        var suppliers = await db.Suppliers.Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.NameAr);

        var rows = agreements.Select(a => new AgreementRow(
            a.Id,
            a.AgreementNumber,
            projects.GetValueOrDefault(a.ProjectId, "—"),
            suppliers.GetValueOrDefault(a.SupplierId, "—"),
            a.ContractValue,
            a.RetentionPercent,
            DomainLabels.Show(SubcontractAgreement.StatusLabel(a.Status)),
            a.Status)).ToList();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count);
        var active = agreements.Count(a => a.Status == SubcontractAgreementStatus.Active);
        var activeValue = agreements.Where(a => a.Status == SubcontractAgreementStatus.Active)
            .Sum(a => a.ContractValue);
        SubcontractorSummaryText.Text =
            $"عقود باطن: {agreements.Count} · نشط: {active} · قيمة نشطة: {activeValue:N2} ر.س";
    }

    private async void OnAddAgreement(object sender, RoutedEventArgs e)
    {
        if (new SubcontractDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnWithholdRetention(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not AgreementRow row)
        {
            UiDialog.Show("اختر عقد باطن.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.Status != SubcontractAgreementStatus.Active)
        {
            UiDialog.Show("يُسجَّل المحتجز لعقد باطن نشط فقط.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new TextInputDialog("تسجيل محتجز باطن",
            $"مبلغ المحتجز لعقد {row.AgreementNumber}:", "0")
            { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
            return;

        if (!NumericInput.TryParse(dialog.Value, out var amount) || amount <= 0)
        {
            UiDialog.Show("أدخل مبلغاً صحيحاً أكبر من صفر.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var username = SecurityContext.ResolveUsername();
            await facade.GetService<SubcontractRetentionWithholdService>().WithholdAsync(
                AppServices.CompanyId, row.Id, amount, DateOnly.FromDateTime(DateTime.Today),
                "محتجز مستحق", username);
            UiDialog.Show("تم ترحيل قيد المحتجز.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnComplete(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not AgreementRow row)
        {
            UiDialog.Show("اختر عقد باطن.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.Status != SubcontractAgreementStatus.Active)
        {
            UiDialog.Show("يُستكمل عقد الباطن النشط فقط.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            await using var db = AppServices.CreateDb();
            var agreement = await db.SubcontractAgreements.SingleAsync(a => a.Id == row.Id);
            agreement.Complete();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnCancel(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not AgreementRow row)
        {
            UiDialog.Show("اختر عقد باطن.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (row.Status is SubcontractAgreementStatus.Completed or SubcontractAgreementStatus.Cancelled)
        {
            UiDialog.Show("لا يمكن إلغاء العقد في حالته الحالية.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (UiDialog.Show($"إلغاء عقد الباطن {row.AgreementNumber}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = AppServices.CreateDb();
            var agreement = await db.SubcontractAgreements.SingleAsync(a => a.Id == row.Id);
            agreement.Cancel();
            await db.SaveChangesAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiDialog.Show(ex.Message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
