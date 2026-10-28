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
        Loaded += (_, _) => StartHero();
        Closed += (_, _) => _heroTimer.Stop();
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
            // ‏(طلبُ المالك) ‏كانت صفحةُ الانتظار مملوكةً لهذا المعالج وتُعرض فوقه — نافذتان متراكبتان. الآن: يُطوى المعالجُ وتظهر الصفحةُ وحدَها،
            // ‏ويعود المعالجُ إن أخفق الاتصال. (الطيُّ لا الإخفاء: إخفاءُ نافذةٍ مشروطةٍ ينهي ShowDialog.)
            waiting = new StartupSplashWindow();
            waiting.SetStatus("جارٍ إعداد خادم قاعدة البيانات على هذا الجهاز…", "Setting up the database server on this computer…");
            WindowState = WindowState.Minimized;
            waiting.Show();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        }

        try
        {
            connectionError = await ConnectionPanel.ValidateProbeAndSaveAsync();
        }
        finally
        {
            if (waiting is not null)
                await waiting.FadeOutAsync();
            waiting?.Close();
            StartButton.IsEnabled = true;
        }

        if (connectionError is not null)
        {
            WindowState = WindowState.Normal;
            Activate();
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

    // ‏(طلبُ المالك) ‏**شاشةُ التسجيل حيّة**: لوحةٌ جانبيّةٌ تتبدّل كلَّ بضع ثوانٍ بين أنواع الأنشطة — أيقونةٌ وعنوانٌ وسطرُ خدمات —
    // ‏بظهورٍ وانزلاقٍ خفيفين وتوهّجٍ يتنفّس خلفها (ألوانُ صفحة الانتظار نفسُها). ومن عطّل الرسومَ المتحرّكة تتبدّل النصوصُ بلا حركة.
    private readonly System.Windows.Threading.DispatcherTimer _heroTimer = new() { Interval = TimeSpan.FromSeconds(3.4) };
    private int _heroIndex;

    private static (string Icon, string Ar, string En, string ServicesAr, string ServicesEn)[] BusinessTypes =>
    [
        ("\U0001F3E2", "شركات تجارية", "Trading companies", "فواتير ضريبية · مخزون · عملاء وموردون · ذمم", "Tax invoices · Inventory · Customers & suppliers · Receivables"),
        ("\U0001F3ED", "مصانع", "Factories", "قوائم المواد · أوامر الإنتاج · تكلفة المنتج", "Bills of materials · Work orders · Product costing"),
        ("\U0001F6D2", "متاجر وتجزئة", "Shops & retail", "نقاط البيع · الباركود · الورديات والمخزون", "Point of sale · Barcodes · Shifts and stock"),
        ("\U0001F37D", "مطاعم", "Restaurants", "نقاط البيع · الوصفات واستهلاك المكوّنات · الورديات", "POS · Recipes and ingredient usage · Shifts"),
        ("\U0001F3D7", "مقاولات", "Contracting", "المشاريع والمستخلصات · مقاولو الباطن · المحتجزات", "Projects and progress billing · Subcontractors · Retentions"),
        ("\U0001F4BC", "خدمات ومكاتب", "Services & offices", "عقود وفوترة دورية · مراكز التكلفة · الرواتب", "Contracts and recurring billing · Cost centres · Payroll"),
        ("\U0001FA7A", "عيادات", "Clinics", "مواعيد وزيارات · فوترة المرضى · صرف الأدوية", "Appointments and visits · Patient billing · Dispensing"),
    ];

    private void StartHero()
    {
        HeroHeadline.Text = LocaleManager.T("نظام واحد لكل نشاط", "One system for every business");
        HeroTagline.Text = LocaleManager.T("اختر نشاطك، ويجهّز برقوش الوحدات والحسابات المناسبة له.", "Pick your business and BRQOSH prepares the right modules and accounts.");
        HeroDots.Children.Clear();
        foreach (var _ in BusinessTypes)
            HeroDots.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 8, Height = 8, Margin = new Thickness(4, 0, 4, 0),
                Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC9, 0x8A, 0x4B)),
            });
        ShowHero(0, animate: false);

        _heroTimer.Tick += (_, _) => ShowHero((_heroIndex + 1) % BusinessTypes.Length, Motion.Enabled);
        _heroTimer.Start();

        if (!Motion.Enabled)
            return;
        var sine = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };
        var breathe = new System.Windows.Media.Animation.DoubleAnimation(0.88, 1.12, TimeSpan.FromSeconds(3.0))
            { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever, EasingFunction = sine };
        HeroGlowScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, breathe);
        HeroGlowScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, breathe);
        HeroGlow.BeginAnimation(System.Windows.Media.Brush.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0.35, 0.75, TimeSpan.FromSeconds(3.0))
            { AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever, EasingFunction = sine });
    }

    private void ShowHero(int index, bool animate)
    {
        _heroIndex = index;
        var (icon, ar, en, servicesAr, servicesEn) = BusinessTypes[index];
        HeroIcon.Text = icon;
        HeroTitle.Text = LocaleManager.T(ar, en);
        HeroServices.Text = LocaleManager.T(servicesAr, servicesEn);
        for (var i = 0; i < HeroDots.Children.Count; i++)
            HeroDots.Children[i].Opacity = i == index ? 1.0 : 0.3;

        if (!animate)
            return;
        var ease = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        HeroCard.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450)) { EasingFunction = ease });
        HeroCardShift.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new System.Windows.Media.Animation.DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(450)) { EasingFunction = ease });
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
