using Brqosh.Desktop.Automation;
﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Views;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class OrganizationsPage : UserControl
{
    private readonly MainWindow? _shell;

    public record CompanyRow(long Id, string NameAr, string ProfileLabel, string VatNumber, string ModeLabel);

    public OrganizationsPage(MainWindow shell)
    {
        _shell = shell;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (ProvisionBtn, PermissionService.CanManageUsers));
            await ReloadAsync();
        };
    }

    private async Task ReloadAsync()
    {
        await using var db = AppServices.CreateDb();
        var rows = await db.Companies
            .OrderBy(c => c.NameAr)
            .Select(c => new CompanyRow(
                c.Id,
                c.NameAr,
                ProfileLabel(c.Profile),
                c.VatRegistrationNumber,
                ModeLabel(c.OperationalMode)))
            .ToListAsync();
        Grid.ItemsSource = rows;
        UiTestAutomation.PublishGridCount(Grid, rows.Count, pageKey: "organizations");
    }

    private static string ProfileLabel(BusinessProfile profile) => profile switch
    {
        BusinessProfile.Construction => LocaleManager.T("مقاولات", "Contracting"),
        BusinessProfile.Manufacturing => LocaleManager.T("تصنيع", "Manufacturing"),
        BusinessProfile.Retail => LocaleManager.T("تجزئة", "Retail"),
        BusinessProfile.Healthcare => LocaleManager.T("صحي", "Healthcare"),
        BusinessProfile.Logistics => LocaleManager.T("لوجستيات", "Logistics"),
        BusinessProfile.RealEstate => LocaleManager.T("عقارات", "Real estate"),
        BusinessProfile.Hospitality => LocaleManager.T("ضيافة", "Hospitality"),
        _ => LocaleManager.T("عام", "General"),
    };

    private static string ModeLabel(CompanyOperationalMode mode) => mode switch
    {
        CompanyOperationalMode.ReadOnly => LocaleManager.T("قراءة فقط", "Read-only"),
        CompanyOperationalMode.Archived => LocaleManager.T("مؤرشف", "Archived"),
        _ => LocaleManager.T("نشط", "Active"),
    };

    private void OnProvision(object sender, RoutedEventArgs e)
    {
        var dialog = new OrganizationProvisionerWindow { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.Result is not null)
            _ = AfterProvisionAsync(dialog.Result.CompanyId, dialog.Result.CompanyName);
    }

    private async Task AfterProvisionAsync(long companyId, string companyName)
    {
        await ReloadAsync();
        var answer = UiDialog.Show(
            $"تم إنشاء المنشأة «{companyName}».\nهل تريد التبديل إليها الآن؟",
            "BRQOSH", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes && _shell is not null)
            await _shell.SwitchToCompanyAsync(companyId);
    }

    private async void OnSwitch(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is not CompanyRow row || _shell is null)
            return;
        await _shell.SwitchToCompanyAsync(row.Id);
    }
}
