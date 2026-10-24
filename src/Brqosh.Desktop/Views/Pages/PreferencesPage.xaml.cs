using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Navigation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Themes;
using Brqosh.Desktop.Ui;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Setup;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// (أسئلةُ المالك 2026-10-24) <b>تفضيلاتُ البرنامج وهويّتُه — صفحةٌ لكلّ مستخدم لا خانةٌ في الترويسة.</b>
///
/// <para><b>ما كان:</b> الثيمُ زرٌّ في الترويسة لا يحفظ اختيارَه، ولا موضعَ في أيّ شاشة إعدادات يُختار منه المظهر؛ وإعداداتُ المنشأة
/// للمدير وحده. وبيانات البرنامج نفسِه (الإصدار والمورّد والشروط والتصنيف) مبعثرةٌ بين نافذة الإقلاع و«حالة النظام» (للمدير)
/// فلا يراها غيرُه. <b>الآن</b> لكلّ دورٍ هذه الصفحة: يختار المظهرَ واللغة (يُحفظان)، ويرى «عن البرنامج»، ويفتح الشروطَ والدليل
/// والدعم، ويفحص التحديثَ إن كانت له قناةٌ محفوظة.</para>
/// </summary>
public partial class PreferencesPage : UserControl
{
    /// <param name="Ltr">القيمةُ لاتينيّةٌ كلُّها (إصدار، مسار، اسمٌ إنجليزيّ): تُعرض يسارَ-يمين بأرقامٍ لاتينيّة.</param>
    public sealed record AboutRow(string Label, string Value, bool Ltr = false);

    private bool _loading;

    public PreferencesPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Bind();
        LocaleManager.Changed += OnLocaleChanged;
        Unloaded += (_, _) => LocaleManager.Changed -= OnLocaleChanged;
    }

    private void OnLocaleChanged()
    {
        LocaleManager.ApplyTo(this);
        if (IsLoaded)
            Bind();
    }

    private void Bind()
    {
        _loading = true;
        try
        {
            TitleText.Text = LocaleManager.T("تفضيلات البرنامج", "Preferences");
            SubtitleText.Text = LocaleManager.T(
                "المظهر واللغة لك أنت · عن البرنامج والشروط والتحديث والدعم",
                "Appearance and language for you · about the program, terms, updates and support");

            AppearanceTitle.Text = LocaleManager.T("المظهر واللغة", "Appearance and language");
            ThemeLabel.Text = LocaleManager.T("المظهر", "Appearance");
            ThemeBox.ItemsSource = new[]
            {
                new Choice(true, LocaleManager.T("داكن", "Dark")),
                new Choice(false, LocaleManager.T("فاتح", "Light")),
            };
            ThemeBox.DisplayMemberPath = nameof(Choice.Label);
            ThemeBox.SelectedItem = ((IEnumerable<Choice>)ThemeBox.ItemsSource).First(c => c.Value == ThemeManager.IsDark);
            ThemeHint.Text = LocaleManager.T(
                "يُطبَّق فوراً ويُحفظ لمستخدم ويندوز هذا فيعود عند كل تشغيل. ألوان النصوص والخلفيات تتبدّل معاً.",
                "Applies immediately and is remembered for this Windows user on every start. Text and background colours switch together.");

            LanguageLabel.Text = LocaleManager.T("لغة الواجهة", "Interface language");
            LanguageBox.ItemsSource = new[]
            {
                new Choice(false, "العربية"),
                new Choice(true, "English"),
            };
            LanguageBox.DisplayMemberPath = nameof(Choice.Label);
            LanguageBox.SelectedItem = ((IEnumerable<Choice>)LanguageBox.ItemsSource).First(c => c.Value == LocaleManager.IsEnglish);
            LanguageHint.Text = LocaleManager.T(
                "تغيّر القوائم والشاشات والاتجاه (يمين ← يسار أو العكس). والمطبوعات — السندات والكشوف والتقارير — تصدر بلغة الواجهة، أمّا الفاتورة الضريبية فتبقى عربية دائماً (متطلب الهيئة).",
                "Changes menus, screens and direction (right-to-left or left-to-right). Printed vouchers, statements and reports follow the interface language; tax invoices always stay in Arabic (authority requirement).");

            AboutTitle.Text = LocaleManager.T("عن البرنامج", "About the program");
            AboutList.ItemsSource = BuildAboutRows();

            TermsBtn.Content = LocaleManager.T("الشروط والأحكام", "Terms and conditions");
            ManualBtn.Content = LocaleManager.T("دليل المستخدم", "User manual");
            UpdateBtn.Content = LocaleManager.T("فحص التحديث", "Check for update");
            SupportBtn.Content = LocaleManager.T("الدعم والشكاوى", "Support and complaints");
            StatusBtn.Content = LocaleManager.T("حالة النظام", "System status");
            StatusBtn.Visibility = PermissionService.CanAccess(AppServices.CurrentUser, "diagnostics")
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        finally
        {
            _loading = false;
        }
    }

    private sealed record Choice(bool Value, string Label);

    private static List<AboutRow> BuildAboutRows()
    {
        // ‏تجميعةُ البرنامج نفسُها لا «نقطةُ الدخول»: هي التجميعةُ ذاتها في التشغيل الحيّ، وفي الاختبار نقطةُ الدخول مضيفُ الاختبارات.
        var entry = typeof(PreferencesPage).Assembly;
        var copyright = entry.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        return
        [
            Latin(LocaleManager.T("البرنامج", "Program"), entry.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "BRQOSH ERP"),
            Latin(LocaleManager.T("الإصدار", "Version"), InstalledVersion(entry)),
            Latin(LocaleManager.T("المورّد", "Vendor"), entry.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "BRQOSH"),
            Latin(LocaleManager.T("موقع المورّد", "Vendor website"), "brqosh.com"),
            Latin(LocaleManager.T("بريد الدعم", "Support e-mail"), "support@brqosh.com"),
            Latin(LocaleManager.T("هاتف / واتساب الدعم", "Support phone / WhatsApp"), "+966 58 277 8405"),
            Latin(LocaleManager.T("حقوق النشر", "Copyright"), string.IsNullOrWhiteSpace(copyright) ? "BRQOSH" : copyright),
            new(LocaleManager.T("نوع الاستخدام", "Type of use"), LocaleManager.T(
                "برنامج محاسبة وإدارة أعمال للمنشآت — للاستخدام المهني والتجاري.",
                "Accounting and business-management software for companies — professional and commercial use.")),
            new(LocaleManager.T("الفئة العمرية", "Age group"), LocaleManager.T(
                "موجّه للبالغين (١٨ سنة فأكثر) من العاملين في المنشأة؛ ليس برنامجاً للأطفال.",
                "Intended for adults (18 and over) working in the company; not a program for children.")),
            Latin(LocaleManager.T("محرك قاعدة البيانات", "Database engine"), AppServices.ActiveProviderLabel),
            Latin(LocaleManager.T("مجلد بيانات هذا الجهاز", "This device's data folder"), Safe(() => BrqoshDataRoot.Path())),
            new(LocaleManager.T("الترخيص", "Licence"), LocaleManager.T(
                "تفاصيل الترخيص والوحدات المرخّصة في «إعدادات المنشأة» (للمدير).",
                "Licence details and licensed modules are in 'Company settings' (administrator).")),
        ];
    }

    private static AboutRow Latin(string label, string value) =>
        new(label, value, Ltr: value.All(c => c < 128 || c == '\u00A9'));

    /// <summary>رقمُ الإصدار المُثبَّت (دون بصمة الالتزام) — وهو ما يقارنه فحصُ التحديث.</summary>
    private static string InstalledVersion(Assembly entry)
    {
        var informational = entry.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
            return entry.GetName().Version?.ToString(3) ?? "—";
        var plus = informational.IndexOf('+');
        return plus > 0 ? informational[..plus] : informational;
    }

    private static string Safe(Func<string> read)
    {
        try { return read(); }
        catch (Exception) { return "—"; }
    }

    private void OnThemeChosen(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeBox.SelectedItem is not Choice choice || choice.Value == ThemeManager.IsDark)
            return;

        // ‏ThemeManager.Apply يُطلق Changed فتُعيد النافذةُ الرئيسةُ رسمَ الصفحة (وهذه منها) وتحفظ الاختيار.
        ThemeManager.Apply(choice.Value);
    }

    private void OnLanguageChosen(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageBox.SelectedItem is not Choice choice || choice.Value == LocaleManager.IsEnglish)
            return;

        AppNavigator.SetLanguage(choice.Value);
    }

    private void OnTerms(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(EulaAcceptance.TextPath))
            {
                UiFeedback.ShowWarning(LocaleManager.T(
                    "نصّ الاتفاقية غير موجود بجوار البرنامج — أعد التثبيت.",
                    "The agreement text is missing beside the program — reinstall."));
                return;
            }

            new EulaWindow(File.ReadAllText(EulaAcceptance.TextPath), readOnly: true) { Owner = Window.GetWindow(this) }.ShowDialog();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private void OnManual(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "UserManual-ar.md");
        try
        {
            if (!File.Exists(path))
            {
                UiFeedback.ShowWarning(LocaleManager.T(
                    "دليل المستخدم غير موجود بجوار البرنامج.",
                    "The user manual is not beside the program."));
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        using var busy = UiBusyGuard.Enter(sender);
        if (!busy.Acquired) return;

        var source = UpdateChannelSettings.Load();
        if (string.IsNullOrWhiteSpace(source))
        {
            UpdateText.Text = LocaleManager.T(
                "لم تُضبط قناة تحديث على هذا الجهاز بعد — يضبطها المدير من «حالة النظام».",
                "No update channel is set on this device yet — the administrator sets it in 'System status'.");
            return;
        }

        try
        {
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<UpdateCheckService>().CheckAsync(source);
            UpdateText.Text = result.Status switch
            {
                UpdateStatus.UpdateAvailable => LocaleManager.T(
                    $"يوجد إصدارٌ أحدث: {result.AvailableVersion} (المثبَّت {result.InstalledVersion}) — يُنزَّل من «حالة النظام».",
                    $"A newer version is available: {result.AvailableVersion} (installed {result.InstalledVersion}) — download it from 'System status'."),
                UpdateStatus.UpToDate => LocaleManager.T(
                    $"أنت على أحدث إصدار ({result.InstalledVersion}).",
                    $"You are on the latest version ({result.InstalledVersion})."),
                UpdateStatus.Untrusted => LocaleManager.T(
                    $"رُفض بيان التحديث: {result.Failure}",
                    $"The update manifest was refused: {result.Failure}"),
                _ => result.Failure ?? LocaleManager.T("لا قناة تحديث.", "No update channel."),
            };
        }
        catch (Exception ex)
        {
            UpdateText.Text = ex.Message;
        }
    }

    private void OnSupport(object sender, RoutedEventArgs e) => AppNavigator.Go("customer_support");

    private void OnStatus(object sender, RoutedEventArgs e) => AppNavigator.Go("diagnostics");
}
