using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Brqosh.Application.Setup;
using Brqosh.Desktop.Ui;
using Brqosh.Desktop.Views;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Microsoft.Win32;

namespace Brqosh.Desktop.Views.Pages;

public partial class CompanySettingsPage : UserControl
{
    private readonly SetupState _setup;
    private readonly MainWindow? _shell;
    private readonly IConfigurationTemplateLoader _templates = new ConfigurationTemplateLoader();
    private string? _pendingLogoSourcePath;
    private bool _removeLogoRequested;

    private record CostingOption(InventoryCostingMethod Method, string Label)
    {
        public override string ToString() => Label;
    }

    private record GroupOption(long? Id, string Label)
    {
        public override string ToString() => Label;
    }

    public CompanySettingsPage(SetupState setup, MainWindow? shell = null)
    {
        _setup = setup;
        _shell = shell;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (SaveBtn, PermissionService.CanManageUsers),
                (ProvisionOrgBtn, PermissionService.CanManageUsers),
                (LicenseBtn, PermissionService.CanManageUsers),
                (UploadLogoBtn, PermissionService.CanManageUsers),
                (RemoveLogoBtn, PermissionService.CanManageUsers));
            await LoadAsync();
        };
    }

    /// <summary>
    /// (CEEF-229 · Z-10) يقول للمستخدم **ما** ينقصه لا «غير جاهز». وهذه هي الشاشة الوحيدة
    /// التي يستطيع فيها إصلاحه — فإخفاء السبب هنا يعني اكتشافه عند رفض الهيئة.
    /// </summary>
    private void RenderEInvoiceReadiness(Brqosh.Domain.Companies.Company company)
    {
        var gaps = company.RegulatoryGaps();
        EInvoiceReadinessText.Text = gaps.Count == 0
            ? "✓ بيانات المنشأة مكتملة للفوترة الإلكترونية."
            : "ينقص للفوترة الإلكترونية:" + Environment.NewLine
              + "• " + string.Join(Environment.NewLine + "• ", gaps);
    }

    private async Task LoadAsync()
    {
        CostingMethodBox.Items.Clear();
        CostingMethodBox.Items.Add(new CostingOption(InventoryCostingMethod.WeightedAverage, "متوسط مرجح (WAC)"));
        CostingMethodBox.Items.Add(new CostingOption(InventoryCostingMethod.Fifo, "الوارد أولاً صادر أولاً (FIFO)"));

        PrintFontBox.Items.Clear();
        foreach (var font in CompanyBrandingStorage.AllowedPrintFonts)
            PrintFontBox.Items.Add(font);

        await using var db = AppServices.CreateDb();
        var company = await db.Companies.SingleAsync(c => c.Id == AppServices.CompanyId);
        NameBox.Text = company.NameAr;
        NameEnBox.Text = company.NameEn ?? "";
        AddressBox.Text = company.AddressAr ?? "";

        // (CEEF-229 · Z-10) بيانات البائع النظامية
        RegistrationNameBox.Text = company.RegistrationName ?? "";
        CrnBox.Text = company.CommercialRegistrationNumber ?? "";
        var addr = company.NationalAddress;
        BuildingNumberBox.Text = addr.BuildingNumber;
        AdditionalNumberBox.Text = addr.AdditionalNumber;
        StreetNameBox.Text = addr.StreetName;
        DistrictNameBox.Text = addr.DistrictName;
        CityNameBox.Text = addr.CityName;
        PostalZoneBox.Text = addr.PostalZone;
        CountrySubentityBox.Text = addr.CountrySubentity;
        CountryCodeBox.Text = string.IsNullOrWhiteSpace(addr.CountryCode) ? "SA" : addr.CountryCode;
        RenderEInvoiceReadiness(company);
        VatBox.Text = company.VatRegistrationNumber;
        ProfileText.Text = ProfileLabel(company.Profile);
        ActiveModulesText.Text = ActiveModulesLabel(company.Profile);

        var countryId = string.IsNullOrWhiteSpace(_setup.CountryTemplateId)
            ? _templates.DefaultCountryId
            : _setup.CountryTemplateId;
        var config = _templates.Resolve(countryId, company.Profile);
        CountryTemplateText.Text = $"{config.Country.NameAr} — {config.Country.Currency.Base} · VAT {config.Country.Vat.StandardRate:P0}";
        CountryComplianceText.Text = string.Join(" · ", config.Country.Compliance);
        IndustryTemplateText.Text = $"{config.Industry.NameAr} ({config.Industry.NameEn})";
        IndustryModulesText.Text =
            $"وحدات: {string.Join(" · ", config.Industry.Modules)} — ميزات: {string.Join(" · ", config.Industry.Features)}";

        MachineIdBox.Text = LicenseService.MachineId();

        var idx = company.CostingMethod == InventoryCostingMethod.Fifo ? 1 : 0;
        CostingMethodBox.SelectedIndex = idx;
        ManualJournalThresholdBox.Text = company.ManualJournalApprovalThreshold.ToString("0.##", CultureInfo.InvariantCulture);
        BaseCurrencyBox.Text = company.BaseCurrencyCode;
        FunctionalCurrencyBox.Text = company.FunctionalCurrencyCode;

        var fontName = CompanyBrandingStorage.NormalizePrintFont(company.PrintFontFamily);
        PrintFontBox.SelectedItem = CompanyBrandingStorage.AllowedPrintFonts.Contains(fontName)
            ? fontName
            : CompanyBrandingStorage.AllowedPrintFonts[0];

        _pendingLogoSourcePath = null;
        _removeLogoRequested = false;
        ShowLogoPreview(company.LogoFilePath);

        var groups = await db.CompanyGroups.OrderBy(g => g.Code).ToListAsync();
        var options = new List<GroupOption> { new(null, "— بدون مجموعة —") };
        options.AddRange(groups.Select(g => new GroupOption(g.Id, $"{g.Code} — {g.NameAr}")));
        GroupCombo.ItemsSource = options;
        GroupCombo.SelectedItem = options.FirstOrDefault(o => o.Id == company.CompanyGroupId) ?? options[0];
    }

    private void ShowLogoPreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            LogoPreviewImage.Source = null;
            LogoPreviewPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path);
        bmp.EndInit();
        LogoPreviewImage.Source = bmp;
        LogoPreviewPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void OnUploadLogo(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "اختيار شعار الشركة",
            Filter = "صور|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp",
            Multiselect = false,
        };
        if (dialog.ShowDialog() != true)
            return;

        _pendingLogoSourcePath = dialog.FileName;
        _removeLogoRequested = false;
        ShowLogoPreview(_pendingLogoSourcePath);
    }

    private void OnRemoveLogo(object sender, RoutedEventArgs e)
    {
        _pendingLogoSourcePath = null;
        _removeLogoRequested = true;
        LogoPreviewImage.Source = null;
        LogoPreviewPlaceholder.Visibility = Visibility.Visible;
    }

    private static string ProfileLabel(BusinessProfile profile) => profile switch
    {
        BusinessProfile.Construction => LocaleManager.T("مقاولات عامة", "General contracting"),
        BusinessProfile.Manufacturing => LocaleManager.T("مصنع / إنتاج", "Factory / production"),
        BusinessProfile.Retail => LocaleManager.T("محل / تسويق", "Shop / retail"),
        BusinessProfile.Healthcare => LocaleManager.T("قطاع صحي", "Healthcare"),
        BusinessProfile.Logistics => LocaleManager.T("لوجستيات ونقل", "Logistics and transport"),
        BusinessProfile.RealEstate => LocaleManager.T("عقارات وإيجارات", "Real estate and leasing"),
        BusinessProfile.Hospitality => LocaleManager.T("ضيافة ومطاعم", "Hospitality and restaurants"),
        _ => LocaleManager.T("محاسبة عامة", "General accounting"),
    };

    private static string ActiveModulesLabel(BusinessProfile profile)
    {
        var modules = ModuleCatalog.ResolveModules(profile);
        var labels = modules
            .Where(m => m != ModuleKey.GeneralLedger)
            .Select(ModuleLabelAr)
            .Distinct()
            .OrderBy(x => x);
        return $"الوحدات المفعّلة في التنقل: {string.Join(" · ", labels)}";
    }

    private static string ModuleLabelAr(ModuleKey key) => key switch
    {
        ModuleKey.Sales => LocaleManager.T("المبيعات", "Sales"),
        ModuleKey.Purchasing => LocaleManager.T("المشتريات", "Purchasing"),
        ModuleKey.Inventory => LocaleManager.T("المخزون", "Inventory"),
        ModuleKey.Treasury => LocaleManager.T("الخزينة والبنوك", "Treasury and banks"),
        ModuleKey.Reports => LocaleManager.T("التقارير المالية", "Financial reports"),
        ModuleKey.Zatca => LocaleManager.T("الفوترة الإلكترونية", "E-invoicing"),
        ModuleKey.FixedAssets => LocaleManager.T("الأصول الثابتة", "Fixed assets"),
        ModuleKey.Payroll => LocaleManager.T("الرواتب", "Payroll"),
        ModuleKey.PointOfSale => LocaleManager.T("نقاط البيع", "Point of sale"),
        ModuleKey.Manufacturing => LocaleManager.T("التصنيع", "Manufacturing"),
        ModuleKey.ProjectsConstruction => LocaleManager.T("المشاريع والمقاولات", "Projects and contracting"),
        ModuleKey.Crm => LocaleManager.T("إدارة علاقات العملاء", "Customer relationship management"),
        _ => LocaleManager.T("المحاسبة العامة", "General accounting"),
    };

    private void OnProvisionOrganization(object sender, RoutedEventArgs e)
    {
        var dialog = new OrganizationProvisionerWindow { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.Result is not null)
            _ = AfterProvisionAsync(dialog.Result.CompanyId, dialog.Result.CompanyName);
    }

    private async Task AfterProvisionAsync(long companyId, string companyName)
    {
        var answer = UiDialog.Show(
            $"تم إنشاء المنشأة «{companyName}» (معرّف {companyId}).\nهل تريد التبديل إليها الآن؟",
            "BRQOSH", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes && _shell is not null)
            await _shell.SwitchToCompanyAsync(companyId);
    }

    /// <summary>(accounting-core#6) حدُّ الاعتماد كما كُتب — رقمٌ غيرُ سالب، وإلّا رُفض الحفظ باسمه.</summary>
    private decimal ReadThreshold() =>
        NumericInput.TryParseNonNegative(ManualJournalThresholdBox.Text, out var value)
            ? value
            : throw new InvalidOperationException("حد اعتماد القيد اليدوي رقم غير سالب.");

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            await using var db = AppServices.CreateDb();
            var company = await db.Companies.SingleAsync(c => c.Id == AppServices.CompanyId);
            company.Update(NameBox.Text, VatBox.Text);
            // (CEEF-229 · Z-10) تُحفَظ كما أُدخلت — الاكتمال يُفحَص عند التفعيل لا عند الحفظ،
            // فالمستخدم يملأ على دفعات ومنعُ الحفظ الجزئيّ يعني إعادة الإدخال من الصفر.
            company.UpdateRegulatoryIdentity(
                RegistrationNameBox.Text,
                CrnBox.Text,
                Brqosh.Domain.Companies.NationalAddress.Create(
                    BuildingNumberBox.Text, StreetNameBox.Text, DistrictNameBox.Text,
                    CityNameBox.Text, PostalZoneBox.Text, CountrySubentityBox.Text,
                    AdditionalNumberBox.Text, CountryCodeBox.Text));

            company.UpdateBranding(
                NameEnBox.Text,
                AddressBox.Text,
                PrintFontBox.SelectedItem?.ToString() ?? "Tahoma");

            if (_removeLogoRequested)
            {
                CompanyBrandingStorage.RemoveLogo(company.LogoFilePath);
                company.SetLogoFilePath(null);
            }
            else if (!string.IsNullOrWhiteSpace(_pendingLogoSourcePath))
            {
                // (CEEF-227) التفريغ اختياريّ ويفشل بأمان: إن تعذّرت المعالجة يُحفظ
                // الأصل كما هو بدل أن يضيع الشعار.
                var processed = TransparentLogoBox.IsChecked == true
                    ? LogoImageProcessor.MakeBackgroundTransparent(_pendingLogoSourcePath)
                    : null;

                var savedPath = processed is { Length: > 0 }
                    ? CompanyBrandingStorage.SaveLogoPng(company.Id, processed)
                    : CompanyBrandingStorage.SaveLogoFromFile(company.Id, _pendingLogoSourcePath);
                company.SetLogoFilePath(savedPath);
            }

            company.SetCurrencies(BaseCurrencyBox.Text, FunctionalCurrencyBox.Text);
            if (GroupCombo.SelectedItem is GroupOption groupOpt)
                company.AssignCompanyGroup(groupOpt.Id);
            await db.SaveChangesAsync();

            // (CEEF-229 · م1-٢٢) ‏طريقةُ التكلفة **لا تُبدَّل بالسنّارة**: تمرّ بخدمةٍ تمنع
            // ‏التبديلَ وفي المستودعات بضاعة. وكان السطرُ هنا `company.SetCostingMethod(...)`
            // ‏مباشرةً، فيصير المخزونُ القائم بلا طبقاتٍ ويتوقّف المتجرُ عن البيع.
            //
            // ‏**وبعد الحفظ لا قبله**: بقيّةُ الإعدادات (الشعار والعنوان والعملة) تُحفظ أوّلاً،
            // ‏فرفضُ تبديل التكلفة لا يُضيّع ما عداه.
            if (CostingMethodBox.SelectedItem is CostingOption opt)
                {
                using var facade = new DesktopAccountingFacade();
                await facade.GetService<CostingMethodService>().ChangeAsync(company.Id, opt.Method);
            }

            // ‏(accounting-core#6) ‏حدُّ اعتماد القيد اليدويّ ضابطٌ رقابيّ — يمرّ بخدمةٍ تشترط المديرَ وتُدقّق التغيير، ولا يُحفظ بالسنّارة.
            var threshold = ReadThreshold();
            if (company.ManualJournalApprovalThreshold != threshold)
            {
                using var facade = new DesktopAccountingFacade();
                await facade.GetService<ManualJournalApprovalPolicy>().SetThresholdAsync(company.Id, threshold);
            }

            _pendingLogoSourcePath = null;
            _removeLogoRequested = false;
            ShowLogoPreview(company.LogoFilePath);
            ErrorText.Visibility = Visibility.Collapsed;
            UiDialog.Show("تم حفظ إعدادات المنشأة — يُطبَّق الشعار والخط على كل المطبوعات.", "BRQOSH",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnLicense(object sender, RoutedEventArgs e)
    {
        var dialog = new LicenseActivationWindow(NameBox.Text.Trim()) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
    }
}
