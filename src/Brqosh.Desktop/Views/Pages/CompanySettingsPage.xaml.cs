using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Brqosh.Application.Setup;
using Brqosh.Desktop.Ui;
using Brqosh.Desktop.Views;
using Brqosh.Domain.Inventory;
using Brqosh.Infrastructure.Inventory;
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
        BusinessProfile.Construction => "مقاولات عامة",
        BusinessProfile.Manufacturing => "مصنع / إنتاج",
        BusinessProfile.Retail => "محل / تسويق",
        BusinessProfile.Healthcare => "قطاع صحي",
        BusinessProfile.Logistics => "لوجستيات ونقل",
        BusinessProfile.RealEstate => "عقارات وإيجارات",
        BusinessProfile.Hospitality => "ضيافة ومطاعم",
        _ => "محاسبة عامة",
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
        ModuleKey.Sales => "المبيعات",
        ModuleKey.Purchasing => "المشتريات",
        ModuleKey.Inventory => "المخزون",
        ModuleKey.Treasury => "الخزينة والبنوك",
        ModuleKey.Reports => "التقارير المالية",
        ModuleKey.Zatca => "الفوترة الإلكترونية",
        ModuleKey.FixedAssets => "الأصول الثابتة",
        ModuleKey.Payroll => "الرواتب",
        ModuleKey.PointOfSale => "نقاط البيع",
        ModuleKey.Manufacturing => "التصنيع",
        ModuleKey.ProjectsConstruction => "المشاريع والمقاولات",
        ModuleKey.Crm => "إدارة علاقات العملاء",
        _ => "المحاسبة العامة",
    };

    private void OnProvisionOrganization(object sender, RoutedEventArgs e)
    {
        var dialog = new OrganizationProvisionerWindow { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.Result is not null)
            _ = AfterProvisionAsync(dialog.Result.CompanyId, dialog.Result.CompanyName);
    }

    private async Task AfterProvisionAsync(long companyId, string companyName)
    {
        var answer = MessageBox.Show(
            $"تم إنشاء المنشأة «{companyName}» (معرّف {companyId}).\nهل تريد التبديل إليها الآن؟",
            "BRQOSH", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes && _shell is not null)
            await _shell.SwitchToCompanyAsync(companyId);
    }

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

            _pendingLogoSourcePath = null;
            _removeLogoRequested = false;
            ShowLogoPreview(company.LogoFilePath);
            ErrorText.Visibility = Visibility.Collapsed;
            MessageBox.Show("تم حفظ إعدادات المنشأة — يُطبَّق الشعار والخط على كل المطبوعات.", "BRQOSH",
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
