using System.Windows;
using System.IO;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Brqosh.Application.Intelligence;
using Brqosh.Application.Setup;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Navigation;
using Brqosh.Desktop.Ui;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Themes;
using Brqosh.Desktop.Views;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Intelligence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brqosh.Desktop;

public partial class MainWindow : Window
{
    private SetupState _setup;
    private readonly Dictionary<string, Button> _navButtons = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _idleTimer;
    private readonly DispatcherTimer _backupTimer;
    private bool _backupRunning;
    private string _activePageKey = "dashboard";
    private bool _branchBinding;
    private bool _companyBinding;
    private bool _aiInputWired;

    /// <summary>
    /// ‏(ح-١٦٤ · security-licensing-tenancy#2) ما يمنحه الترخيصُ من وحدات — يُقرأ مرّةً عند فتح النافذة؛ و<c>null</c> لا يحصر.
    /// ‏والترخيصُ يتبع الجهازَ لا المنشأةَ المختارة، فتبديلُ المنشأة لا يُعيد قراءتَه.
    /// </summary>
    private readonly IReadOnlySet<ModuleKey>? _licensedModules;

    private sealed record BranchOption(long Id, string Display, bool IsHeadquarters);
    private sealed record CompanyOption(long Id, string Display, BusinessProfile Profile);

    public MainWindow(SetupState setup)
    {
        _setup = setup;
        _licensedModules = Brqosh.Infrastructure.Licensing.LicenseStore.LicensedModulesFor(
            setup.LicensedCompanyName ?? setup.CompanyName);
        InitializeComponent();
        // ‏(UI-AUDIT · P1) الصفحةُ الطويلة تُمرَّر، والجدولُ يبقى محدودَ الارتفاع.
        Ui.PageViewport.Attach(PageViewportScroll, PageHost);

        // ‏لوحةُ المساعد تُطوى تلقائيّاً على النوافذ الضيّقة — كانت تُفتح دائماً فتترك الصفحةَ ٤٥٢ بكسلاً عند الحدّ الأدنى
        // ‏وتُقصّ أزرارُ الأشرطة. فإذا اختار المستخدمُ فتحَها أو طيَّها بزرّها فاختيارُه هو المُتّبَع.
        SizeChanged += (_, _) => ApplyAiPanelPolicy();
        PageHeader.SizeChanged += (_, e) => ApplyHeaderFit(e.NewSize.Width);
        Loaded += (_, _) => ApplyAiPanelPolicy();

        // ‏(ح-١٢٠) ‏**وضعُ القراءة يُرى في العنوان** — فلا يُحسب رفضُ الحفظ عطلاً.
        if (Brqosh.Infrastructure.Licensing.ReadOnlyBooks.IsActive)
            Title += " — " + LocaleManager.T("للقراءة فقط (الترخيص منتهٍ)", "Read-only (licence expired)");

        // ‏(أسئلةُ المالك 2026-10-24) ‏المظهرُ واللغةُ من تفضيلات المستخدم المحفوظة — كان يبدأ داكناً عربيّاً دائماً.
        var uiPrefs = UiPreferencesStore.Load();
        ThemeManager.Apply(uiPrefs.Dark);
        DensityManager.Apply(uiPrefs.Compact);
        if (uiPrefs.English)
        {
            LocaleManager.Set(true);
            FlowDirection = LocaleManager.Flow;
            LanguageToggleBtn.Content = LocaleManager.LanguageLabel;
        }
        RefreshThemeToggleText();
        KeyboardNavigationHelper.ApplyShellHeader(BranchCombo, ThemeToggleBtn);
        LocaleManager.Changed += OnLocaleChanged;
        ThemeManager.Changed += OnThemeChanged;
        DensityManager.Changed += OnDensityChanged;
        Closed += (_, _) => DensityManager.Changed -= OnDensityChanged;
        BuildAiPanelShell();
        _ = RefreshAiPanelAsync();
        CompanyNameText.Text = setup.CompanyName;
        ProfileBadge.Text = ProfileLabel(setup.Profile);

        PreviewMouseMove += (_, _) => AppServices.TouchSession();
        PreviewKeyDown += (_, e) =>
        {
            AppServices.TouchSession();
            // ‏Ctrl+K: إلى بحث القائمة من أيّ شاشة — لا فأرة بين الفكرة والصفحة.
            if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
            {
                FocusNavSearch();
                e.Handled = true;
            }
            else if (HandlePageShortcut(e))
                e.Handled = true;
        };
        PreviewMouseDown += (_, _) => AppServices.TouchSession();

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _idleTimer.Tick += (_, _) => CheckGlobalIdle();
        _idleTimer.Start();
        Closed += (_, _) => _idleTimer.Stop();

        // ‏(CEEF-237 · ت-٤) النسخُ الاحتياطيّ يقع من نفسِه.
        //
        // ‏كان مستدعي BackupService الوحيد زرّاً في شاشة، والكتيّبُ يَعِد بنسخةٍ يوميّة —
        // ‏فمنشأةٌ لم يضغط أحدٌ زرَّها شهراً ثمّ تلف قرصُها تفقد دفاترَها كلَّها.
        // ‏والفاصلُ نصفُ ساعةٍ لا يومٌ عمداً: جهازٌ يُطفأ مساءً ويُشغَّل صباحاً لا يبلغ
        // ‏مؤقّتاً بأربعٍ وعشرين ساعة أبداً، والاستحقاقُ يُقاس بعمر آخرِ ملفٍّ لا بعدّاد.
        _backupTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _backupTimer.Tick += async (_, _) => await RunScheduledBackupAsync();
        _backupTimer.Start();
        Closed += (_, _) => _backupTimer.Stop();
        _ = RunScheduledBackupAsync();
        StatusBackupBadge.MouseLeftButtonUp += (_, _) => Navigate("backup", NavTitleFor("backup"));

        // ‏(جاهزيّةُ الإطلاق · ح-١٥٦ · ‏`tax-zatca#8`) ‏**المعلَّقُ لدى الهيئة يُعاد إرسالُه من نفسه.** كان زرّاً في سجلّ الفواتير
        // ‏الإلكترونيّة، ومهلةُ الإبلاغ ٢٤ ساعة. ‏المؤقّتُ ينبض كلَّ دقيقة، و`ZatcaRetryBackoff` يقرّر متى تُحاوَل الدفعة.
        var zatcaTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        zatcaTimer.Tick += async (_, _) => await RunZatcaResubmitAsync();
        zatcaTimer.Start();
        Closed += (_, _) => zatcaTimer.Stop();

        // ‏(ح-١٤١ · ‏`installer-deployment-ops#12`) ‏**التحديثُ يُفحص من نفسه.** كان الفحصُ زرّاً في
        // ‏«حالة النظام» لا يضغطه أحد، فقناةٌ موقَّعةٌ لا يبلغها عميل. ‏يُفحص مرّةً بعد الإقلاع إن حُفظت قناة،
        // ‏ويُخبَر المالكُ ولا يُنزَّل شيءٌ ولا يُنصَّب.
        _ = CheckForUpdateAtStartupAsync();

        // ‏(جاهزيّةُ الإطلاق · ح-١٢٧ · ‏`security-licensing-tenancy#1`) ‏**الجلسةُ الطويلةُ لا تتجاوز الانتهاء.**
        // ‏كان الحكمُ عند الإقلاع وحدَه، فتطبيقٌ لا يُغلق يعمل بترخيصٍ انتهى أمس. ‏يُحفظ انتهاءُ الترخيص الصالح عند بدء الجلسة،
        // ‏ويُقارَن كلَّ ربع ساعةٍ بالوقت الفعّال (`LicenseClock`) — فإن بلغه دخلت الجلسةُ وضعَ القراءة، ولا تُغلق.
        var licensedName = setup.LicensedCompanyName ?? setup.CompanyName;
        var licenseExpiresUtc = Brqosh.Infrastructure.Licensing.LicenseStore.ValidLicenseExpiresUtc(
            licensedName, Brqosh.Infrastructure.Licensing.LicenseClock.EffectiveNowUtc());
        var licenseTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        licenseTimer.Tick += (_, _) => EnterReadOnlyIfLicenseExpired(licenseExpiresUtc);
        licenseTimer.Start();
        Closed += (_, _) => licenseTimer.Stop();

        // ‏عملةُ دفاتر المنشأة الجارية قبل أوّل شاشة — فلا تُرسم اللوحةُ بالريال ثمّ تتغيّر.
        CurrencyLabel.Load();
        // ‏الشاشاتُ تطلب فتحَ شاشةٍ أخرى (إجراءاتُ اللوحة) من هنا، فتمرّ بفحوص Navigate نفسِها.
        Navigation.AppNavigator.Handler = key => Navigate(key, NavTitleFor(key));
        Navigation.AppNavigator.LanguageHandler = english =>
        {
            if (LocaleManager.IsEnglish != english)
                OnToggleLanguage(this, new RoutedEventArgs());
        };
        Closed += (_, _) =>
        {
            Navigation.AppNavigator.Handler = null;
            Navigation.AppNavigator.LanguageHandler = null;
        };
        BindUserHeader();
        BindStatusBar();
        _ = RefreshDbHealthAsync();
        _ = BindCompaniesAsync();
        BuildNavigation();
        UiFeedback.AttachToastHost(ToastOverlay);
        Navigate("dashboard", NavTitleFor("dashboard"));
    }

    private async Task BindCompaniesAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var user = AppServices.CurrentUser;
            IQueryable<Company> query = db.Companies
                .Where(c => c.OperationalMode == CompanyOperationalMode.Active);

            if (user?.Role != UserRole.Administrator)
                query = query.Where(c => c.Id == user!.CompanyId);

            var rows = await query
                .OrderBy(c => c.NameAr)
                .Select(c => new CompanyOption(c.Id, c.NameAr, c.Profile))
                .ToListAsync();

            _companyBinding = true;
            CompanyCombo.ItemsSource = rows;
            if (rows.Count == 0)
            {
                CompanyCombo.IsEnabled = false;
                return;
            }

            var selected = rows.FirstOrDefault(r => r.Id == AppServices.CompanyId) ?? rows[0];
            CompanyCombo.SelectedItem = selected;
            CompanyCombo.IsEnabled = rows.Count > 1;
            await ApplyCompanyContextAsync(selected, reloadBranches: true);
        }
        catch
        {
            CompanyCombo.IsEnabled = false;
        }
        finally
        {
            _companyBinding = false;
        }
    }

    private async void OnCompanyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_companyBinding) return;
        if (CompanyCombo.SelectedItem is not CompanyOption opt) return;
        await ApplyCompanyContextAsync(opt, reloadBranches: true);
        Navigate(_activePageKey, NavTitleFor(_activePageKey));
    }

    internal async Task SwitchToCompanyAsync(long companyId)
    {
        if (CompanyCombo.ItemsSource is not IEnumerable<CompanyOption> rows)
        {
            await BindCompaniesAsync();
            rows = CompanyCombo.ItemsSource as IEnumerable<CompanyOption> ?? [];
        }

        var target = rows.FirstOrDefault(r => r.Id == companyId);
        if (target is null)
        {
            await BindCompaniesAsync();
            target = (CompanyCombo.ItemsSource as IEnumerable<CompanyOption>)?.FirstOrDefault(r => r.Id == companyId);
        }

        if (target is null) return;

        _companyBinding = true;
        CompanyCombo.SelectedItem = target;
        _companyBinding = false;
        await ApplyCompanyContextAsync(target, reloadBranches: true);
        Navigate("dashboard", NavTitleFor("dashboard"));
    }

    private async Task ApplyCompanyContextAsync(CompanyOption company, bool reloadBranches)
    {
        AppServices.CompanyId = company.Id;
        AppServices.TouchSession();
        CurrencyLabel.Load();

        // ‏(جاهزيّةُ الإطلاق · ح-٦٦ · sector-modules#10) **القوائمُ تتبع نظامَ المنشأة المطبَّقة — هنا لا عند مستدعيها.**
        // ‏كان هذا يُحدّث النظامَ والشارةَ ولا يُعيد بناءَ القوائم، وأوّلُ بناءٍ يقع في المُنشئ قبل أن
        // ‏يُربَط هذا (`BindCompaniesAsync` بلا انتظار) — فتبقى قوائمُ نظامٍ والشارةُ تُسمّي غيرَه حتى
        // ‏تبديل اللغة. والقوائمُ تُبنى من `_setup.Profile` وحدَه، فالمقارنةُ قبل التعيين دقيقة.
        var profileChanged = _setup.Profile != company.Profile;
        _setup = _setup with
        {
            Profile = company.Profile,
            CompanyName = company.Display,
        };
        CompanyNameText.Text = company.Display;
        ProfileBadge.Text = ProfileLabel(company.Profile);
        if (profileChanged)
        {
            BuildNavigation();
            SetActiveNav(_activePageKey);
        }
        if (reloadBranches)
            await BindBranchesAsync();
        UpdateSessionStatus();
        _ = RefreshAiPanelAsync();
    }

    private async Task BindBranchesAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var rows = await db.Branches
                .Where(b => b.CompanyId == AppServices.CompanyId && b.IsActive)
                .OrderByDescending(b => b.IsHeadquarters)
                .ThenBy(b => b.Code)
                .Select(b => new BranchOption(b.Id, b.Code + " — " + b.NameAr, b.IsHeadquarters))
                .ToListAsync();

            _branchBinding = true;
            BranchCombo.ItemsSource = rows;
            if (rows.Count == 0)
            {
                AppServices.CurrentBranchId = null;
                AppServices.CurrentBranchLabel = null;
                BranchCombo.IsEnabled = false;
                return;
            }

            var preferred = AppServices.CurrentBranchId;
            var selected = preferred is long pid
                ? rows.FirstOrDefault(r => r.Id == pid) ?? rows[0]
                : rows.FirstOrDefault(r => r.IsHeadquarters) ?? rows[0];
            BranchCombo.SelectedItem = selected;
            AppServices.CurrentBranchId = selected.Id;
            AppServices.CurrentBranchLabel = selected.Display;
            BranchCombo.IsEnabled = rows.Count > 1;
            UpdateSessionStatus();
        }
        catch
        {
            BranchCombo.IsEnabled = false;
        }
        finally
        {
            _branchBinding = false;
        }
    }

    private void OnBranchChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_branchBinding) return;
        if (BranchCombo.SelectedItem is not BranchOption opt) return;
        AppServices.CurrentBranchId = opt.Id;
        AppServices.CurrentBranchLabel = opt.Display;
        AppServices.TouchSession();
        UpdateSessionStatus();
    }

    private void BindUserHeader()
    {
        var user = AppServices.CurrentUser;
        var displayName = user?.DisplayNameAr ?? user?.Username ?? "مستخدم";
        UserNameText.Text = displayName;
        UserRoleText.Text = RoleLabel(user?.Role);
        HeaderUserText.Text = displayName;
        // (O-27) التاريخ يتبع اللغة الجارية — كان مثبّتاً على ar-SA فيظهر
        // «السبت، 25 يوليو 2026» داخل واجهة إنجليزية بالكامل.
        DateText.Text = LocaleManager.IsEnglish
            ? DateTime.Now.ToString("dddd, d MMMM yyyy", new System.Globalization.CultureInfo("en-GB"))
            : DateTime.Now.ToString("dddd، d MMMM yyyy", new System.Globalization.CultureInfo("ar-SA"));
        UserInitialText.Text = displayName.Length > 0 ? displayName[..1] : "م";
        UpdateSessionStatus();
    }

    private void BindStatusBar()
    {
        StatusDbText.Text = $"قاعدة البيانات: {AppServices.ActiveProviderLabel}";
        UpdateSessionStatus();
    }

    private async Task RefreshDbHealthAsync()
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var ok = await db.Database.CanConnectAsync();
            StatusDbDot.Background = (System.Windows.Media.Brush)FindResource(ok ? "SuccessBrush" : "DangerBrush");
            var state = ok
                ? LocaleManager.T("متصل", "Connected")
                : LocaleManager.T("غير متصل", "Disconnected");
            StatusDbText.Text = $"{LocaleManager.T("قاعدة البيانات", "Database")}: {AppServices.ActiveProviderLabel} · {state}";
        }
        catch
        {
            StatusDbDot.Background = (System.Windows.Media.Brush)FindResource("DangerBrush");
            StatusDbText.Text =
                $"{LocaleManager.T("قاعدة البيانات", "Database")}: {AppServices.ActiveProviderLabel} · " +
                LocaleManager.T("خطأ اتصال", "Connection error");
        }
    }

    private void UpdateSessionStatus()
    {
        var user = AppServices.CurrentUser?.Username;
        var branch = AppServices.CurrentBranchLabel;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(user))
            parts.Add($"{LocaleManager.T("جلسة", "Session")} · {user}");
        else
            parts.Add(LocaleManager.T("الجلسة نشطة", "Session active"));
        if (!string.IsNullOrWhiteSpace(branch))
            parts.Add(branch);
        StatusSessionText.Text = string.Join(" · ", parts);
    }

    private void CheckGlobalIdle()
    {
        if (AppServices.CurrentUser is null) return;
        if (AppServices.IsSessionExpired())
        {
            EnforceReauthentication("انتهت الجلسة بسبب الخمول. سجّل الدخول للمتابعة.");
            return;
        }

        _ = CheckSessionRevokedAsync();
    }

    private async Task CheckSessionRevokedAsync()
    {
        if (AppServices.CurrentUser is null) return;
        if (!await AppServices.IsSessionRevokedAsync()) return;
        Dispatcher.Invoke(() =>
            EnforceReauthentication("تم إنهاء جلستك من قبل المسؤول. سجّل الدخول للمتابعة."));
    }

    private bool EnforceReauthentication(string? message = null)
    {
        AppServices.CurrentUser = null;
        if (!string.IsNullOrWhiteSpace(message))
            UiDialog.Show(this, message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);

        var login = new LoginWindow { Owner = this };
        if (login.ShowDialog() != true || login.AuthenticatedUser is null)
        {
            UiDialog.Show(this, "انتهت الجلسة ولم يُعاد تسجيل الدخول — سيتم إغلاق البرنامج.",
                "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
            System.Windows.Application.Current.Shutdown();
            return false;
        }

        var user = login.AuthenticatedUser;
        if (user.MustChangePasswordOnNextLogin)
        {
            var change = new PasswordChangeWindow(user) { Owner = this };
            if (change.ShowDialog() != true || change.UpdatedUser is null)
            {
                UiDialog.Show(this, "يجب تغيير كلمة المرور قبل المتابعة — سيتم إغلاق البرنامج.",
                    "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Warning);
                System.Windows.Application.Current.Shutdown();
                return false;
            }
            user = change.UpdatedUser;
        }

        AppServices.CurrentUser = user;
        AppServices.TouchSession();
        BindUserHeader();
        BuildNavigation();
        return true;
    }

    // ‏التبديلُ يُطلق ThemeManager.Changed فيُحدّث OnThemeChanged زرَّ الثيم ويحفظ الاختيارَ ويُعيد رسم الصفحة — مرّةً واحدة
    // ‏(كانت الصفحةُ تُبنى مرّتين لكلّ نقرة: هنا وفي المعالج).
    private void OnToggleTheme(object sender, RoutedEventArgs e) => ThemeManager.Toggle();

    /// <summary>
    /// نصّ زرّ الثيم باللغة الجارية (O-25). كان مثبّتاً بالعربية فيبقى «فاتح» في
    /// الوضع الإنجليزيّ — وهو ما رصده المسح الحيّ على الستّين صفحة كلّها.
    /// </summary>
    private void RefreshThemeToggleText()
    {
        ThemeToggleBtn.Content = ThemeManager.IsDark
            ? LocaleManager.T("فاتح", "Light")
            : LocaleManager.T("داكن", "Dark");
        ThemeToggleBtn.ToolTip = ThemeManager.IsDark
            ? LocaleManager.T("التبديل إلى الوضع الفاتح", "Switch to light mode")
            : LocaleManager.T("التبديل إلى الوضع الداكن", "Switch to dark mode");
    }

    private void OnThemeChanged() =>
        Dispatcher.Invoke(() =>
        {
            RefreshThemeToggleText();
            SaveUiPreferences();
            RefreshActivePage();
        });

    /// <summary>يحفظ المظهرَ واللغةَ الجاريين — أياً كان من غيّرهما (زرُّ الترويسة أو صفحةُ التفضيلات).</summary>
    private static void SaveUiPreferences() =>
        UiPreferencesStore.Save(new UiPreferences
        {
            Dark = ThemeManager.IsDark,
            English = LocaleManager.IsEnglish,
            Compact = DensityManager.IsCompact,
        });

    private void ApplyHeaderFit(double width)
    {
        var tier = HeaderFit.For(width);
        var labels = tier.ShowLabelsAndDate ? Visibility.Visible : Visibility.Collapsed;
        CompanyLabel.Visibility = labels;
        BranchLabel.Visibility = labels;
        DateText.Visibility = labels;
        HeaderUserBadge.Visibility = tier.ShowUserBadge ? Visibility.Visible : Visibility.Collapsed;
        CompanyCombo.Width = tier.CompanyWidth;
        BranchCombo.Width = tier.BranchWidth;
    }

    private void OnDensityChanged() =>
        Dispatcher.Invoke(() =>
        {
            SaveUiPreferences();
            RefreshActivePage();
        });

    private void RefreshActivePage()
    {
        var key = _activePageKey;
        var title = PageTitle.Text;
        Navigate(key, title);
        _ = RefreshAiPanelAsync();
    }

    private void OnToggleLanguage(object sender, RoutedEventArgs e)
    {
        LocaleManager.Toggle();
        LanguageToggleBtn.Content = LocaleManager.LanguageLabel;
        FlowDirection = LocaleManager.Flow;
        RefreshChromeText();

        // (O-25، CEEF-223) إعادة بناء التنقّل **إلزامية**: أزرار الشريط الجانبي تُنشأ
        // مرّة بعناوين `NavigationCatalog.PageTitleEn/GroupTitleEn`، فبلا إعادة بناء
        // تبقى بلغة الإقلاع. المقيس في المسح الحيّ: كل عناوين المجموعات وعدّة عناصر
        // بقيت عربية بعد التبديل — والكتالوج مترجَم 100%، لكنّه لم يكن يُستخدَم.
        // (ما ظهر إنجليزياً كان من مرور XamlUiLocalizer على الشجرة، لا من الكتالوج،
        //  فبقي ما لا مدخل له في سجلّ الترجمة عربياً — واجهة نصفان.)
        BuildNavigation();
        RefreshThemeToggleText();
        BindUserHeader();          // (O-27) التاريخ في الشريط العلويّ يتبع اللغة
        UpdateSessionStatus();
        _ = RefreshDbHealthAsync();

        // (O-26) لوحة المساعد: المقترحات والرؤى تُعاد باللغة الجديدة.
        // **المحادثة لا تُمسح عمداً**: نصّ الردّ محفوظ بلغته وقت إنشائه، وسجلّ
        // المحادثة لا يُعاد ترجمته بأثر رجعيّ — كما لا يفعل أيّ تطبيق محادثة.
        RenderAiQuickPrompts();
        if (AiColumn.Width.Value > 0)
            _ = RefreshAiPanelAsync();

        var key = _activePageKey;
        Navigate(key, NavTitleFor(key));
        SaveUiPreferences();
    }

    /// <summary>عنوان الصفحة باللغة الجارية من الكتالوج — لا من نصّ الشاشة الحاليّ.</summary>
    private static string NavTitleFor(string pageKey)
    {
        foreach (var group in NavigationCatalog.Groups)
        {
            foreach (var page in group.Pages)
            {
                if (page.Key == pageKey)
                    return LocaleManager.T(page.TitleAr, NavigationCatalog.PageTitleEn(page.Key));
            }
        }
        return pageKey;
    }

    private bool _aiPanelChosenByUser;

    private void OnToggleAiPanel(object sender, RoutedEventArgs e)
    {
        _aiPanelChosenByUser = true;
        SetAiPanelOpen(AiColumn.Width.Value <= 0);
    }

    private void ApplyAiPanelPolicy()
    {
        if (_aiPanelChosenByUser || ActualWidth <= 0)
            return;
        var open = Ui.ShellLayout.AiPanelFits(ActualWidth);
        if (open != AiColumn.Width.Value > 0)
            SetAiPanelOpen(open);
    }

    private void SetAiPanelOpen(bool open)
    {
        AiColumn.Width = open ? new GridLength(Ui.ShellLayout.AiPanelWidth) : new GridLength(0);
        AiPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (open)
            _ = RefreshAiPanelAsync();
    }

    private void OnLocaleChanged()
    {
        FlowDirection = LocaleManager.Flow;
        RefreshChromeText();
        if (PageHost.Content is DependencyObject page)
            XamlUiLocalizer.Apply(page);
        _ = RefreshAiPanelAsync();
    }

    private void RefreshChromeText()
    {
        AiSubtitleText.Text = LocaleManager.T("مساعد البرنامج الذكي", "Smart program copilot");
        AiInsightsLabel.Text = LocaleManager.T("رؤى فورية", "Live insights");
        AiChatLabel.Text = LocaleManager.T("المحادثة", "Conversation");
        AiInputBox.ToolTip = LocaleManager.T(
            "اسأل عن هذه الشاشة، الترحيل، ZATCA، VAT، الذمم...",
            "Ask about this screen, posting, ZATCA, VAT, AR/AP...");
        AiSendBtn.Content = LocaleManager.T("إرسال", "Send");
        NavSearchBox.Tag = LocaleManager.T("ابحث في القوائم…  Ctrl+K", "Search menu…  Ctrl+K");
        AutomationProperties.SetName(NavSearchBox, LocaleManager.T("بحث في القوائم", "Search menu"));
    }

    private void BuildAiPanelShell()
    {
        AiChatHost.Children.Clear();
        if (!_aiInputWired)
        {
            AiAssistantPanelBuilder.WireInput(this, AiInputBox, AiSendBtn, OnAiQuestionAsync);
            _aiInputWired = true;
        }
        RenderAiQuickPrompts();
        RefreshChromeText();
    }

    /// <summary>
    /// (O-26) رسم المقترحات السريعة. كان داخل حارس التوصيل <c>_aiInputWired</c> فيُرسم
    /// **مرّة واحدة** بلغة الإقلاع ويبقى — «كيف أرحّل؟» و«صلاحياتي» في واجهة إنجليزية
    /// على الستّين صفحة كلّها. فُصل عن التوصيل ليُعاد عند كل تبديل لغة.
    /// </summary>
    private void RenderAiQuickPrompts() =>
        AiAssistantPanelBuilder.RenderQuickPrompts(this, AiQuickPromptsHost, OnAiQuestionAsync);

    private async Task RefreshAiPanelAsync()
    {
        try
        {
            // ‏تحليلٌ مشتركٌ مع اللوحة (IntelligenceCache): فتحُ اللوحة والتنقّلُ المتتابع لا يحملان القاعدةَ بالتحليل نفسِه مرّتين.
            var report = await IntelligenceCache.AnalyzeAsync(AppServices.CompanyId);
            AiInsightHost.Children.Clear();
            var pageGuide = ProgramKnowledge.TryGetPageGuide(_activePageKey);
            if (pageGuide is not null)
            {
                AiInsightHost.Children.Add(new Border
                {
                    Style = UiResources.StyleOf(this, "AiInsightCard"),
                    Margin = new Thickness(0, 0, 0, 8),
                    Child = new TextBlock
                    {
                        Text = LocaleManager.T(
                            $"الشاشة: {pageGuide.TitleAr} — {pageGuide.GuideAr}",
                            $"Screen: {pageGuide.TitleEn} — {pageGuide.GuideEn}"),
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 12.5,
                        Foreground = UiResources.BrushOf(this, "TextPrimaryBrush"),
                    },
                });
            }

            foreach (var insight in report.Insights.Take(5))
                AiInsightHost.Children.Add(AiAssistantPanelBuilder.BuildInsightCard(this, insight));
        }
        catch
        {
            AiAssistantPanelBuilder.RenderStarterTips(this, AiInsightHost);
        }
    }

    private async Task OnAiQuestionAsync(string question)
    {
        AiAssistantPanelBuilder.AppendUserBubble(this, AiChatHost, question);
        try
        {
            using var scope = AppServices.CreateInfrastructureScope();
            var assistant = scope.ServiceProvider.GetRequiredService<IProgramAssistantService>();
            var ctx = new ProgramAssistantContext(
                AppServices.CompanyId,
                _activePageKey,
                RoleLabel(AppServices.CurrentUser?.Role),
                ProfileLabel(_setup.Profile));
            var reply = await assistant.AskAsync(ctx, question);
            AiAssistantPanelBuilder.AppendAssistantBubble(this, AiChatHost, reply);
        }
        catch (Exception ex)
        {
            var fallback = new FinancialAssistantReply(
                $"تعذّر الرد: {ex.Message}",
                $"Could not answer: {ex.Message}",
                ProgramAssistantService.EngineId);
            AiAssistantPanelBuilder.AppendAssistantBubble(this, AiChatHost, fallback);
        }
    }

    // (O-26) تسميتان مولَّدتان في الكود لا في XAML، فلم يمسّهما مسحُ التوطين —
    // تبقيان عربيَّتين في الشريط الجانبي على كل صفحة بالوضع الإنجليزيّ.
    private static string RoleLabel(UserRole? role) => role switch
    {
        UserRole.Administrator => LocaleManager.T("مدير النظام", "System administrator"),
        UserRole.Accountant => LocaleManager.T("محاسب", "Accountant"),
        UserRole.Cashier => LocaleManager.T("أمين صندوق", "Cashier"),
        UserRole.Viewer => LocaleManager.T("مشاهد", "Viewer"),
        _ => LocaleManager.T("مستخدم", "User"),
    };

    private static string ProfileLabel(BusinessProfile profile) => profile switch
    {
        BusinessProfile.Construction => LocaleManager.T("مقاولات عامة", "General contracting"),
        BusinessProfile.Manufacturing => LocaleManager.T("مصنع / إنتاج", "Manufacturing / production"),
        BusinessProfile.Retail => LocaleManager.T("محل / تسويق", "Retail / trading"),
        BusinessProfile.Healthcare => LocaleManager.T("قطاع صحي", "Healthcare"),
        BusinessProfile.Logistics => LocaleManager.T("لوجستيات ونقل", "Logistics and transport"),
        BusinessProfile.RealEstate => LocaleManager.T("عقارات وإيجارات", "Real estate and leasing"),
        BusinessProfile.Hospitality => LocaleManager.T("ضيافة ومطاعم", "Hospitality and restaurants"),
        _ => LocaleManager.T("محاسبة عامة", "General accounting"),
    };

    /// <summary>
    /// (دفتر الحبر والنحاس) مجموعةٌ في القائمة: رأسٌ يُطوى وجسمٌ يضمّ أزرارَها. العشرون مجموعةً كانت تُعرض كلُّها دفعةً واحدة
    /// (مئةُ بندٍ تقريباً)، فصار المفتوحُ منها مجموعةَ الصفحة الجارية وما يختاره المستخدم، والبحثُ يفتح كلَّ ما يطابق.
    /// </summary>
    private sealed class NavGroupView
    {
        public required string Key { get; init; }
        public required string TitleAr { get; init; }
        public required System.Windows.Controls.Primitives.ToggleButton Header { get; init; }
        public required StackPanel Body { get; init; }
        public required List<(NavPage Page, string Title, Button Button)> Items { get; init; }
    }

    private readonly List<NavGroupView> _navGroups = [];
    private readonly Dictionary<string, NavGroupView> _navGroupOfPage = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _openNavGroups = new(StringComparer.Ordinal);

    /// <summary>أدواتُ التدقيق الآليّ (FlaUI/pywinauto) تعثر على الأزرار بمعرّفها، والمطويُّ يغيب من شجرة الأتمتة — فيبقى كلُّه مفتوحاً.</summary>
    private static bool KeepNavOpenForAutomation =>
        string.Equals(Environment.GetEnvironmentVariable("BRQOSH_UITEST"), "1", StringComparison.Ordinal);

    private void BuildNavigation()
    {
        NavPanel.Children.Clear();
        _navButtons.Clear();
        _navGroups.Clear();
        _navGroupOfPage.Clear();
        var menuPages = new Dictionary<string, NavPage>(StringComparer.Ordinal);
        foreach (var group in NavigationCatalog.ForProfile(_setup.Profile, _licensedModules))
        {
            var allowedPages = group.Pages
                .Where(page => PermissionService.CanAccess(AppServices.CurrentUser, page.Key))
                .ToList();
            // (طلبُ المالك 2026-10-26) صفحاتُ الإعدادات والدعم تنتقل من القائمة الجانبيّة إلى قائمة المستخدم — بالصلاحيّة والترخيص نفسَيهما.
            foreach (var page in allowedPages.Where(p => NavigationCatalog.IsInUserMenu(p.Key)))
                menuPages[page.Key] = page;
            var visiblePages = allowedPages.Where(p => !NavigationCatalog.IsInUserMenu(p.Key)).ToList();
            if (visiblePages.Count == 0)
                continue;

            var header = new System.Windows.Controls.Primitives.ToggleButton
            {
                Style = (Style)FindResource("NavGroupToggle"),
                Content = LocaleManager.T(group.TitleAr, NavigationCatalog.GroupTitleEn(group.TitleAr)).ToUpperInvariant(),
            };
            AutomationProperties.SetName(header, LocaleManager.T(group.TitleAr, NavigationCatalog.GroupTitleEn(group.TitleAr)));
            AutomationProperties.SetAutomationId(header, $"NavGroup_{group.Module}_{_navGroups.Count}");
            var body = new StackPanel();
            var items = new List<(NavPage, string, Button)>();
            var view = new NavGroupView { Key = group.TitleAr, TitleAr = group.TitleAr, Header = header, Body = body, Items = items };

            foreach (var page in visiblePages)
            {
                var localizedTitle = LocaleManager.T(page.TitleAr, NavigationCatalog.PageTitleEn(page.Key));
                var button = new Button
                {
                    Style = (Style)FindResource("NavItem"),
                    Content = NavIconFactory.BuildContent(this, page.Key, localizedTitle),
                    Tag = page,
                };
                AutomationProperties.SetName(button, localizedTitle);
                AutomationProperties.SetAutomationId(button, $"Nav_{page.Key}");
                button.Click += (_, _) => Navigate(page.Key, localizedTitle);
                body.Children.Add(button);
                items.Add((page, localizedTitle, button));
                _navButtons[page.Key] = button;
                _navGroupOfPage[page.Key] = view;
            }

            header.Click += (_, _) =>
            {
                if (header.IsChecked == true) _openNavGroups.Add(view.Key); else _openNavGroups.Remove(view.Key);
                ApplyNavFilter();
            };
            NavPanel.Children.Add(header);
            NavPanel.Children.Add(body);
            _navGroups.Add(view);
        }

        BuildUserMenuModel(menuPages);

        // ‏أوّلُ بناء: تُفتح مجموعةُ الصفحة الجارية (واللوحةُ التنفيذيّة أوّلاً) فلا تبدأ القائمةُ مطويّةً كلُّها.
        if (_openNavGroups.Count == 0)
        {
            if (_navGroupOfPage.TryGetValue(_activePageKey, out var active))
                _openNavGroups.Add(active.Key);
            if (_navGroups.Count > 0)
                _openNavGroups.Add(_navGroups[0].Key);
        }

        ApplyNavFilter();
    }

    /// <summary>يطبّق حالةَ الطيّ ونصَّ البحث معاً: البحثُ يفتح المطابقَ ويخفي ما سواه، وفراغُه يعيد حالةَ الطيّ.</summary>
    private const string SettingsGroupKey = "__settings";

    private readonly List<(SettingsSection Section, List<(NavPage Page, string Title)> Items)> _settingsMenu = [];
    private (NavPage Page, string Title)? _supportMenuItem;

    /// <summary>
    /// نموذجُ قائمة المستخدم من الصفحات المسموحة: أقسامُ الإعدادات بترتيب الكتالوج، ثمّ «الدعم والشكاوى». ولأنّ هذه الصفحاتِ خرجت من القائمة الجانبيّة فتُبنى لها
    /// مجموعةٌ مخفيّةٌ تظهر <b>وقتَ البحث وحده</b> (Ctrl+K) فلا يضيع الوصولُ إليها بالكتابة.
    /// </summary>
    private void BuildUserMenuModel(Dictionary<string, NavPage> menuPages)
    {
        _settingsMenu.Clear();
        _supportMenuItem = null;

        string TitleOf(NavPage page) => LocaleManager.T(page.TitleAr, NavigationCatalog.PageTitleEn(page.Key));

        foreach (var section in NavigationCatalog.SettingsSections)
        {
            var items = section.Keys
                .Where(menuPages.ContainsKey)
                .Select(k => (Page: menuPages[k], Title: TitleOf(menuPages[k])))
                .ToList();
            if (items.Count > 0)
                _settingsMenu.Add((section, items));
        }

        if (menuPages.TryGetValue(NavigationCatalog.SupportKey, out var support))
            _supportMenuItem = (support, TitleOf(support));

        var searchable = _settingsMenu.SelectMany(m => m.Items).ToList();
        if (_supportMenuItem is { } sup)
            searchable.Add(sup);
        if (searchable.Count == 0)
            return;

        var groupTitle = LocaleManager.T("الإعدادات", NavigationCatalog.GroupTitleEn("الإعدادات"));
        var header = new System.Windows.Controls.Primitives.ToggleButton
        {
            Style = (Style)FindResource("NavGroupToggle"),
            Content = groupTitle.ToUpperInvariant(),
            Visibility = Visibility.Collapsed,
        };
        AutomationProperties.SetName(header, groupTitle);
        var body = new StackPanel { Visibility = Visibility.Collapsed };
        var view = new NavGroupView
        {
            Key = SettingsGroupKey,
            TitleAr = "الإعدادات",
            Header = header,
            Body = body,
            Items = [],
        };
        foreach (var (page, title) in searchable)
        {
            var button = new Button
            {
                Style = (Style)FindResource("NavItem"),
                Content = NavIconFactory.BuildContent(this, page.Key, title),
                Tag = page,
                Visibility = Visibility.Collapsed,
            };
            AutomationProperties.SetName(button, title);
            AutomationProperties.SetAutomationId(button, $"Nav_{page.Key}");
            var captured = title;
            var key = page.Key;
            button.Click += (_, _) => { NavSearchBox.Clear(); Navigate(key, captured); };
            body.Children.Add(button);
            view.Items.Add((page, title, button));
            _navButtons[page.Key] = button;
            _navGroupOfPage[page.Key] = view;
        }

        NavPanel.Children.Add(header);
        NavPanel.Children.Add(body);
        _navGroups.Add(view);
    }

    private void OnUserCardClick(object sender, RoutedEventArgs e) =>
        OpenUserMenu(UserCardButton, System.Windows.Controls.Primitives.PlacementMode.Top);

    private void OnHeaderUserBadgeClick(object sender, MouseButtonEventArgs e)
    {
        OpenUserMenu(HeaderUserBadge, System.Windows.Controls.Primitives.PlacementMode.Bottom);
        e.Handled = true;
    }

    /// <summary>قائمةُ المستخدم: «الإعدادات» في خانةٍ واحدةٍ بأقسامها، ثمّ «الدعم والشكاوى». تُبنى عند الفتح فتتبع اللغةَ والصلاحيّةَ الجاريتين.</summary>
    internal ContextMenu BuildUserMenu()
    {
        var menu = new ContextMenu { MinWidth = 260, FlowDirection = FlowDirection };
        if (_settingsMenu.Count > 0)
        {
            menu.Items.Add(new MenuItem
            {
                Header = LocaleManager.T("الإعدادات", NavigationCatalog.GroupTitleEn("الإعدادات")),
                Style = (Style)FindResource("UserMenuTitle"),
            });
            foreach (var (section, items) in _settingsMenu)
            {
                menu.Items.Add(new MenuItem
                {
                    Header = LocaleManager.T(section.TitleAr, section.TitleEn),
                    Style = (Style)FindResource("UserMenuSectionHeader"),
                });
                foreach (var (page, title) in items)
                    menu.Items.Add(MenuEntry(page, title));
            }
        }

        if (_supportMenuItem is { } support)
        {
            if (menu.Items.Count > 0)
                menu.Items.Add(new Separator());
            menu.Items.Add(MenuEntry(support.Page, support.Title));
        }

        return menu;
    }

    private MenuItem MenuEntry(NavPage page, string title)
    {
        var item = new MenuItem { Header = title, Tag = page.Key };
        AutomationProperties.SetAutomationId(item, $"UserMenu_{page.Key}");
        var key = page.Key;
        item.Click += (_, _) => Navigate(key, title);
        return item;
    }

    private void OpenUserMenu(FrameworkElement target, System.Windows.Controls.Primitives.PlacementMode placement)
    {
        var menu = BuildUserMenu();
        if (menu.Items.Count == 0)
            return;
        menu.PlacementTarget = target;
        menu.Placement = placement;
        menu.IsOpen = true;
    }

    private void ApplyNavFilter()
    {
        var query = NavSearchBox.Text.Trim();
        var searching = query.Length > 0;
        foreach (var group in _navGroups)
        {
            var matches = 0;
            foreach (var (page, title, button) in group.Items)
            {
                var hit = !searching
                    || title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || page.TitleAr.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || NavigationCatalog.PageTitleEn(page.Key).Contains(query, StringComparison.OrdinalIgnoreCase);
                button.Visibility = hit ? Visibility.Visible : Visibility.Collapsed;
                if (hit) matches++;
            }

            var isSettings = group.Key == SettingsGroupKey;
            var open = isSettings
                ? searching || KeepNavOpenForAutomation
                : searching || KeepNavOpenForAutomation || _openNavGroups.Contains(group.Key);
            group.Header.IsChecked = open;
            group.Header.Visibility = matches > 0 ? Visibility.Visible : Visibility.Collapsed;
            group.Header.IsEnabled = !searching;
            group.Body.Visibility = matches > 0 && open ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnNavSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded && _navGroups.Count == 0)
            return;
        ApplyNavFilter();
    }

    private void OnNavSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                NavSearchBox.Clear();
                e.Handled = true;
                break;
            case Key.Enter:
                // ‏Enter يفتح أوّل مطابقٍ ظاهر — مسارٌ بلا فأرة من Ctrl+K إلى الشاشة.
                var first = _navGroups
                    .SelectMany(g => g.Items)
                    .FirstOrDefault(i => i.Button.Visibility == Visibility.Visible);
                if (first.Button is not null)
                {
                    NavSearchBox.Clear();
                    Navigate(first.Page.Key, first.Title);
                }
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// اختصاراتُ الصفحة الجارية (Ctrl+N إضافة · Ctrl+F/F3 بحث · F5 إعادة تحميل) — تُستثنى حالةُ كتابةٍ داخل حقلٍ لا يخصّها
    /// (F5 وCtrl+N لا يتعارضان مع التحرير)؛ والمنطقُ في <see cref="PageShortcuts"/> ليُقاس بلا نافذة.
    /// </summary>
    private bool HandlePageShortcut(KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        if (PageHost.Content is not DependencyObject page)
            return false;

        if (ctrl && e.Key == Key.N)
        {
            var add = PageShortcuts.FindAddButton(page);
            if (add is null)
                return false;
            add.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            return true;
        }

        if ((ctrl && e.Key == Key.F) || (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F3))
        {
            var box = PageShortcuts.FindSearchBox(page);
            if (box is null)
                return false;
            box.Focus();
            box.SelectAll();
            return true;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F5)
        {
            Navigate(_activePageKey, NavTitleFor(_activePageKey));
            return true;
        }

        return false;
    }

    private void FocusNavSearch()
    {
        NavSearchBox.Focus();
        NavSearchBox.SelectAll();
    }

    private void SetActiveNav(string pageKey)
    {
        if (_navButtons.TryGetValue(_activePageKey, out var prev))
            prev.Style = (Style)FindResource("NavItem");

        _activePageKey = pageKey;

        if (_navButtons.TryGetValue(pageKey, out var current))
            current.Style = (Style)FindResource("NavItemActive");

        // ‏فتحُ شاشةٍ من خارج القائمة (روابط اللوحة) يبسط مجموعتَها فيرى المستخدم أين هو.
        if (_navGroupOfPage.TryGetValue(pageKey, out var group) && _openNavGroups.Add(group.Key))
            ApplyNavFilter();
    }

    /// <summary>مسار التنقل في الترويسة: «المنشأة › المجموعة» بدل النصّ الثابت «الصفحة الحالية».</summary>
    private void UpdateBreadcrumb(string pageKey)
    {
        var company = string.IsNullOrWhiteSpace(CompanyNameText.Text) ? "BRQOSH" : CompanyNameText.Text;
        PageCrumb.Text = _navGroupOfPage.TryGetValue(pageKey, out var group)
            ? $"{company}  ›  {LocaleManager.T(group.TitleAr, NavigationCatalog.GroupTitleEn(group.TitleAr))}"
            : company;
    }

    private void Navigate(string pageKey, string title)
    {
        if (AppServices.IsSessionExpired())
        {
            if (!EnforceReauthentication())
                return;
        }

        AppServices.TouchSession();

        if (!PermissionService.CanAccess(AppServices.CurrentUser, pageKey))
        {
            UiDialog.Show(this, PermissionService.DeniedMessage(title), "BRQOSH",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // ‏(ح-١٦٤) ‏والترخيصُ يحجب هنا لا في القائمة وحدَها — فالشاشةُ تُفتح بمفتاحها من روابط اللوحة والشاشات.
        if (NavigationCatalog.IsWithheldByLicense(pageKey, _setup.Profile, _licensedModules))
        {
            UiDialog.Show(this, "هذه الوحدة ليست ضمن ترخيصك. لإضافتها تواصل مع المورّد لإصدار ترخيصٍ يشملها.", "BRQOSH",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        PageTitle.Text = title;
        UpdateBreadcrumb(pageKey);
        SetActiveNav(pageKey);
        if (AiColumn.Width.Value > 0)
            _ = RefreshAiPanelAsync();
        // ‏(CEEF-230 · ط6-١) **الربطُ في `PageFactory` لا هنا** — فما داخلَ نافذةٍ لا
        // ‏يُقاس. وشاشتان تحتاجان سياقَ النافذة فتبقيان، **ومُعلَنتان في
        // ‏`PageFactory.WindowScopedKeys`** فلا يُقرأ غيابُهما من المصنع نقصاً.
        switch (pageKey)
        {
            case "company_settings":
                PageHost.Content = new Views.Pages.CompanySettingsPage(_setup, this);
                Motion.FadeIn(PageHost);
                return;
            case "organizations":
                PageHost.Content = new Views.Pages.OrganizationsPage(this);
                Motion.FadeIn(PageHost);
                return;
        }

        var page = Navigation.PageFactory.Create(pageKey);
        if (page is not null)
        {
            PageHost.Content = page;
            Motion.FadeIn(PageHost);
            return;
        }

        ShowPlaceholder(pageKey, title);
    }


    private void ShowPlaceholder(string pageKey, string title)
    {
        var badge = new Border
        {
            Style = (Style)FindResource("KpiMonogramBadge"),
            Background = (System.Windows.Media.Brush)FindResource("PrimarySoftBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                Text = "PB",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryDarkBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        PageHost.Content = new Border
        {
            Style = (Style)FindResource("EmptyStatePanel"),
            Child = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    badge,
                    new TextBlock
                    {
                        Text = title,
                        Style = (Style)FindResource("EmptyStateTitle"),
                        Margin = new Thickness(0, 16, 0, 0),
                    },
                    new TextBlock
                    {
                        Text = $"هذه الشاشة قيد البناء ({pageKey}) — ستتوفر في المرحلة القادمة.",
                        Style = (Style)FindResource("EmptyStateSubtitle"),
                        Margin = new Thickness(0, 10, 0, 0),
                    },
                },
            },
        };
    }

    /// <summary>مجلّدُ النسخ — هو نفسُه الذي تقرأه شاشةُ النسخ وتكتب فيه.</summary>
    /// <summary>(ح-١٢٧) يُدخل الجلسةَ وضعَ القراءة إن بلغ الوقتُ الفعّالُ انتهاءَ الترخيص — مرّةً، ويقول ذلك في العنوان وحوار.</summary>
    private void EnterReadOnlyIfLicenseExpired(DateTime? licenseExpiresUtc)
    {
        if (!Brqosh.Infrastructure.Licensing.LicenseSessionGuard.ShouldEnterReadOnly(
                licenseExpiresUtc, Brqosh.Infrastructure.Licensing.LicenseClock.EffectiveNowUtc(),
                Brqosh.Infrastructure.Licensing.ReadOnlyBooks.IsActive))
            return;

        Brqosh.Infrastructure.Licensing.ReadOnlyBooks.Enter();
        Title += " — " + LocaleManager.T("للقراءة فقط (الترخيص منتهٍ)", "Read-only (licence expired)");
        UiDialog.Show(
            "انتهت صلاحية الترخيص أثناء الجلسة — الدفاتر الآن للقراءة فقط: تُعرض وتُطبع وتُصدَّر، ولا يُضاف إليها شيء حتى يُجدَّد الترخيص.",
            "BRQOSH — الترخيص منتهٍ", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static string BackupFolder => Path.Combine(BrqoshDataRoot.Path(), "backups");

    /// <summary>
    /// (CEEF-237 · ت-٤) ينسخ إن حان الوقت، ويُحدّث الشارة. <b>ولا يرمي أبداً</b>:
    /// مؤقّتٌ يرمي في WPF يُسقط التطبيق، <b>ونسخةٌ فاشلة أهونُ من دفترٍ يُغلق</b>.
    /// </summary>
    /// <summary>
    /// (ح-١٤١) فحصُ القناة المحفوظة بعد الإقلاع — <b>إخبارٌ لا تنزيل</b>.
    /// </summary>
    /// <remarks>
    /// «لا قناة» (انقطاعُ شبكةٍ أو مشاركةٍ غائبة) يمرّ صامتاً — منشأةٌ خلف جدارٍ ناريٍّ تراه كلَّ يوم؛
    /// <b>أمّا البيانُ غيرُ الموثوق فيُقال ويُسجَّل</b>: هو ما يجب أن يراه المالك.
    /// </remarks>
    private async Task CheckForUpdateAtStartupAsync()
    {
        var source = UpdateChannelSettings.Load();
        if (source is null) return;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20));
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<UpdateCheckService>().CheckAsync(source);

            if (result.Status == UpdateStatus.UpdateAvailable)
            {
                UiFeedback.ShowInfo(
                    LocaleManager.T("يوجد إصدارٌ أحدث من برقوش: ", "A newer BRQOSH version is available: ")
                    + result.AvailableVersion
                    + LocaleManager.T(" — نزّله من «حالة النظام».", " — download it from System status."));
            }
            else if (result.Status == UpdateStatus.Untrusted)
            {
                CrashLog.Write(new InvalidOperationException(result.Failure), "update-channel-untrusted");
                UiFeedback.ShowWarning(LocaleManager.T(
                    "رُفض بيانُ التحديث في القناة المحفوظة — التفصيلُ في «حالة النظام».",
                    "The update manifest in the saved channel was refused — details in System status."));
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "update-check");
        }
    }

    private readonly Brqosh.Infrastructure.Zatca.ZatcaRetryBackoff _zatcaBackoff = new();
    private bool _zatcaRunning;

    private async Task RunZatcaResubmitAsync()
    {
        if (_zatcaRunning || !_zatcaBackoff.IsDue(DateTime.UtcNow)) return;
        _zatcaRunning = true;
        try
        {
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<Brqosh.Infrastructure.Zatca.ZatcaSubmissionService>()
                .ResubmitPendingInBackgroundAsync(AppServices.CompanyId);
            if (result.StillPending > 0)
            {
                // ‏السببُ مسجَّلٌ على المستند في الجدول، ويُكتب هنا أيضاً ليبلغ الدعمَ — والمحاولةُ التالية بعد مهلةٍ أطول.
                _zatcaBackoff.RecordFailure(DateTime.UtcNow);
                CrashLog.Write(new InvalidOperationException(
                    $"إعادةُ إرسال المعلَّق إلى الهيئة وقفت: أُرسل {result.Submitted} وبقي {result.StillPending} — {result.LastError}"),
                    "zatca-resubmit-pending");
            }
            else
            {
                _zatcaBackoff.RecordSuccess(DateTime.UtcNow);
            }
        }
        catch (Exception ex)
        {
            _zatcaBackoff.RecordFailure(DateTime.UtcNow);
            CrashLog.Write(ex, "zatca-resubmit");
        }
        finally { _zatcaRunning = false; }
    }

    private async Task RunScheduledBackupAsync()
    {
        if (_backupRunning) return;   // ‏تشغيلةٌ طويلةٌ لا تُستدعى فوق نفسها.
        _backupRunning = true;
        try
        {
            using var facade = new DesktopAccountingFacade();
            var isPg = facade.Db.Database.IsNpgsql();
            var outcome = await facade.GetService<BackupScheduleService>()
                .RunIfDueAsync(BackupFolder, isPg);

            // ‏(ن-٤١) ‏**السببُ يُقرأ — وكان يُحسب ويُعاد ويُرمى.**
            //
            // ‏`BackupScheduleService` يَعِد في تعليقه: «الفشلُ لا يُبتلع… **لكنّ الإخفاق
            // ‏يُعاد ليُعرض لا ليُنسى**». وكان هذا السطرُ يقرأ `outcome.State` وحدَه —
            // ‏**والمستدعي الوحيد**. فيُحسب السببُ ويُعاد ويُسقَط، والمالكُ يرى شارةَ
            // ‏«تأخّر النسخ» **ولا يعرف لماذا**: أقرصٌ ممتلئ؟ أم `pg_dump` مفقود؟ أم
            // ‏صلاحيّةُ مجلّد؟ — والفرقُ بينها هو الفرقُ بين إصلاحٍ في دقيقةٍ وبلاغِ دعم.
            if (outcome.Failure is { Length: > 0 } failure)
                CrashLog.Write(new InvalidOperationException(failure), "scheduled-backup-failed");

            // ‏(م-٤ · ن-٣٦) ‏**وحذفُ نسخٍ قديمةٍ يُسجَّل** — فمُؤقِّتٌ يحذف ملفّاتِ نسخٍ
            // ‏احتياطيّةٍ بلا أثرٍ في أيّ مكان هو عينُ شكلِ ن-٤١ الذي أُغلق لتوّه.
            // ‏و`CrashLog` هو السجلُّ الدائمُ في هذا المنتَج، ويُستعمل للإخطار لا للأعطال
            // ‏وحدَها (‏سابقة: `clock-rollback`). وحصّةُ السياق (ن-٣٣) تمنع إغراقَه.
            if (outcome.Pruned > 0 || outcome.PruneFailures > 0)
                CrashLog.Write(
                    new InvalidOperationException(
                        $"تنظيفُ النسخ المحلّيّة: حُذف {outcome.Pruned}"
                        + (outcome.PruneFailures > 0
                            ? $"، وتعذّر حذفُ {outcome.PruneFailures} — القرصُ قد يمتلئ رغم الحدّ."
                            : ".")),
                    "scheduled-backup-pruned");

            ShowBackupBadge(outcome.State, outcome.Failure);
        }
        catch (Exception ex)
        {
            // ‏قاعدةٌ مقطوعةٌ أو إعدادٌ ناقص: تُعرض الحالةُ من الملفّات وحدَها.
            CrashLog.Write(ex, "scheduled-backup");
            // ‏وحتى لو تعذّرت قراءةُ الحالة، لا يُبتلع الفشل: تُعرض الشارةُ بأشدّ حالاتها.
            try
            {
                ShowBackupBadge(BackupScheduleService.ReadState(BackupFolder));
            }
            catch (Exception badgeEx) when (badgeEx is IOException or UnauthorizedAccessException
                                                or ArgumentException or NotSupportedException)
            {
                CrashLog.Write(badgeEx, "backup-badge");
                ShowBackupBadge(new BackupScheduleState(null, null, IsDue: true, IsOverdue: true));
            }
        }
        finally { _backupRunning = false; }
    }

    /// <param name="failure">
    /// (ن-٤١) سببُ إخفاقِ آخرِ محاولةٍ — <b>يُعرض تلميحاً وتُشعَل الشارةُ له</b>.
    ///
    /// <para><b>وإشعالُ الشارة شرطٌ لا زينة:</b> محاولةٌ تُخفق اليوم لا تجعل النسخَ
    /// «متأخّراً» فوراً — فآخرُ نسخةٍ ناجحةٍ قد تكون أمس. <b>فبلا هذا يبقى الإخفاقُ
    /// مخفيّاً حتى يمرّ حدُّ التأخّر</b>، وتكون المنشأةُ بلا نسخةٍ أيّاماً وهي تظنّ
    /// نفسَها مجدولة.</para>
    /// </param>
    private void ShowBackupBadge(BackupScheduleState state, string? failure = null)
    {
        StatusBackupText.Text = state.Badge;

        var failed = failure is { Length: > 0 };
        StatusBackupBadge.Visibility = state.IsOverdue || failed
            ? Visibility.Visible
            : Visibility.Collapsed;

        StatusBackupBadge.ToolTip = failed
            ? $"‏أخفقت آخرُ محاولةِ نسخٍ احتياطيّ: {failure}"
            : null;
    }
}
