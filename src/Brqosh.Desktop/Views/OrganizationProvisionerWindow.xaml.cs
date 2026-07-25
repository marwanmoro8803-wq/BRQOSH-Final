using System.Windows;
using Brqosh.Application.Setup;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Setup;

namespace Brqosh.Desktop.Views;

public partial class OrganizationProvisionerWindow : Window
{
    private readonly IConfigurationTemplateLoader _templates = new ConfigurationTemplateLoader();

    public OrganizationProvisionResult? Result { get; private set; }

    public OrganizationProvisionerWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        ProfileCombo.SelectionChanged += (_, _) => UpdatePreview();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ProfileCombo.ItemsSource = _templates.ListIndustries();
        ProfileCombo.SelectedIndex = 0;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (ProfileCombo.SelectedItem is not IndustryTemplateSummary summary)
        {
            IndustryPreviewText.Text = "";
            return;
        }

        var industry = _templates.LoadIndustry(summary.BusinessProfile);
        IndustryPreviewText.Text =
            $"وحدات: {string.Join(" · ", industry.Modules)} — ميزات: {string.Join(" · ", industry.Features)}";
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ProfileCombo.SelectedItem is not IndustryTemplateSummary summary)
                throw new AccountingException("اختر نمط العمل.");

            using var facade = new DesktopAccountingFacade();
            var service = facade.GetService<OrganizationProvisioningService>();
            var request = new OrganizationProvisionRequest(
                CompanyNameBox.Text,
                VatNumberBox.Text,
                summary.BusinessProfile,
                _templates.DefaultCountryId,
                string.IsNullOrWhiteSpace(AdminUsernameBox.Text) ? null : AdminUsernameBox.Text,
                string.IsNullOrWhiteSpace(AdminPasswordBox.Password) ? null : AdminPasswordBox.Password,
                AdminUsernameBox.Text);

            Result = await service.ProvisionAsync(request);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex is AccountingException ae ? ae.Message : ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
