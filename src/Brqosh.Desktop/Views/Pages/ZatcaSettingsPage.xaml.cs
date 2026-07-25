using System.Windows;
using System.Windows.Controls;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;

namespace Brqosh.Desktop.Views.Pages;

public partial class ZatcaSettingsPage : UserControl
{
    public ZatcaSettingsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (SaveBtn, PermissionService.CanManageUsers),
                (ValidateBtn, PermissionService.CanManageUsers),
                (CsidWizardBtn, PermissionService.CanManageUsers));
            await LoadAsync();
        };
    }

    private async Task LoadAsync()
    {
        await using var db = AppServices.CreateDb();
        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == AppServices.CompanyId)
            ?? ZatcaSettings.CreateDefault(AppServices.CompanyId);

        EnabledCheck.IsChecked = settings.IsEnabled;
        EnvCombo.SelectedIndex = settings.Environment == ZatcaEnvironment.Production ? 1 : 0;
        ApiUrlBox.Text = settings.ApiBaseUrl;
        ApiTokenBox.Text = ZatcaSettingsSecrets.GetApiToken(settings);
        CertPathBox.Text = settings.CertificateFilePath;
        KeyPathBox.Text = settings.PrivateKeyFilePath;

        await ShowStatusAsync();
    }

    private async void OnSave(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(SaveAsync);

    private async Task SaveAsync()
    {
        await using var db = AppServices.CreateDb();
        var settings = await db.ZatcaSettings.FirstOrDefaultAsync(s => s.CompanyId == AppServices.CompanyId);
        if (settings is null)
        {
            settings = ZatcaSettings.CreateDefault(AppServices.CompanyId);
            db.ZatcaSettings.Add(settings);
        }

        var env = EnvCombo.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "Production"
            ? ZatcaEnvironment.Production
            : ZatcaEnvironment.Simulation;

        ZatcaSettingsSecrets.ApplyProtectedUpdate(settings, EnabledCheck.IsChecked == true, env,
            ApiUrlBox.Text, CertPathBox.Text, KeyPathBox.Text, ApiTokenBox.Text);
        await db.SaveChangesAsync();
        StatusText.Text = "تم الحفظ.";
        await ShowStatusAsync();
    }

    private async void OnValidate(object sender, RoutedEventArgs e) => await UiFeedback.RunSafeAsync(ShowStatusAsync);

    private void OnCsidWizard(object sender, RoutedEventArgs e)
    {
        var wizard = new Views.ZatcaCsidWizardWindow { Owner = Window.GetWindow(this) };
        wizard.ShowDialog();
        _ = LoadAsync();
    }

    private async Task ShowStatusAsync()
    {
        await using var db = AppServices.CreateDb();
        var status = await new ZatcaConfigurationValidator(db).ValidateAsync(AppServices.CompanyId);
        var ready = status.ReadyForSubmission ? "جاهز للإرسال ✓" : "غير جاهز";
        StatusText.Text = $"{ready}\n" + string.Join("\n", status.Warnings);
    }
}
