using System.IO;
using System.Windows;
using System.Windows.Media;
using Brqosh.Desktop.Ui;
using Brqosh.Application.Setup;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Setup;

namespace Brqosh.Desktop.Views;

public partial class SetupWizardWindow : Window
{
    private readonly ISetupStateStore _store;
    private readonly IConfigurationTemplateLoader _templates = new ConfigurationTemplateLoader();

    /// <summary>
    /// (CEEF-236 · ت-٣) ملفُّ اتصالٍ قائمٌ يُشير إلى PostgreSQL — تُملأ حقولُه ولا تُعرض كلمةُ
    /// مروره؛ وتركُ حقلِ كلمة المرور فارغاً يعني الإبقاءَ عليه كما هو.
    /// </summary>
    private DatabaseConfigEntry? _existingPostgres;

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
        InitializeDatabaseSection();
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

    // ───────────────────────── ④ قاعدة البيانات (CEEF-236 · ت-٣) ─────────────────────────

    /// <summary>
    /// <b>ما كان:</b> في الإنتاج يرمي <c>AppServices.LoadDatabaseConfig</c> إن غاب
    /// <c>database.json</c>، <b>ولا شاشةٌ تكتبه</b> — فأوّلُ إقلاعٍ عند العميل كان يشترط
    /// تأليفَ JSON باليد بكلمة مرور PostgreSQL صريحة. <b>وما صار:</b> تُدخَل البياناتُ هنا،
    /// تُجرَّب، ثمّ تُكتب محميّةً. وخيارُ SQLite يظهر خارجَ الإنتاج وحدَه ويبقى الافتراضَ هناك
    /// كي لا يتعطّل مسحٌ آليٌّ يقود المعالجَ على جذرٍ فارغ.
    /// </summary>
    private void InitializeDatabaseSection()
    {
        var isProduction = AppEnvironmentConfig.IsProduction;
        UseSqliteBox.Visibility = isProduction ? Visibility.Collapsed : Visibility.Visible;

        DatabaseConfigEntry? existing = null;
        try
        {
            existing = DatabaseConfigFile.Load();
        }
        catch (InvalidOperationException ex)
        {
            // ‏ملفٌّ محميٌّ بحساب مستخدمٍ آخر — يُقال ذلك ويُعاد الإدخال، لا يُسقَط المعالج.
            ExistingConfigHint.Text = ex.Message;
            ExistingConfigHint.Visibility = Visibility.Visible;
        }

        if (existing is not null
            && existing.Provider.Equals(DatabaseConfigFile.PostgresProvider, StringComparison.OrdinalIgnoreCase)
            && DatabaseConfigFile.TryParsePostgres(existing.ConnectionString,
                out var host, out var port, out var database, out var username))
        {
            _existingPostgres = existing;
            PgHostBox.Text = host;
            PgPortBox.Text = port.ToString();
            PgDatabaseBox.Text = database;
            PgUserBox.Text = username;
            // ‏اشتراطُ التشفير يُقرأ من الملفّ لا يُفترض — وإلا خُفِّض ملفٌّ مُشدَّدٌ بصمتٍ عند أوّل تحرير.
            RequireTlsBox.IsChecked =
                DatabaseConfigFile.ReadSslMode(existing.ConnectionString) is not Npgsql.SslMode.Disable and not Npgsql.SslMode.Allow
                and not Npgsql.SslMode.Prefer;
            ExistingConfigHint.Text = LocaleManager.T(
                "وُجد ملفُّ اتصالٍ قائم — اترك كلمة المرور فارغةً للإبقاء عليه كما هو.",
                "An existing connection file was found — leave the password empty to keep it as it is.");
            ExistingConfigHint.Visibility = Visibility.Visible;
            UseSqliteBox.IsChecked = false;
        }
        else
        {
            UseSqliteBox.IsChecked = !isProduction;
        }

        ApplyDatabaseMode();
    }

    private void OnDatabaseModeChanged(object sender, RoutedEventArgs e) => ApplyDatabaseMode();

    private void ApplyDatabaseMode()
    {
        if (PostgresPanel is null || UseSqliteBox is null)
            return;
        PostgresPanel.Visibility = UseSqliteBox.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
    }

    private bool RequiresPostgres => AppEnvironmentConfig.IsProduction || UseSqliteBox.IsChecked != true;

    /// <summary>يبني سلسلةَ الاتصال من الحقول — أو يُعيد الملفَّ القائمَ إن تُركت كلمةُ المرور فارغةً والحقولُ لم تتغيّر.</summary>
    private string? BuildConnectionStringOrShowError()
    {
        var host = PgHostBox.Text.Trim();
        if (host.Length == 0)
        {
            ShowError(LocaleManager.T("اسم خادم قاعدة البيانات مطلوب.", "The database server host is required."));
            return null;
        }

        if (!int.TryParse(PgPortBox.Text.Trim(), out var port) || port is < 1 or > 65535)
        {
            ShowError(LocaleManager.T("المنفذ يجب أن يكون رقماً بين 1 و65535.", "The port must be a number between 1 and 65535."));
            return null;
        }

        var database = PgDatabaseBox.Text.Trim();
        if (database.Length == 0)
        {
            ShowError(LocaleManager.T("اسم القاعدة مطلوب.", "The database name is required."));
            return null;
        }

        var username = PgUserBox.Text.Trim();
        if (username.Length == 0)
        {
            ShowError(LocaleManager.T("مستخدم القاعدة مطلوب.", "The database user is required."));
            return null;
        }

        // ‏وضعُ التشفير: المُعلَنُ في الملفّ القائم إن لم يُغيّره المستخدم، وإلا ما اختاره في الشاشة.
        // ‏(‏`Require` لا `VerifyFull`: الثاني يحتاج شهادةَ جذرٍ للمنشأة، وهو قرارٌ لا افتراض.)
        var requireTls = RequireTlsBox.IsChecked == true;
        var existingSslMode = _existingPostgres is null
            ? (Npgsql.SslMode?)null
            : DatabaseConfigFile.ReadSslMode(_existingPostgres.ConnectionString);
        var sslMode = requireTls
            ? (existingSslMode is Npgsql.SslMode.VerifyCA or Npgsql.SslMode.VerifyFull
                ? existingSslMode.Value
                : Npgsql.SslMode.Require)
            : Npgsql.SslMode.Prefer;

        var password = PgPasswordBox.Password;
        if (password.Length == 0)
        {
            if (_existingPostgres is not null
                && DatabaseConfigFile.TryParsePostgres(_existingPostgres.ConnectionString,
                    out var oldHost, out var oldPort, out var oldDb, out var oldUser)
                && string.Equals(oldHost, host, StringComparison.OrdinalIgnoreCase)
                && oldPort == port
                && string.Equals(oldDb, database, StringComparison.Ordinal)
                // ‏أسماءُ مستخدمي PostgreSQL حسّاسةٌ لحالة الأحرف — والمقارنةُ المتساهلة تُعيد
                // ‏استعمالَ سلسلةٍ باسمٍ غيرِ الذي كُتب في الحقل.
                && string.Equals(oldUser, username, StringComparison.Ordinal)
                && existingSslMode == sslMode)
            {
                return _existingPostgres.ConnectionString;
            }

            ShowError(LocaleManager.T("كلمة مرور قاعدة البيانات مطلوبة.", "The database password is required."));
            return null;
        }

        return DatabaseConfigFile.BuildPostgresConnectionString(host, port, database, username, password, sslMode);
    }

    private async void OnTestConnection(object sender, RoutedEventArgs e)
    {
        HideError();
        var connectionString = BuildConnectionStringOrShowError();
        if (connectionString is null)
            return;

        TestConnectionBtn.IsEnabled = false;
        ConnectionStatusText.Foreground = (Brush)FindResource("TextMutedBrush");
        ConnectionStatusText.Text = LocaleManager.T("جارٍ الاتصال…", "Connecting…");
        try
        {
            ShowConnectionStatus(await DatabaseConfigFile.TestPostgresAsync(connectionString));
        }
        finally
        {
            TestConnectionBtn.IsEnabled = true;
        }
    }

    private void ShowConnectionStatus(DatabaseConfigFile.ConnectionProbe probe)
    {
        ConnectionStatusText.Text = probe.Message;
        ConnectionStatusText.Foreground = (Brush)FindResource(probe.Ok ? "SuccessBrush" : "DangerBrush");
    }

    // ─────────────────────────────────────────────────────────────────────────────────────

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
        if (RequiresPostgres)
        {
            var connectionString = BuildConnectionStringOrShowError();
            if (connectionString is null)
                return;

            StartButton.IsEnabled = false;
            DatabaseConfigFile.ConnectionProbe probe;
            try
            {
                probe = await DatabaseConfigFile.TestPostgresAsync(connectionString);
            }
            finally
            {
                StartButton.IsEnabled = true;
            }

            ShowConnectionStatus(probe);
            if (!probe.Ok)
            {
                ShowError(probe.Message);
                return;
            }

            try
            {
                DatabaseConfigFile.Save(DatabaseConfigFile.PostgresProvider, connectionString);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowError(LocaleManager.T("تعذّر حفظ ملف الاتصال: ", "Could not save the connection file: ") + ex.Message);
                return;
            }
        }
        else if (_existingPostgres is not null)
        {
            // ‏اختار قاعدةً محلية وثمّة ملفٌّ يُشير إلى PostgreSQL — يُكتب المزوّدُ صريحاً كي لا
            // ‏يُقلع التطبيقُ على قاعدةٍ غير التي اختارها للتوّ.
            DatabaseConfigFile.Save(DatabaseConfigFile.SqliteProvider,
                $"Data Source={BrqoshDbContextFactory.DefaultSqlitePath()}");
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

    private void HideError()
    {
        ErrorText.Visibility = Visibility.Collapsed;
    }
}
