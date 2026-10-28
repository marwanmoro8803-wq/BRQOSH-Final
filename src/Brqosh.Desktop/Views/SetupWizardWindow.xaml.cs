using System.Windows;
using Brqosh.Desktop.Ui;
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
        // (CEEF-232 · و-١) ‏**والإرشادُ يُشتقّ من السياسة لا يُكتب بجانبها.**
        //
        // ‏كان مكتوباً «٨ أحرف على الأقل» في شاشتين، **و`PasswordPolicy.MinLength` اثنتا عشرة**.
        // ‏**فيُرفض المستخدمُ بشرطٍ لم يُقَل له**: يكتب ثمانيةً كما أُمر فتردّه الشاشة —
        // ‏وأخرجه تشغيلٌ حيٌّ للمعالج لا قراءةُ شيفرة، **إذ كلا النصّين صحيحٌ نحوياً**.
        // ‏وشاشةُ تغيير كلمة المرور **يمرّ بها كلُّ مستخدمٍ في أوّل دخول**.
        PolicyHintText.Text = LocaleManager.T(
            $"{PasswordPolicy.MinLength} حرفاً على الأقل، مع حرف كبير وصغير ورقم ورمز خاص.",
            $"At least {PasswordPolicy.MinLength} characters, with upper and lower case, a digit and a symbol.");
        Loaded += OnLoaded;
        ProfileCombo.SelectionChanged += (_, _) => UpdateTemplatePreview();
        // (CEEF-236 · ت-٣) خطوةُ قاعدة البيانات — لوحةٌ مشتركةٌ مع نافذة الإنقاذ.
        ConnectionPanel.Initialize(AppEnvironmentConfig.IsProduction);
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

    private async void OnStartClicked(object sender, RoutedEventArgs e)
    {
        HideError();

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

        // (CEEF-236 · ت-٣) ‏الاتصالُ يُجرَّب قبل أن يُكتب — فلا يُحفظ ملفٌّ يُقلع التطبيقُ عليه إلى خطأ.
        StartButton.IsEnabled = false;
        string? connectionError;
        // ‏(1.0.32) إعدادُ القاعدة المدمجة أوّلَ مرّة يستغرق عشرات الثواني (تهيئةُ مجلّد البيانات وتشغيلُ الخادم) — صفحةُ الانتظار بالشعار بدل نافذةٍ جامدة.
        StartupSplashWindow? waiting = null;
        if (ConnectionPanel.UseEmbedded && !Brqosh.Infrastructure.Persistence.EmbeddedPostgres.IsProvisioned)
        {
            waiting = new StartupSplashWindow { Owner = this };
            waiting.SetStatus("جارٍ إعداد خادم قاعدة البيانات على هذا الجهاز…", "Setting up the database server on this computer…");
            waiting.Show();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        }

        try
        {
            connectionError = await ConnectionPanel.ValidateProbeAndSaveAsync();
        }
        finally
        {
            waiting?.Close();
            StartButton.IsEnabled = true;
        }

        if (connectionError is not null)
        {
            ShowError(connectionError);
            return;
        }

        AdminUsername = username;
        AdminPassword = password;

        // ‏(م-٥ · ن-٥٠) **الاسمُ الذي قُبل به الترخيصُ يُحمَل ولا يُسقَط.**
        //
        // ‏كان يُبنى `SetupState` بلا `LicensedCompanyName`، فيأخذ الوسيطُ الاختياريُّ
        // ‏`null`. ثمّ يقرأ `App` ‏`LicensedCompanyName ?? CompanyName` — **فيُقاس الترخيصُ
        // ‏على ما يكتبه المُشغّلُ الآن لا على ما قُبل به**. وهو بابُ ن-٣٥ نفسِه: من أعاد
        // ‏المعالجَ على تركيبٍ قائمٍ وجد الإنتاجَ مقفلاً «لمنشأةٍ أخرى» — والمنشأةُ منشأتُه.
        //
        // ‏والمعالجُ يعمل في حالتين: تركيبٌ بكرٌ (فلا شيءَ يُحمَل)، أو إعدادٌ لم يكتمل
        // ‏(فيُحمَل ما كان). **والقراءةُ هنا آمنة**: لو كان الملفُّ غيرَ مقروءٍ لما بلغ
        // ‏التنفيذُ هذه النافذةَ أصلاً — `Load` يرفض قبلها.
        var carried = _store.Load()?.LicensedCompanyName;

        _store.Save(new SetupState(
            IsCompleted: true,
            Profile: summary.BusinessProfile,
            CompanyName: companyName,
            VatRegistrationNumber: vatNumber,
            CompletedAtUtc: DateTime.UtcNow,
            CountryTemplateId: _templates.DefaultCountryId,
            LicensedCompanyName: carried));

        DialogResult = true;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorText.Visibility = Visibility.Collapsed;
    }
}
