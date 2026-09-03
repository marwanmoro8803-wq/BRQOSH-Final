using System.Windows;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Desktop.Views;

public partial class LicenseActivationWindow : Window
{
    public bool LicenseActivated { get; private set; }

    public LicenseActivationWindow(string companyName)
    {
        InitializeComponent();
        MachineIdBox.Text = LicenseService.MachineId();
        CompanyBox.Text = companyName;
        KeyBox.Text = LicenseStore.Load()?.SignedLicenseJson ?? "";
    }

    private void OnActivate(object sender, RoutedEventArgs e)
    {
        var company = CompanyBox.Text.Trim();
        var machineId = MachineIdBox.Text.Trim();
        var signedJson = KeyBox.Text.Trim();

        // (CEEF-236 · ت-١) ‏**الرسالةُ تُسمّي السبب.** كانت كلُّ الإخفاقات تقول «ترخيص غير صالح
        // ‏لهذا الجهاز أو اسم المنشأة» — وحين كان السببُ غيابَ مفتاح الثقة في التركيب أرسلت
        // ‏العميلَ يُراجع اسمَ منشأته. فيُطلب الحكمُ باسمه ويُترجَم إلى ما يفعله المستخدم بعده.
        var verdict = RsaLicenseValidator.Validate(company, machineId, signedJson, out _);
        if (verdict != LicenseVerdict.Valid)
        {
            ErrorText.Text = Describe(verdict);
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        LicenseStore.SaveSignedLicense(company, signedJson);
        LicenseActivated = true;
        DialogResult = true;
        Close();
    }

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
