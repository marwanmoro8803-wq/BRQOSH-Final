using System.Windows;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop.Views;

public partial class ZatcaCsidWizardWindow : Window
{
    private ZatcaKeyMaterial? _keyMaterial;
    private ZatcaCsidResult? _complianceResult;

    public ZatcaCsidWizardWindow()
    {
        InitializeComponent();
    }

    private async void OnGenerate(object sender, RoutedEventArgs e)
    {
        try
        {
            ErrorText.Visibility = Visibility.Collapsed;
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var company = await db.Companies.SingleAsync(c => c.Id == AppServices.CompanyId);

            var onboarding = facade.GetService<ZatcaCsidOnboardingService>();
            _keyMaterial = onboarding.GenerateKeyPairAndCsr(
                AppServices.CompanyId, company.NameAr, company.VatRegistrationNumber);

            CsrStatusText.Text = $"تم التوليد.\nالمفتاح: {_keyMaterial.PrivateKeyPath}\nCSR: {_keyMaterial.CsrPath}";
            ResultText.Text = "جاهز لطلب شهادة الامتثال — أدخل OTP من بوابة فاتورة.";
        }
        catch (Exception ex)
        {
            ShowError(ex is AccountingException ae ? ae.Message : ex.Message);
        }
    }

    private async void OnCompliance(object sender, RoutedEventArgs e)
    {
        if (_keyMaterial is null)
        {
            ShowError("ولّد المفتاح و CSR أولاً.");
            return;
        }

        try
        {
            ErrorText.Visibility = Visibility.Collapsed;
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var settings = await db.ZatcaSettings.SingleAsync(s => s.CompanyId == AppServices.CompanyId);
            var apiUrl = string.IsNullOrWhiteSpace(settings.ApiBaseUrl)
                ? "https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal"
                : settings.ApiBaseUrl;

            var onboarding = facade.GetService<ZatcaCsidOnboardingService>();
            _complianceResult = await onboarding.RequestComplianceCsidAsync(
                apiUrl, OtpBox.Text, _keyMaterial.CsrBase64, AppServices.CompanyId);

            var token = ZatcaCsidOnboardingService.BuildApiToken(
                _complianceResult.BinarySecurityToken, _complianceResult.Secret);

            ZatcaSettingsSecrets.ApplyProtectedCsid(
                settings,
                _complianceResult.CertificatePemPath,
                _complianceResult.PrivateKeyPemPath,
                token,
                _complianceResult.RequestId,
                _complianceResult.Secret);
            await db.SaveChangesAsync();

            ResultText.Text = $"شهادة الامتثال جاهزة.\nRequest ID: {_complianceResult.RequestId}\nالشهادة: {_complianceResult.CertificatePemPath}";
        }
        catch (Exception ex)
        {
            ShowError(ex is AccountingException ae ? ae.Message : ex.Message);
        }
    }

    private async void OnProduction(object sender, RoutedEventArgs e)
    {
        if (_complianceResult is null)
        {
            ShowError("اطلب شهادة الامتثال أولاً.");
            return;
        }

        try
        {
            ErrorText.Visibility = Visibility.Collapsed;
            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var settings = await db.ZatcaSettings.SingleAsync(s => s.CompanyId == AppServices.CompanyId);
            var apiUrl = settings.ApiBaseUrl.Replace("developer-portal", "core", StringComparison.OrdinalIgnoreCase);
            if (!apiUrl.Contains("core", StringComparison.OrdinalIgnoreCase))
                apiUrl = "https://gw-fatoora.zatca.gov.sa/e-invoicing/core";

            var onboarding = facade.GetService<ZatcaCsidOnboardingService>();
            var result = await onboarding.RequestProductionCsidAsync(
                apiUrl, _complianceResult.RequestId,
                _complianceResult.BinarySecurityToken, _complianceResult.Secret,
                AppServices.CompanyId);

            var token = ZatcaCsidOnboardingService.BuildApiToken(result.BinarySecurityToken, result.Secret);
            ZatcaSettingsSecrets.ApplyProtectedCsid(
                settings, result.CertificatePemPath, result.PrivateKeyPemPath, token, result.RequestId, result.Secret);
            ZatcaSettingsSecrets.ApplyProtectedUpdate(settings, true, ZatcaEnvironment.Production, apiUrl,
                result.CertificatePemPath, result.PrivateKeyPemPath, token);
            await db.SaveChangesAsync();

            ResultText.Text = $"شهادة الإنتاج جاهزة.\nالشهادة: {result.CertificatePemPath}\nيمكنك الآن إرسال الفواتير للهيئة.";
            MessageBox.Show("تم تسجيل CSID للإنتاج بنجاح.", "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError(ex is AccountingException ae ? ae.Message : ex.Message);
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
