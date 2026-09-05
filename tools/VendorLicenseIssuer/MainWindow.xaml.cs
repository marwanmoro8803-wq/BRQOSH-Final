using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace Brqosh.VendorIssuer;

public partial class MainWindow : Window
{
    private RSA? _privateKey;
    private string? _keyFingerprint;
    private bool _selfTestPassed;
    private string? _issuedEnvelope;
    private LicensePayload? _issuedPayload;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += (_, _) => _privateKey?.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RunSelfTest();

        // ‏المسارُ المعتاد يُعرض ويُحمَّل إن وُجد — ولا يُفترض. مورِّدٌ يحفظ مفتاحَه في
        // ‏مكانٍ آخرَ (وهو الأصحّ) يجب أن يجد الأداةَ صامتةً لا مدّعيةً أنّها وجدت مفتاحاً.
        var fallback = LicenseCore.DefaultPrivateKeyPath;
        if (File.Exists(fallback))
            LoadPrivateKey(fallback);
        else
            SetKeyStatus("لم يُحمَّل مفتاحٌ بعد — اختر ملفَّ المفتاح الخاصّ.", warn: true);

        RefreshInputState();
    }

    // ── ① الاختبارُ الذاتيّ ──────────────────────────────────────────────────
    private sealed record Row(string Mark, Brush Brush, string Name, string Detail);

    private void RunSelfTest()
    {
        var rows = SelfTest.RunAll();
        _selfTestPassed = rows.All(r => r.Ok);
        var failed = rows.Count(r => !r.Ok);

        SelfTestList.ItemsSource = rows.Select(r => new Row(
            r.Ok ? "✓" : "✕",
            (Brush)FindResource(r.Ok ? "SuccessBrush" : "DangerBrush"),
            r.Name, r.Detail)).ToList();

        SelfTestBanner.Background = (Brush)FindResource(_selfTestPassed ? "SuccessBrush" : "DangerBrush");
        SelfTestSummary.Text = _selfTestPassed
            ? $"الاختبارُ الذاتيّ: {rows.Count} فحصاً ناجحاً"
            : $"الاختبارُ الذاتيّ أخفق في {failed} من {rows.Count} — الإصدارُ متوقّف";
        SelfTestHint.Text = _selfTestPassed
            ? "قواعدُ التحقّق هنا تطابق ما يطبّقه المنتج، والتوقيعُ يقع على البايتات المكتوبة نفسِها."
            : "لا تُصدر تراخيصَ من هذا البناء: نسخةُ قواعد التحقّق فارقت المنتج. افتح التفاصيل.";
        SelfTestExpander.IsExpanded = !_selfTestPassed;
    }

    // ── المفتاح ─────────────────────────────────────────────────────────────
    private void OnPickKey(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "اختر مفتاحَ المورِّد الخاصّ (PEM)",
            Filter = "مفاتيح PEM (*.pem)|*.pem|كلّ الملفات (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(LicenseCore.DefaultPrivateKeyPath),
        };
        if (dialog.ShowDialog() == true)
            LoadPrivateKey(dialog.FileName);
    }

    private void LoadPrivateKey(string path)
    {
        _privateKey?.Dispose();
        _privateKey = null;
        _keyFingerprint = null;
        KeyPathBox.Text = path;

        string pem;
        try
        {
            pem = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetKeyStatus($"تعذّرت قراءةُ الملفّ: {ex.Message}", warn: true);
            RefreshInputState();
            return;
        }

        RSA rsa;
        try
        {
            rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            _ = rsa.ExportParameters(includePrivateParameters: true);
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            SetKeyStatus("هذا ليس مفتاحاً خاصّاً صالحاً بصيغة PEM — تأكّد أنّك اخترت license-private.pem لا العامّ.", warn: true);
            RefreshInputState();
            return;
        }

        var fingerprint = LicenseCore.Fingerprint(rsa);

        // ‏**البوّابةُ التي تمنع كارثةً صامتة:** التوقيعُ بمفتاحٍ لا يقابل المُضمَّنَ في المنتج
        // ‏يُنتج تراخيصَ تُرفض على كلّ جهاز — والرسالةُ هناك تلوم الترخيصَ لا المفتاح.
        if (!string.Equals(fingerprint, TrustAnchor.ExpectedFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            rsa.Dispose();
            SetKeyStatus(
                "هذا المفتاحُ لا يقابل المفتاحَ المُضمَّن في المنتج المشحون — كلُّ ترخيصٍ يصدر به سيُرفض على أجهزة العملاء.\n"
                + $"بصمةُ المفتاح المُحمَّل : {fingerprint}\n"
                + $"البصمةُ التي يثق بها المنتج: {TrustAnchor.ExpectedFingerprint}",
                warn: true);
            RefreshInputState();
            return;
        }

        _privateKey = rsa;
        _keyFingerprint = fingerprint;
        SetKeyStatus($"المفتاحُ يطابق ما يثق به المنتج · البصمة {fingerprint[..24]}…", warn: false);
        RefreshInputState();
    }

    private void SetKeyStatus(string message, bool warn)
    {
        KeyStatusText.Text = message;
        KeyStatusText.Foreground = (Brush)FindResource(warn ? "DangerBrush" : "SuccessBrush");
    }

    // ── المدخلات ────────────────────────────────────────────────────────────
    private void OnInputChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshInputState();

    private void RefreshInputState()
    {
        if (CompanyBox is null || MachineBox is null || DaysBox is null || IssueButton is null)
            return;

        var company = CompanyBox.Text;
        var hidden = LicenseCore.DescribeHiddenCharacters(company);
        CompanyWarning.Text = hidden is null ? "" : $"محارفُ خفيّةٌ في الاسم: {hidden} — المقارنةُ عند العميل حرفيّة.";
        CompanyWarning.Visibility = hidden is null ? Visibility.Collapsed : Visibility.Visible;

        var machine = MachineBox.Text.Trim();
        var machineOk = LicenseCore.LooksLikeMachineId(machine);
        MachineWarning.Text = machine.Length == 0 || machineOk
            ? ""
            : "معرّفُ الجهاز يُتوقَّع أن يكون ١٦ محرفاً ستّ عشريّاً كما يظهر في شاشة التفعيل.";
        MachineWarning.Visibility = MachineWarning.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        var daysOk = int.TryParse(DaysBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var days)
                     && days is > 0 and <= 3650;
        ExpiryPreview.Text = daysOk
            ? $"ينتهي في {LicenseCore.FormatUtc(DateTime.UtcNow.AddDays(days))}"
            : "المدّة يجب أن تكون رقماً بين ١ و٣٦٥٠ يوماً.";

        IssueButton.IsEnabled = _selfTestPassed
                                && _privateKey is not null
                                && company.Trim().Length > 0
                                && machineOk
                                && daysOk;
    }

    // ── ③ الإصدار ───────────────────────────────────────────────────────────
    private void OnIssue(object sender, RoutedEventArgs e)
    {
        if (_privateKey is null)
            return;

        var company = CompanyBox.Text.Trim();
        var machine = MachineBox.Text.Trim();
        var days = int.Parse(DaysBox.Text.Trim(), CultureInfo.InvariantCulture);
        var tier = (TierBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "standard";

        var payload = new LicensePayload(company, machine, tier,
            DateTime.UtcNow.AddDays(days), ["core", "zatca", "construction"]);

        var envelope = LicenseCore.BuildSignedEnvelope(payload, _privateKey, out _);

        // ‏**لا يُسلَّم ترخيصٌ لم يُتحقَّق منه هنا.** والتحقّقُ بمرساة المنتج المُضمَّنة
        // ‏(‏TrustAnchor.PublicKeyPem‎) لا بالنصف العامّ للمفتاح المُحمَّل — وإلا كان الفحصُ
        // ‏يسأل المفتاحَ عن نفسِه ويقبل أيَّ مفتاح.
        var verdict = LicenseCore.Validate(company, machine, envelope, TrustAnchor.PublicKeyPem, out var verified);
        if (verdict != LicenseVerdict.Valid || verified is null)
        {
            _issuedEnvelope = null;
            _issuedPayload = null;
            ResultBox.Visibility = Visibility.Collapsed;
            ResultActions.Visibility = Visibility.Collapsed;
            IssueStatus.Foreground = (Brush)FindResource("DangerBrush");
            IssueStatus.Text = $"رُفض الترخيصُ في التحقّق الذاتيّ ({verdict}) — لم يُسلَّم شيء.";
            return;
        }

        _issuedEnvelope = envelope;
        _issuedPayload = verified;

        var ledgerError = IssueLedger.TryAppend(verified, envelope, _keyFingerprint ?? "?");

        ResultBox.Text = envelope;
        ResultBox.Visibility = Visibility.Visible;
        ResultActions.Visibility = Visibility.Visible;
        IssueStatus.Foreground = (Brush)FindResource("SuccessBrush");
        IssueStatus.Text = $"صدر الترخيصُ وتُحقّق منه بمرساة المنتج · ينتهي {LicenseCore.FormatUtc(verified.ExpiresUtc)}"
                           + (ledgerError is null ? "" : $"\nتعذّر تسجيلُه في سجلّ الإصدار: {ledgerError}");
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (_issuedEnvelope is null)
            return;
        Clipboard.SetText(_issuedEnvelope);
        IssueStatus.Text = "نُسخ المفتاحُ إلى الحافظة — الصقه في حقل «مفتاح الترخيص» عند العميل.";
        IssueStatus.Foreground = (Brush)FindResource("SuccessBrush");
    }

    private void OnSaveEnvelope(object sender, RoutedEventArgs e)
    {
        if (_issuedEnvelope is null || _issuedPayload is null)
            return;
        SaveText($"brqosh-license-{_issuedPayload.MachineId}.json", _issuedEnvelope);
    }

    private void OnSaveStoreFile(object sender, RoutedEventArgs e)
    {
        if (_issuedEnvelope is null || _issuedPayload is null)
            return;

        // ‏ملفٌّ يُوضع مباشرةً في `%APPDATA%\BRQOSH\license.json` عند العميل — طريقٌ لا يمرّ
        // ‏بلصق نصٍّ طويلٍ في حقلٍ واحد، وهو الأعملُ حين يكون العميلُ على الهاتف.
        SaveText("license.json",
            LicenseCore.BuildLicenseStoreFile(_issuedPayload.CompanyName, _issuedPayload.MachineId, _issuedEnvelope));
    }

    private void SaveText(string suggestedName, string content)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = suggestedName,
            Filter = "ملفات JSON (*.json)|*.json|كلّ الملفات (*.*)|*.*",
        };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            // ‏بلا BOM: علامةُ الترتيب في أوّل الملفّ ليست فراغاً في .NET، فلا يقتطعها
            // ‏`Trim()` في شاشة التفعيل — ويُرفض الملفُّ بحكم «ليس ملفَّ ترخيص».
            File.WriteAllText(dialog.FileName, content, new UTF8Encoding(false));
            IssueStatus.Foreground = (Brush)FindResource("SuccessBrush");
            IssueStatus.Text = $"حُفظ في {dialog.FileName}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            IssueStatus.Foreground = (Brush)FindResource("DangerBrush");
            IssueStatus.Text = $"تعذّر الحفظ: {ex.Message}";
        }
    }
}
