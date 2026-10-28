using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Brqosh.VendorIssuer;

public partial class MainWindow : Window
{
    private bool _ready;
    private bool _selfTestPassed;

    private string? _issuedCode;
    private string _issuedCompany = "";
    private string _issuedMachine = "";
    private LicensePlan _issuedPlan;
    private DateOnly _issuedOn;
    private DateTime _issuedExpiry;
    private string _issuedNotes = "";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RunSelfTest();

        IssueDatePicker.SelectedDate = DateTime.Today;
        IssueDatePicker.DisplayDateEnd = DateTime.Today;
        IssueDatePicker.DisplayDateStart = new DateTime(2025, 1, 1);

        _ready = true;
        LoadLedger();
        RefreshInputState();
    }

    // ── الفحصُ الذاتيّ ─────────────────────────────────────────────────────
    private sealed record SelfTestRowView(string Mark, Brush Brush, string Name, string Detail);

    private void RunSelfTest()
    {
        var rows = SelfTest.RunAll();
        _selfTestPassed = rows.All(r => r.Ok);
        var failed = rows.Count(r => !r.Ok);

        SelfTestList.ItemsSource = rows.Select(r => new SelfTestRowView(
            r.Ok ? "✓" : "✕",
            (Brush)FindResource(r.Ok ? "SuccessBrush" : "DangerBrush"),
            r.Name, r.Detail)).ToList();

        SelfTestBanner.Background = (Brush)FindResource(_selfTestPassed ? "SuccessBrush" : "DangerBrush");
        SelfTestSummary.Text = _selfTestPassed
            ? $"الفحص الذاتي: {rows.Count} فحصاً ناجحاً"
            : $"الفحص الذاتي أخفق في {failed} من {rows.Count} — الإصدار متوقف";
        SelfTestHint.Text = _selfTestPassed
            ? "ترميز الأداة يطابق ترميز البرنامج: المفتاح الذي يصدر من هنا يُقبل هناك."
            : "لا تُصدر تراخيص من هذا البناء: نسخة قواعد الترميز فارقت البرنامج. افتح التفاصيل.";
        SelfTestExpander.IsExpanded = !_selfTestPassed;
    }

    // ── المدخلات ───────────────────────────────────────────────────────────
    private void OnInputChanged(object sender, TextChangedEventArgs e) => RefreshInputState();

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshInputState();

    private void OnDateChanged(object? sender, SelectionChangedEventArgs e) => RefreshInputState();

    private LicensePlan SelectedPlan()
    {
        var tag = (PlanBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return tag switch
        {
            "Trial7" => LicensePlan.Trial7,
            "Lifetime" => LicensePlan.Lifetime,
            _ => LicensePlan.Annual,
        };
    }

    private DateOnly? SelectedIssueDate()
    {
        if (IssueDatePicker.SelectedDate is not DateTime date)
            return null;
        var day = DateOnly.FromDateTime(date);
        return day.DayNumber >= new DateOnly(2025, 1, 1).DayNumber
               && day.DayNumber <= DateOnly.FromDateTime(DateTime.Today).DayNumber
            ? day
            : null;
    }

    private void RefreshInputState()
    {
        if (!_ready || CompanyBox is null || MachineBox is null || PlanBox is null
            || IssueDatePicker is null || IssueButton is null)
            return;

        var company = CompanyBox.Text;
        var hidden = LicenseCodec.DescribeHiddenCharacters(company);
        CompanyWarning.Text = hidden is null ? "" : $"محارف خفية في الاسم: {hidden} — المقارنة عند العميل حرفية.";
        CompanyWarning.Visibility = hidden is null ? Visibility.Collapsed : Visibility.Visible;

        var machine = MachineBox.Text.Trim();
        var machineOk = LicenseCodec.LooksLikeMachineId(machine);
        MachineWarning.Text = machine.Length == 0 || machineOk
            ? ""
            : "معرّف الجهاز يُتوقع أن يكون ١٦ محرفاً ست عشرياً كما يظهر في شاشة التفعيل.";
        MachineWarning.Visibility = MachineWarning.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        var plan = SelectedPlan();
        var issuedOn = SelectedIssueDate();

        if (issuedOn is DateOnly day)
        {
            var expires = LicenseCodec.ExpiryFor(plan, day);
            ExpiryPreview.Text = expires == DateTime.MaxValue
                ? $"المدة: دائم — لا ينتهي · تاريخ الإصدار {day:yyyy-MM-dd}"
                : $"ينتهي في {LicenseCodec.FormatDate(expires)} (UTC) · المدة: {LicenseCodec.PlanLabelAr(plan)}";
        }
        else
        {
            ExpiryPreview.Text = "تاريخ الإصدار يجب أن يكون بين 2025-01-01 واليوم.";
        }

        IssueButton.IsEnabled = _selfTestPassed
                                && company.Trim().Length > 0
                                && machineOk
                                && issuedOn is not null;
    }

    // ── الإصدار ────────────────────────────────────────────────────────────
    private void OnIssue(object sender, RoutedEventArgs e)
    {
        if (!_selfTestPassed || SelectedIssueDate() is not DateOnly issuedOn)
            return;

        var company = CompanyBox.Text.Trim();
        var machine = MachineBox.Text.Trim();
        var plan = SelectedPlan();

        string code;
        try
        {
            code = LicenseCodec.Encode(plan, issuedOn, machine, company);
        }
        catch (ArgumentException ex)
        {
            Fail($"تعذّر الإصدار: {ex.Message}");
            return;
        }

        // **لا يُسلَّم مفتاحٌ لم يُتحقَّق منه هنا** — بالترميز نفسِه الذي يطبّقه البرنامج.
        var verdict = LicenseCodec.Decode(code, machine, company, DateTime.UtcNow,
            out var verifiedPlan, out var expires, out var verifiedIssuedOn);
        if (verdict != NumericLicenseVerdict.Valid || verifiedPlan != plan || verifiedIssuedOn != issuedOn)
        {
            Fail($"رُفض المفتاح في التحقق الذاتي ({verdict}) — لم يُسلَّم شيء.");
            return;
        }

        _issuedCode = code;
        _issuedCompany = company;
        _issuedMachine = machine;
        _issuedPlan = plan;
        _issuedOn = issuedOn;
        _issuedExpiry = expires;
        _issuedNotes = NotesBox.Text.Trim();

        CodeBox.Text = LicenseCodec.Format(code);
        ExpiryText.Text = expires == DateTime.MaxValue
            ? "ينتهي في: بلا انتهاء (دائم)"
            : $"ينتهي في: {LicenseCodec.FormatDate(expires)} (UTC)";
        DurationText.Text = $"النوع: {LicenseCodec.PlanLabelAr(plan)} · المنشأة: {company}";
        ResultPanel.Visibility = Visibility.Visible;

        var ledgerError = IssueLedger.TryAppend(new IssueLedger.LedgerRow(
            code, company, machine, plan,
            issuedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            expires == DateTime.MaxValue ? "never" : LicenseCodec.FormatDate(expires),
            DateTime.UtcNow));

        IssueStatus.Foreground = (Brush)FindResource("SuccessBrush");
        IssueStatus.Text = "صدر المفتاح وتُحقّق منه بالترميز نفسه."
                           + (ledgerError is null ? "" : $"\nتعذّر تسجيله في السجل: {ledgerError}");

        LoadLedger();
    }

    private void Fail(string message)
    {
        _issuedCode = null;
        ResultPanel.Visibility = Visibility.Collapsed;
        IssueStatus.Foreground = (Brush)FindResource("DangerBrush");
        IssueStatus.Text = message;
    }

    // ── النقل والحفظ ───────────────────────────────────────────────────────
    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (_issuedCode is null)
            return;
        Clipboard.SetText(LicenseCodec.Format(_issuedCode));
        IssueStatus.Foreground = (Brush)FindResource("SuccessBrush");
        IssueStatus.Text = "نُسخ المفتاح إلى الحافظة — الصقه في حقل «مفتاح الترخيص» عند العميل.";
    }

    private void OnSaveCard(object sender, RoutedEventArgs e)
    {
        if (_issuedCode is null)
            return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"brqosh-license-{_issuedMachine}.txt",
            Filter = "بطاقة ترخيص (*.txt)|*.txt|كل الملفات (*.*)|*.*",
        };
        if (dialog.ShowDialog() != true)
            return;

        var card = new StringBuilder()
            .AppendLine("BRQOSH — ترخيص برقوش")
            .AppendLine("────────────────────────────")
            .AppendLine($"اسم المنشأة : {_issuedCompany}")
            .AppendLine($"معرّف الجهاز : {_issuedMachine}")
            .AppendLine($"نوع الاشتراك : {LicenseCodec.PlanLabelAr(_issuedPlan)}")
            .AppendLine($"تاريخ الإصدار: {_issuedOn:yyyy-MM-dd}")
            .AppendLine(_issuedExpiry == DateTime.MaxValue
                ? "تاريخ الانتهاء: بلا انتهاء (دائم)"
                : $"تاريخ الانتهاء: {LicenseCodec.FormatDate(_issuedExpiry)} (UTC)")
            .AppendLine()
            .AppendLine($"مفتاح الترخيص: {LicenseCodec.Format(_issuedCode)}")
            .AppendLine();
        if (_issuedNotes.Length > 0)
            card.AppendLine($"ملاحظات: {_issuedNotes}").AppendLine();
        card.AppendLine("لا تشارك هذا المفتاح مع غيرك — إنه مربوط بجهازك ومنشأتك ولا يعمل على غيرهما.");

        try
        {
            File.WriteAllText(dialog.FileName, card.ToString(), new UTF8Encoding(false));
            IssueStatus.Foreground = (Brush)FindResource("SuccessBrush");
            IssueStatus.Text = $"حُفظت البطاقة في {dialog.FileName}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            IssueStatus.Foreground = (Brush)FindResource("DangerBrush");
            IssueStatus.Text = $"تعذّر الحفظ: {ex.Message}";
        }
    }

    // ── السجلّ ─────────────────────────────────────────────────────────────
    private sealed record LedgerRowView(string Code, string Company, string Plan);

    private void LoadLedger()
    {
        var rows = IssueLedger.ReadRecent(10)
            .Select(r => new LedgerRowView(
                LicenseCodec.Normalize(r.Code) is { } digits ? LicenseCodec.Format(digits) : r.Code,
                r.Company,
                LicenseCodec.PlanLabelAr(r.Plan)))
            .ToList();

        LedgerList.ItemsSource = rows;
        EmptyLedger.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
