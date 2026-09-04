using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Persistence;
using Npgsql;

namespace Brqosh.Desktop.Views.Controls;

/// <summary>
/// (CEEF-236 · ت-٣ · م-١١) <b>لوحةُ الاتصال بقاعدة البيانات — نسخةٌ واحدةٌ في مسارَين.</b>
///
/// <para>يستعملها <see cref="SetupWizardWindow"/> في أوّل تهيئة، و<see cref="DatabaseConnectionWindow"/>
/// حين يعجز الإقلاعُ عن قراءة الملفّ. <b>ولولا الاشتراك لَتكرّر منطقُ التحقّق والبناء والحفظ في
/// موضعَين</b> — فيُصلَح أحدُهما ويُنسى الآخر، وهو نمطُ العطل الذي تلاحقه هذه الحملة أصلاً.</para>
/// </summary>
public partial class DatabaseConnectionPanel : UserControl
{
    private DatabaseConfigEntry? _existingPostgres;
    private bool _isProduction;

    public DatabaseConnectionPanel()
    {
        InitializeComponent();
    }

    /// <summary>هل يلزم PostgreSQL — إنتاجاً أو باختيار المستخدم.</summary>
    public bool RequiresPostgres => _isProduction || UseSqliteBox.IsChecked != true;

    /// <summary>
    /// يُهيّئ اللوحة: يخفي خيارَ SQLite في الإنتاج، ويملأ الحقولَ من ملفٍّ قائمٍ إن وُجد.
    /// وملفٌّ لا يُفكّ يُعرض سببُه هنا ولا يُسقط الشاشة — <b>فهذه الشاشةُ نفسُها هي علاجُه</b>.
    /// </summary>
    public void Initialize(bool isProduction)
    {
        _isProduction = isProduction;
        UseSqliteBox.Visibility = isProduction ? Visibility.Collapsed : Visibility.Visible;

        DatabaseConfigEntry? existing = null;
        try
        {
            existing = DatabaseConfigFile.Load();
        }
        catch (InvalidOperationException ex)
        {
            LoadErrorText.Text = ex.Message;
            LoadErrorText.Visibility = Visibility.Visible;
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
            RequireTlsBox.IsChecked = DatabaseConfigFile.ReadSslMode(existing.ConnectionString)
                is not (SslMode.Disable or SslMode.Allow or SslMode.Prefer);
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

    /// <summary>
    /// (CEEF-236 · م-٩) <b>نتيجةٌ لا تصف حالةً قد تغيّرت.</b>
    ///
    /// <para>كان نصُّ الحالة يُكتب عند النجاح والفشل ولا يُمحى أبداً: فتُعرض «الاتصال ناجح» خضراءَ
    /// بجوار خطأٍ أحمرَ لحقلٍ فُرّغ للتوّ، وتصف اعتماداً لم يعد في الشاشة.</para>
    /// </summary>
    private void OnFieldChanged(object sender, RoutedEventArgs e) => ClearStatus();

    public void ClearStatus()
    {
        if (ConnectionStatusText is null)
            return;
        ConnectionStatusText.Text = "";
    }

    /// <summary>
    /// يبني سلسلةَ الاتصال من الحقول، أو يُعيد الملفَّ القائمَ إن تُركت كلمةُ المرور فارغةً ولم
    /// يتغيّر شيء. <c>null</c> مع رسالةٍ عند نقصٍ في المدخلات.
    /// </summary>
    public string? TryBuildConnectionString(out string? error)
    {
        error = null;

        var host = PgHostBox.Text.Trim();
        if (host.Length == 0)
        {
            error = LocaleManager.T("اسم خادم قاعدة البيانات مطلوب.", "The database server host is required.");
            return null;
        }

        if (!int.TryParse(PgPortBox.Text.Trim(), out var port) || port is < 1 or > 65535)
        {
            error = LocaleManager.T("المنفذ يجب أن يكون رقماً بين 1 و65535.", "The port must be a number between 1 and 65535.");
            return null;
        }

        var database = PgDatabaseBox.Text.Trim();
        if (database.Length == 0)
        {
            error = LocaleManager.T("اسم القاعدة مطلوب.", "The database name is required.");
            return null;
        }

        var username = PgUserBox.Text.Trim();
        if (username.Length == 0)
        {
            error = LocaleManager.T("مستخدم القاعدة مطلوب.", "The database user is required.");
            return null;
        }

        // ‏وضعُ التشفير: `Require` عند الاشتراط — وتُحفظ `VerifyCA`/`VerifyFull` إن كان الملفُّ القائم
        // ‏أشدّ، فلا يُخفَّض تشدّدٌ كتبه مسؤولُ النظام بيده. (‏`VerifyFull` يحتاج شهادةَ جذرٍ لا تملكها
        // ‏هذه الشاشة — فتُحفَظ من السلسلة القائمة ولا تُبنى هنا ابتداءً.)
        var existingSslMode = _existingPostgres is null
            ? (SslMode?)null
            : DatabaseConfigFile.ReadSslMode(_existingPostgres.ConnectionString);
        var sslMode = RequireTlsBox.IsChecked == true
            ? (existingSslMode is SslMode.VerifyCA or SslMode.VerifyFull ? existingSslMode.Value : SslMode.Require)
            : SslMode.Prefer;

        var password = PgPasswordBox.Password;
        if (password.Length == 0)
        {
            if (_existingPostgres is not null
                && DatabaseConfigFile.TryParsePostgres(_existingPostgres.ConnectionString,
                    out var oldHost, out var oldPort, out var oldDb, out var oldUser)
                && string.Equals(oldHost, host, StringComparison.OrdinalIgnoreCase)
                && oldPort == port
                && string.Equals(oldDb, database, StringComparison.Ordinal)
                // ‏أسماءُ أدوار PostgreSQL حسّاسةٌ لحالة الأحرف — والمقارنةُ المتساهلة تُعيد استعمالَ
                // ‏سلسلةٍ باسمٍ غيرِ الذي كُتب في الحقل.
                && string.Equals(oldUser, username, StringComparison.Ordinal)
                && SameStrictness(existingSslMode, sslMode))
            {
                return _existingPostgres.ConnectionString;
            }

            error = LocaleManager.T("كلمة مرور قاعدة البيانات مطلوبة.", "The database password is required.");
            return null;
        }

        return DatabaseConfigFile.BuildPostgresConnectionString(host, port, database, username, password, sslMode);
    }

    /// <summary>
    /// (CEEF-236 · م-١٢) هل يُبقي الاشتراطُ المطلوبُ الملفَّ القائمَ صالحاً كما هو؟
    ///
    /// <para>المقارنةُ على <b>التشدّد</b> لا على تطابق القيمة: ملفٌّ بـ<c>Disable</c> أو <c>Allow</c>
    /// والخانةُ غيرُ مؤشَّرة (‏<c>Prefer</c>‎) كان يُرفض ويُطلب كلمةُ مرورٍ لم تتغيّر — رسالةٌ تُشير
    /// إلى الحقل الخطأ. وهما أضعفُ من <c>Prefer</c> فلا يُعاد استعمالُهما إلا حين لا يُشترط تشفير.</para>
    /// </summary>
    private static bool SameStrictness(SslMode? existing, SslMode requested)
    {
        if (existing is null) return false;
        var existingRequiresTls = existing is not (SslMode.Disable or SslMode.Allow or SslMode.Prefer);
        var requestedRequiresTls = requested is not (SslMode.Disable or SslMode.Allow or SslMode.Prefer);
        return existingRequiresTls == requestedRequiresTls;
    }

    /// <summary>يُجرّب الاتصالَ ويعرض النتيجة — ولا يحفظ.</summary>
    private async void OnTestConnection(object sender, RoutedEventArgs e)
    {
        ClearStatus();
        var connectionString = TryBuildConnectionString(out var error);
        if (connectionString is null)
        {
            ShowStatus(error ?? "", ok: false);
            return;
        }

        TestConnectionBtn.IsEnabled = false;
        ShowStatus(LocaleManager.T("جارٍ الاتصال…", "Connecting…"), ok: null);
        try
        {
            var probe = await DatabaseConfigFile.TestPostgresAsync(connectionString);
            ShowStatus(probe.Message, probe.Ok);
        }
        finally
        {
            TestConnectionBtn.IsEnabled = true;
        }
    }

    /// <summary>
    /// يتحقّق ويُجرّب ويحفظ. يُعيد رسالةَ الخطأ عند الإخفاق، أو <c>null</c> عند النجاح.
    /// <b>ولا يُحفظ ملفٌّ يُقلع التطبيقُ عليه إلى خطأ</b> — الاتصالُ يُجرَّب قبل الكتابة.
    /// </summary>
    public async Task<string?> ValidateProbeAndSaveAsync()
    {
        if (!RequiresPostgres)
        {
            // ‏اختار قاعدةً محلية وثمّة ملفٌّ يُشير إلى PostgreSQL — يُكتب المزوّدُ صريحاً كي لا يُقلع
            // ‏التطبيقُ على قاعدةٍ غير التي اختارها للتوّ.
            return _existingPostgres is null
                ? null
                : SaveOrDescribe(DatabaseConfigFile.SqliteProvider,
                    $"Data Source={BrqoshDbContextFactory.DefaultSqlitePath()}");
        }

        var connectionString = TryBuildConnectionString(out var error);
        if (connectionString is null)
        {
            ShowStatus(error ?? "", ok: false);
            return error;
        }

        ShowStatus(LocaleManager.T("جارٍ الاتصال…", "Connecting…"), ok: null);
        var probe = await DatabaseConfigFile.TestPostgresAsync(connectionString);
        ShowStatus(probe.Message, probe.Ok);
        if (!probe.Ok)
            return probe.Message;

        return SaveOrDescribe(DatabaseConfigFile.PostgresProvider, connectionString);
    }

    /// <summary>
    /// (CEEF-236 · م-١٠) الحفظُ لا يُسقط الشاشة. <c>Save</c> يكتب على القرص <b>ويُشفّر بـDPAPI</b> —
    /// فإلى جانب أخطاء الملفّات يجوز أن ترمي <c>CryptographicException</c>، وكان المُرشِّحُ لا يلتقطها
    /// فتخرج من معالجٍ <c>async void</c> إلى نافذةِ عطلٍ بمكدّس.
    /// </summary>
    private string? SaveOrDescribe(string provider, string connectionString)
    {
        try
        {
            DatabaseConfigFile.Save(provider, connectionString);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            var message = LocaleManager.T("تعذّر حفظ ملف الاتصال: ", "Could not save the connection file: ") + ex.Message;
            ShowStatus(message, ok: false);
            return message;
        }
    }

    private void ShowStatus(string message, bool? ok)
    {
        ConnectionStatusText.Text = message;
        ConnectionStatusText.Foreground = (Brush)FindResource(
            ok switch { true => "SuccessBrush", false => "DangerBrush", null => "TextMutedBrush" });
    }
}
