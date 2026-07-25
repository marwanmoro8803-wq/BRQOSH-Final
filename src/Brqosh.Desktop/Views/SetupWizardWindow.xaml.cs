using System.Windows;
using Brqosh.Application.Setup;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Setup;

namespace Brqosh.Desktop.Views;

public partial class SetupWizardWindow : Window
{
    private readonly ISetupStateStore _store;
    private readonly IConfigurationTemplateLoader _templates = new ConfigurationTemplateLoader();

    public string? AdminUsername { get; private set; }
    public string? AdminPassword { get; private set; }

    public SetupWizardWindow(ISetupStateStore store)
    {
        _store = store;
        InitializeComponent();
        Loaded += OnLoaded;
        ProfileCombo.SelectionChanged += (_, _) => UpdateTemplatePreview();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ProfileCombo.ItemsSource = _templates.ListIndustries();
        ProfileCombo.SelectedIndex = 0;

        var country = _templates.LoadCountry(_templates.DefaultCountryId);
        CountryTemplateTitle.Text = $"{country.NameAr} ({country.NameEn})";
        CountryTemplateDetails.Text =
            $"عملة: {country.Currency.Base} · ضريبة: {country.Vat.StandardRate:P0} · امتثال: {string.Join(" · ", country.Compliance)}";
        UpdateTemplatePreview();
    }

    private void UpdateTemplatePreview()
    {
        if (ProfileCombo.SelectedItem is not IndustryTemplateSummary summary)
        {
            TemplatePreviewPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var industry = _templates.LoadIndustry(summary.BusinessProfile);
        IndustryTemplateTitle.Text = industry.NameAr;
        TemplateModulesText.Text = string.Join(" · ", industry.Modules);
        TemplateFeaturesText.Text = string.Join(" · ", industry.Features);
        TemplatePreviewPanel.Visibility = Visibility.Visible;
    }

    private void OnStartClicked(object sender, RoutedEventArgs e)
    {
        if (ProfileCombo.SelectedItem is not IndustryTemplateSummary summary)
        {
            ShowError("اختر نظام العمل أولاً.");
            return;
        }

        var companyName = CompanyNameBox.Text.Trim();
        if (companyName.Length < 2)
        {
            ShowError("اسم المنشأة مطلوب.");
            return;
        }

        var vatNumber = VatNumberBox.Text.Trim();
        if (vatNumber.Length > 0 && (vatNumber.Length != 15 || !vatNumber.All(char.IsDigit)))
        {
            ShowError("الرقم الضريبي يجب أن يتكون من 15 رقماً (أو اتركه فارغاً الآن).");
            return;
        }

        var username = AdminUsernameBox.Text.Trim().ToLowerInvariant();
        if (username.Length < 3)
        {
            ShowError("اسم مستخدم المدير مطلوب (3 أحرف على الأقل).");
            return;
        }

        var password = AdminPasswordBox.Password;
        var confirm = AdminPasswordConfirmBox.Password;
        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            ShowError("تأكيد كلمة المرور غير متطابق.");
            return;
        }

        if (!PasswordPolicy.IsValid(password, out var pwdError))
        {
            ShowError(pwdError ?? "كلمة المرور غير صالحة.");
            return;
        }

        AdminUsername = username;
        AdminPassword = password;

        _store.Save(new SetupState(
            IsCompleted: true,
            Profile: summary.BusinessProfile,
            CompanyName: companyName,
            VatRegistrationNumber: vatNumber,
            CompletedAtUtc: DateTime.UtcNow,
            CountryTemplateId: _templates.DefaultCountryId));

        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
