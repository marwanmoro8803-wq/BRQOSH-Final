using System.Windows;
using System.Windows.Media;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Desktop.Views;

public partial class LicenseActivationWindow : Window
{
    public bool LicenseActivated { get; private set; }

    private readonly string _companyName;

    public LicenseActivationWindow(string companyName)
    {
        InitializeComponent();
        _companyName = companyName;
        MachineIdBox.Text = LicenseService.MachineId();
        RequestCodeBox.Text = RequestCode.FromDeviceHash(DeviceIdentity.CurrentDeviceHash());
        CompanyBox.Text = companyName;
        KeyBox.Text = LicenseStore.Load()?.Code ?? "";
        KeyHint.Text = LocaleManager.T(
            "مفتاحٌ رقميٌّ من ٢٥ رقماً في خمس مجموعات — يصلك من فريق الدعم، ولا يعمل إلا على هذا الجهاز ولهذه المنشأة.",
            "A 25-digit code in five groups — sent by the support team; it works only on this machine and for this company.");
        ShowCurrentStatus();
        ShowOfflineLock();
    }

    private void ShowCurrentStatus()
    {
        var status = LicenseStatusResolver.Current(_companyName);
        (var text, var brush) = status.Kind switch
        {
            LicenseKind.Trial => (
                LocaleManager.T(
                    $"الفترة التجريبية: باقٍ {status.DaysLeft} من ٧ أيام — تنتهي {status.ExpiresUtc!.Value:yyyy-MM-dd}.",
                    $"Trial period: {status.DaysLeft} of 7 days left — ends {status.ExpiresUtc!.Value:yyyy-MM-dd}."),
                (Brush)FindResource("WarningSoftBrush")),
            LicenseKind.Licensed => status.Plan == LicensePlan.Lifetime
                ? (
                    LocaleManager.T(
                        "الترخيص الحالي: دائم — لا ينتهي.",
                        "Current licence: lifetime — never expires."),
                    (Brush)FindResource("SuccessSoftBrush"))
                : (
                    LocaleManager.T(
                        $"الترخيص الحالي: {status.PlanLabelAr} — باقٍ {status.DaysLeft} يوماً، ينتهي {status.ExpiresUtc!.Value:yyyy-MM-dd}. أدخل مفتاحاً جديداً للاستبدال.",
                        $"Current licence: {status.PlanLabelEn} — {status.DaysLeft} days left, ends {status.ExpiresUtc!.Value:yyyy-MM-dd}. Enter a new code to replace it."),
                    (Brush)FindResource("SuccessSoftBrush")),
            LicenseKind.Expired => (
                LocaleManager.T(
                    "انتهت صلاحية الترخيص — أدخل مفتاح الترخيص لمواصلة العمل.",
                    "The licence has expired — enter a licence code to keep working."),
                (Brush)FindResource("DangerSoftBrush")),
            _ => (
                LocaleManager.T(
                    "ابدأ بإدخال مفتاح الترخيص — أو انسخ معرّف الجهاز أعلاه وأرسله لفريق الدعم.",
                    "Start by entering a licence code — or copy the machine ID above and send it to the support team."),
                (Brush)FindResource("WarningSoftBrush")),
        };

        StatusText.Text = text;
        StatusPanel.Background = brush;
        StatusPanel.Visibility = Visibility.Visible;
    }

    private void OnActivate(object sender, RoutedEventArgs e)
    {
        // ‏الاسمُ من ملفّ الإعداد لا من الحقل: الإقلاعُ التالي يتحقّق به، فلو خالفه المحفوظُ
        // ‏هنا نجح التفعيلُ ثمّ رُفض الإقلاع.
        var company = _companyName.Trim();
        var machineId = LicenseService.MachineId();
        var input = KeyBox.Text.Trim();

        if (input.Length == 0)
        {
            ShowError(LocaleManager.T("أدخل مفتاح الترخيص أولاً.", "Enter a licence code first."));
            return;
        }

        // ‏ملفُّ ترخيصٍ موقَّعٌ (قديم) يُلصَق كما كان — يبدأ بقوسٍ معقوف.
        // ‏رمزُ تفعيلٍ موقَّعٌ من المورّد (BRQA1…) — يُستهلك مرّةً واحدةً لهذا الجهاز.
        if (input.StartsWith(ActivationCodec.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            _ = UiFeedback.RunSafeAsync(() => ActivateOfflineAsync(input));
            return;
        }

        if (input.StartsWith('{'))
        {
            var verdict = RsaLicenseValidator.Validate(company, machineId, input, out _);
            if (verdict != LicenseVerdict.Valid)
            {
                ShowError(Describe(verdict));
                return;
            }

            LicenseStore.SaveSignedLicense(company, input);
            LicenseActivated = true;
            DialogResult = true;
            Close();
            return;
        }

        // ‏مفتاحٌ رقميّ: يُطبَّع ويُفحص بالترميز نفسِه الذي فحصته أداةُ المورِّد.
        var verdict2 = NumericLicense.Decode(input, machineId, company,
            LicenseClock.EffectiveNowUtc(), out _, out _, out _);
        if (verdict2 != NumericLicenseVerdict.Valid)
        {
            ShowError(DescribeNumeric(verdict2));
            return;
        }

        LicenseStore.SaveCode(company, NumericLicense.Normalize(input)!);
        LicenseActivated = true;
        DialogResult = true;
        Close();
    }

    /// <summary>‏(قفل البدء) تفعيلٌ قائمٌ مرفوض — جهازٌ آخر أو ساعةٌ مُرجَعة أو سجلٌّ معبوثٌ به — يُعرض سببُه هنا نصّاً لا نافذة.</summary>
    private void ShowOfflineLock()
    {
        var offline = OfflineActivationService.Current();
        if (offline is null) return;
        if (offline.Verdict == ActivationVerdict.Valid)
        {
            StatusText.Text = LocaleManager.T("هذا الجهاز مفعّلٌ برمز تفعيلٍ من المورّد.", "This device is activated with a vendor activation code.");
            StatusPanel.Background = (Brush)FindResource("SuccessSoftBrush");
            return;
        }

        ShowError(DescribeOffline(offline.Verdict));
    }

    private void OnCopyRequest(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(RequestCodeBox.Text);
            CopiedText.Text = LocaleManager.T("نُسخ رقم الطلب — أرسله إلى فريق الدعم ليصلك رمز التفعيل.", "Request number copied — send it to the support team to receive your activation code.");
            CopiedText.Visibility = Visibility.Visible;
        }
        catch (System.Runtime.InteropServices.ExternalException ex)
        {
            ShowError(LocaleManager.T("تعذّر النسخ إلى الحافظة — حدّد الرقم وانسخه يدوياً.", "Could not copy to the clipboard — select the number and copy it manually.") + " " + ex.Message);
        }
    }

    private async System.Threading.Tasks.Task ActivateOfflineAsync(string code)
    {
        ActivateButton.IsEnabled = false;
        ErrorText.Visibility = Visibility.Collapsed;
        try
        {
            await using var db = AppServices.CreateDb();
            var result = await new OfflineActivationService(db).ActivateAsync(code);
            if (result.Verdict != ActivationVerdict.Valid)
            {
                ShowError(DescribeOffline(result.Verdict));
                return;
            }

            LicenseActivated = true;
            DialogResult = true;
            Close();
        }
        finally
        {
            ActivateButton.IsEnabled = true;
        }
    }

    private static string DescribeOffline(ActivationVerdict verdict) => verdict switch
    {
        ActivationVerdict.Malformed => LocaleManager.T("رمز التفعيل غير مكتمل أو محرَّف — انسخه كاملاً كما وصلك.", "The activation code is incomplete or altered — copy it in full exactly as you received it."),
        ActivationVerdict.NoTrustAnchor => LocaleManager.T("لا مفتاح تحقّق مضمَّن في هذا الإصدار — تواصل مع الدعم.", "This build has no embedded verification key — contact support."),
        ActivationVerdict.BadSignature => LocaleManager.T("رمز التفعيل لم يصدر من المورّد أو عُدِّل.", "The activation code was not issued by the vendor or has been altered."),
        ActivationVerdict.WrongDevice => LocaleManager.T("رمز التفعيل صدر لجهاز آخر — أرسل رقم الطلب الظاهر أعلاه.", "The activation code was issued for another device — send the request number shown above."),
        ActivationVerdict.Expired => LocaleManager.T("انتهت مدّة هذا التفعيل — اطلب رمزاً جديداً من المورّد.", "This activation has expired — request a new code from the vendor."),
        ActivationVerdict.AlreadyUsed => LocaleManager.T("استُعمل رمز التفعيل هذا من قبل — لكلّ تفعيل رمز جديد.", "This activation code has already been used — each activation needs a new code."),
        ActivationVerdict.ClockRolledBack => LocaleManager.T("ساعة الجهاز أُرجعت إلى الوراء — صحّح التاريخ ثمّ أعد المحاولة.", "The device clock was set back — correct the date and try again."),
        ActivationVerdict.LedgerTampered => LocaleManager.T("سجلّ التفعيل المحلّي عُبث به — تواصل مع المورّد.", "The local activation log was tampered with — contact the vendor."),
        _ => LocaleManager.T("تعذّر التفعيل.", "Activation failed."),
    };

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private static string DescribeNumeric(NumericLicenseVerdict verdict) => verdict switch
    {
        NumericLicenseVerdict.Malformed or NumericLicenseVerdict.UnknownVersion
            or NumericLicenseVerdict.UnknownPlan => LocaleManager.T(
            "المفتاح يجب أن يكون ٢٥ رقماً في خمس مجموعات — تحقق من الأرقام وأعد المحاولة.",
            "The code must be 25 digits in five groups — check the digits and try again."),
        NumericLicenseVerdict.DeviceMismatch => LocaleManager.T(
            "المفتاح صادرٌ لجهازٍ آخر — أرسل معرّفَ هذا الجهاز إلى الدعم لإصدار مفتاحٍ له.",
            "The code was issued for a different machine — send this machine ID to support for a code for it."),
        NumericLicenseVerdict.Expired => LocaleManager.T(
            "انتهت صلاحيةُ المفتاح — اطلب تجديدَه من المورِّد.",
            "The code has expired — request a renewal from the vendor."),
        NumericLicenseVerdict.NotYetValid => LocaleManager.T(
            "تاريخُ إصدار المفتاح في المستقبل — راجع الدعم.",
            "The code's issue date is in the future — contact support."),
        NumericLicenseVerdict.BadIntegrity => LocaleManager.T(
            "المفتاح غير صحيحٍ لهذه المنشأة أو أنه مُحرَّف — تحقق من الأرقام كما أُرسلت إليك.",
            "The code does not match this company or has been altered — check the digits exactly as sent."),
        _ => LocaleManager.T("ترخيص غير صالح.", "Invalid license."),
    };

    private static string Describe(LicenseVerdict verdict) => verdict switch
    {
        LicenseVerdict.Malformed => LocaleManager.T(
            "النصُّ الملصَق ليس ملفَّ ترخيص — الصق محتوى الملفّ كاملاً كما أُرسل إليك.",
            "The pasted text is not a license file — paste the whole file exactly as it was sent to you."),
        LicenseVerdict.NoTrustAnchor => LocaleManager.T(
            "هذا التركيبُ بلا مفتاح ثقةٍ صالح — عطلٌ في التنصيب لا في الترخيص؛ أعد التنصيب أو راجع الدعم.",
            "This installation has no usable trust key — an installation fault, not a license fault; reinstall or contact support."),
        LicenseVerdict.CompanyMismatch => LocaleManager.T(
            "الترخيصُ صادرٌ لاسم منشأةٍ آخر — يجب أن يطابق اسمَ المنشأة أعلاه حرفاً بحرف.",
            "The license was issued to a different company name — it must match the name above exactly."),
        LicenseVerdict.MachineMismatch => LocaleManager.T(
            "الترخيصُ صادرٌ لجهازٍ آخر — أرسل معرّفَ هذا الجهاز إلى الدعم لإصدار ترخيصٍ له.",
            "The license was issued to a different machine — send this machine ID to support for a license for it."),
        LicenseVerdict.Expired => LocaleManager.T(
            "انتهت صلاحيةُ الترخيص — اطلب تجديدَه من المورِّد.",
            "The license has expired — request a renewal from the vendor."),
        LicenseVerdict.BadSignature => LocaleManager.T(
            "توقيعُ الترخيص ليس من المورِّد — الملفُّ مُحرَّف أو صادرٌ بمفتاحٍ آخر.",
            "The license signature is not the vendor's — the file was altered or issued with another key."),
        _ => LocaleManager.T("ترخيص غير صالح.", "Invalid license."),
    };
}
