using System.Windows;
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
    private string _activePageKey = "dashboard";
    private bool _branchBinding;
    private bool _companyBinding;
    private bool _aiInputWired;

    private sealed record BranchOption(long Id, string Display, bool IsHeadquarters);
    private sealed record CompanyOption(long Id, string Display, BusinessProfile Profile);

    public MainWindow(SetupState setup)
    {
        _setup = setup;
        InitializeComponent();
        ThemeManager.Apply(true);
        ThemeToggleBtn.Content = "فاتح";
        ThemeToggleBtn.ToolTip = "التبديل إلى الوضع الفاتح";
        KeyboardNavigationHelper.ApplyShellHeader(BranchCombo, ThemeToggleBtn);
        LocaleManager.Changed += OnLocaleChanged;
        ThemeManager.Changed += OnThemeChanged;
        BuildAiPanelShell();
        _ = RefreshAiPanelAsync();
        CompanyNameText.Text = setup.CompanyName;
        ProfileBadge.Text = ProfileLabel(setup.Profile);

        PreviewMouseMove += (_, _) => AppServices.TouchSession();
        PreviewKeyDown += (_, _) => AppServices.TouchSession();
        PreviewMouseDown += (_, _) => AppServices.TouchSession();

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _idleTimer.Tick += (_, _) => CheckGlobalIdle();
        _idleTimer.Start();
        Closed += (_, _) => _idleTimer.Stop();

        BindUserHeader();
        BindStatusBar();
        _ = RefreshDbHealthAsync();
        _ = BindCompaniesAsync();
        BuildNavigation();
        UiFeedback.AttachToastHost(ToastOverlay);
        Navigate("dashboard", "لوحة التحكم");
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
        BuildNavigation();
        Navigate(_activePageKey, PageTitle.Text);
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
        BuildNavigation();
        Navigate("dashboard", "لوحة التحكم");
    }

    private async Task ApplyCompanyContextAsync(CompanyOption company, bool reloadBranches)
    {
        AppServices.CompanyId = company.Id;
        AppServices.TouchSession();
        _setup = _setup with
        {
            Profile = company.Profile,
            CompanyName = company.Display,
        };
        CompanyNameText.Text = company.Display;
        ProfileBadge.Text = ProfileLabel(company.Profile);
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
        DateText.Text = DateTime.Now.ToString("dddd، d MMMM yyyy", new System.Globalization.CultureInfo("ar-SA"));
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
            StatusDbText.Text = ok
                ? $"قاعدة البيانات: {AppServices.ActiveProviderLabel} · متصل"
                : $"قاعدة البيانات: {AppServices.ActiveProviderLabel} · غير متصل";
        }
        catch
        {
            StatusDbDot.Background = (System.Windows.Media.Brush)FindResource("DangerBrush");
            StatusDbText.Text = $"قاعدة البيانات: {AppServices.ActiveProviderLabel} · خطأ اتصال";
        }
    }

    private void UpdateSessionStatus()
    {
        var user = AppServices.CurrentUser?.Username;
        var branch = AppServices.CurrentBranchLabel;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(user))
            parts.Add($"جلسة · {user}");
        else
            parts.Add("الجلسة نشطة");
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
            MessageBox.Show(this, message, "BRQOSH", MessageBoxButton.OK, MessageBoxImage.Information);

        var login = new LoginWindow { Owner = this };
        if (login.ShowDialog() != true || login.AuthenticatedUser is null)
        {
            MessageBox.Show(this, "انتهت الجلسة ولم يُعاد تسجيل الدخول — سيتم إغلاق البرنامج.",
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
                MessageBox.Show(this, "يجب تغيير كلمة المرور قبل المتابعة — سيتم إغلاق البرنامج.",
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

    private void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
        ThemeToggleBtn.Content = ThemeManager.IsDark ? "فاتح" : "داكن";
        ThemeToggleBtn.ToolTip = ThemeManager.IsDark ? "التبديل إلى الوضع الفاتح" : "التبديل إلى الوضع الداكن";
        RefreshActivePage();
    }

    private void OnThemeChanged() =>
        Dispatcher.Invoke(RefreshActivePage);

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
        var key = _activePageKey;
        var title = PageTitle.Text;
        Navigate(key, title);
    }

    private void OnToggleAiPanel(object sender, RoutedEventArgs e)
    {
        AiColumn.Width = AiColumn.Width.Value > 0 ? new GridLength(0) : new GridLength(300);
        AiPanel.Visibility = AiColumn.Width.Value > 0 ? Visibility.Visible : Visibility.Collapsed;
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
    }

    private void BuildAiPanelShell()
    {
        AiChatHost.Children.Clear();
        if (!_aiInputWired)
        {
            AiAssistantPanelBuilder.WireInput(this, AiInputBox, AiSendBtn, OnAiQuestionAsync);
            AiAssistantPanelBuilder.RenderQuickPrompts(this, AiQuickPromptsHost, OnAiQuestionAsync);
            _aiInputWired = true;
        }
        RefreshChromeText();
    }

    private async Task RefreshAiPanelAsync()
    {
        try
        {
            using var scope = AppServices.CreateInfrastructureScope();
            var intel = scope.ServiceProvider.GetRequiredService<IFinancialIntelligenceService>();
            var report = await intel.AnalyzeAsync(AppServices.CompanyId);
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

    private static string RoleLabel(UserRole? role) => role switch
    {
        UserRole.Administrator => "مدير النظام",
        UserRole.Accountant => "محاسب",
        UserRole.Cashier => "أمين صندوق",
        UserRole.Viewer => "مشاهد",
        _ => "مستخدم",
    };

    private static string ProfileLabel(BusinessProfile profile) => profile switch
    {
        BusinessProfile.Construction => "مقاولات عامة",
        BusinessProfile.Manufacturing => "مصنع / إنتاج",
        BusinessProfile.Retail => "محل / تسويق",
        BusinessProfile.Healthcare => "قطاع صحي",
        BusinessProfile.Logistics => "لوجستيات ونقل",
        BusinessProfile.RealEstate => "عقارات وإيجارات",
        BusinessProfile.Hospitality => "ضيافة ومطاعم",
        _ => "محاسبة عامة",
    };

    private void BuildNavigation()
    {
        NavPanel.Children.Clear();
        _navButtons.Clear();
        foreach (var group in NavigationCatalog.ForProfile(_setup.Profile))
        {
            var visiblePages = group.Pages
                .Where(page => PermissionService.CanAccess(AppServices.CurrentUser, page.Key))
                .ToList();
            if (visiblePages.Count == 0)
                continue;

            NavPanel.Children.Add(new TextBlock
            {
                Text = LocaleManager.T(group.TitleAr, NavigationCatalog.GroupTitleEn(group.TitleAr)).ToUpperInvariant(),
                Style = (Style)FindResource("NavGroupHeader"),
            });

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
                NavPanel.Children.Add(button);
                _navButtons[page.Key] = button;
            }
        }
    }

    private void SetActiveNav(string pageKey)
    {
        if (_navButtons.TryGetValue(_activePageKey, out var prev))
            prev.Style = (Style)FindResource("NavItem");

        _activePageKey = pageKey;

        if (_navButtons.TryGetValue(pageKey, out var current))
            current.Style = (Style)FindResource("NavItemActive");
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
            MessageBox.Show(this, PermissionService.DeniedMessage(title), "BRQOSH",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PageTitle.Text = title;
        SetActiveNav(pageKey);
        if (AiColumn.Width.Value > 0)
            _ = RefreshAiPanelAsync();
        switch (pageKey)
        {
            case "dashboard":
                PageHost.Content = new Views.Pages.DashboardPage();
                return;
            case "chart_of_accounts":
                PageHost.Content = new Views.Pages.ChartOfAccountsPage();
                return;
            case "journal_entries":
                PageHost.Content = new Views.Pages.JournalEntriesPage();
                return;
            case "trial_balance":
                PageHost.Content = new Views.Pages.TrialBalancePage();
                return;
            case "customers":
                PageHost.Content = new Views.Pages.CustomersPage();
                return;
            case "customer_statement":
                PageHost.Content = new Views.Pages.CustomerStatementPage();
                return;
            case "sales_quotations":
                PageHost.Content = new Views.Pages.SalesQuotationsPage();
                return;
            case "sales_orders":
                PageHost.Content = new Views.Pages.SalesOrdersPage();
                return;
            case "sales_invoices":
                PageHost.Content = new Views.Pages.SalesInvoicesPage();
                return;
            case "fiscal_periods":
                PageHost.Content = new Views.Pages.FiscalPeriodsPage();
                return;
            case "sales_returns":
                PageHost.Content = new Views.Pages.CreditNotesPage();
                return;
            case "crm_leads":
                PageHost.Content = new Views.Pages.CrmLeadsPage();
                return;
            case "crm_opportunities":
                PageHost.Content = new Views.Pages.CrmOpportunitiesPage();
                return;
            case "crm_activities":
                PageHost.Content = new Views.Pages.CrmActivitiesPage();
                return;
            case "receipt_vouchers":
                PageHost.Content = new Views.Pages.ReceiptVouchersPage();
                return;
            case "payment_vouchers":
                PageHost.Content = new Views.Pages.PaymentVouchersPage();
                return;
            case "suppliers":
                PageHost.Content = new Views.Pages.SuppliersPage();
                return;
            case "supplier_statement":
                PageHost.Content = new Views.Pages.SupplierStatementPage();
                return;
            case "purchase_requests":
                PageHost.Content = new Views.Pages.PurchaseRequestsPage();
                return;
            case "purchase_orders":
                PageHost.Content = new Views.Pages.PurchaseOrdersPage();
                return;
            case "goods_receipts":
                PageHost.Content = new Views.Pages.GoodsReceiptsPage();
                return;
            case "purchase_invoices":
                PageHost.Content = new Views.Pages.PurchaseInvoicesPage();
                return;
            case "debit_notes":
                PageHost.Content = new Views.Pages.DebitNotesPage();
                return;
            case "products":
                PageHost.Content = new Views.Pages.ProductsPage();
                return;
            case "warehouses":
                PageHost.Content = new Views.Pages.WarehousesPage();
                return;
            case "stock_movements":
                PageHost.Content = new Views.Pages.StockMovementsPage();
                return;
            case "stock_count":
                PageHost.Content = new Views.Pages.StockCountPage();
                return;
            case "landed_cost":
                PageHost.Content = new Views.Pages.LandedCostPage();
                return;
            case "bank_accounts":
                PageHost.Content = new Views.Pages.BankAccountsPage();
                return;
            case "bank_transfers":
                PageHost.Content = new Views.Pages.BankTransfersPage();
                return;
            case "exchange_rates":
                PageHost.Content = new Views.Pages.ExchangeRatesPage();
                return;
            case "cheques":
                PageHost.Content = new Views.Pages.ChequesPage();
                return;
            case "bank_reconciliation":
                PageHost.Content = new Views.Pages.BankReconciliationPage();
                return;
            case "cost_centers":
                PageHost.Content = new Views.Pages.CostCentersPage();
                return;
            case "fixed_assets":
                PageHost.Content = new Views.Pages.FixedAssetsPage();
                return;
            case "payroll":
                PageHost.Content = new Views.Pages.PayrollPage();
                return;
            case "projects":
                PageHost.Content = new Views.Pages.ProjectsPage();
                return;
            case "contracts":
                PageHost.Content = new Views.Pages.ContractsPage();
                return;
            case "boq":
                PageHost.Content = new Views.Pages.BoqPage();
                return;
            case "progress_billings":
                PageHost.Content = new Views.Pages.ProgressBillingsPage();
                return;
            case "subcontractors":
                PageHost.Content = new Views.Pages.SubcontractorsPage();
                return;
            case "retentions":
                PageHost.Content = new Views.Pages.RetentionsPage();
                return;
            case "project_profitability":
                PageHost.Content = new Views.Pages.ProjectProfitabilityPage();
                return;
            case "bom":
                PageHost.Content = new Views.Pages.BomPage();
                return;
            case "work_orders":
                PageHost.Content = new Views.Pages.WorkOrdersPage();
                return;
            case "wip":
                PageHost.Content = new Views.Pages.WipPage();
                return;
            case "pos_session":
                PageHost.Content = new Views.Pages.PosSessionPage();
                return;
            case "pos_shifts":
                PageHost.Content = new Views.Pages.PosShiftsPage();
                return;
            case "pos_reports":
                PageHost.Content = new Views.Pages.PosReportsPage();
                return;
            case "income_statement":
                PageHost.Content = new Views.Pages.IncomeStatementPage();
                return;
            case "balance_sheet":
                PageHost.Content = new Views.Pages.BalanceSheetPage();
                return;
            case "cash_flow_statement":
                PageHost.Content = new Views.Pages.CashFlowStatementPage();
                return;
            case "consolidated_trial_balance":
                PageHost.Content = new Views.Pages.ConsolidatedTrialBalancePage();
                return;
            case "consolidated_income_statement":
                PageHost.Content = new Views.Pages.ConsolidatedIncomeStatementPage();
                return;
            case "consolidated_balance_sheet":
                PageHost.Content = new Views.Pages.ConsolidatedBalanceSheetPage();
                return;
            case "intercompany_elimination":
                PageHost.Content = new Views.Pages.IntercompanyEliminationPage();
                return;
            case "vat_return":
                PageHost.Content = new Views.Pages.VatReturnPage();
                return;
            case "general_ledger_report":
                PageHost.Content = new Views.Pages.GeneralLedgerPage();
                return;
            case "zatca_invoices":
                PageHost.Content = new Views.Pages.ZatcaInvoicesPage();
                return;
            case "zatca_settings":
                PageHost.Content = new Views.Pages.ZatcaSettingsPage();
                return;
            case "users":
                PageHost.Content = new Views.Pages.UsersPage();
                return;
            case "audit_log":
                PageHost.Content = new Views.Pages.AuditLogPage();
                return;
            case "data_quality":
                PageHost.Content = new Views.Pages.DataQualityPage();
                return;
            case "platform_events":
                PageHost.Content = new Views.Pages.PlatformEventsPage();
                return;
            case "customer_support":
                PageHost.Content = new Views.Pages.CustomerSupportPage();
                return;
            case "pricing_plans":
                PageHost.Content = new Views.Pages.PricingPlansPage();
                return;
            case "company_settings":
                PageHost.Content = new Views.Pages.CompanySettingsPage(_setup, this);
                return;
            case "organizations":
                PageHost.Content = new Views.Pages.OrganizationsPage(this);
                return;
            case "company_groups":
                PageHost.Content = new Views.Pages.CompanyGroupsPage();
                return;
            case "branches":
                PageHost.Content = new Views.Pages.BranchesPage();
                return;
            default:
                ShowPlaceholder(pageKey, title);
                return;
        }
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
}
