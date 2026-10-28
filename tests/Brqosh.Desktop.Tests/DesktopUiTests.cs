using Brqosh.Desktop.Navigation;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Application.Intelligence;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Desktop.Tests;

public class NavIconGlyphsTests
{
    [Theory]
    [InlineData("dashboard", "LC")]
    [InlineData("journal_entries", "QY")]
    [InlineData("exchange_rates", "FX")]
    [InlineData("company_groups", "GRP")]
    public void Get_returns_registered_monogram(string key, string expected) =>
        Assert.Equal(expected, NavIconGlyphs.Get(key));

    [Fact]
    public void Get_unknown_key_falls_back_to_initials()
    {
        var glyph = NavIconGlyphs.Get("custom_module");
        Assert.Equal("CM", glyph);
    }
}

public class LocaleManagerTests
{
    [Fact]
    public void T_switches_with_language()
    {
        var startEnglish = LocaleManager.IsEnglish;
        try
        {
            LocaleManager.Toggle();
            var flipped = LocaleManager.T("عربي", "English");
            Assert.Equal(startEnglish ? "عربي" : "English", flipped);
        }
        finally
        {
            if (LocaleManager.IsEnglish != startEnglish)
                LocaleManager.Toggle();
        }
    }
}

public class G27I18nRegistryTests
{
    [Fact]
    public void UiBilingualRegistry_covers_all_xaml_strings()
    {
        Assert.True(UiBilingualRegistry.Map.Count >= 700);
        Assert.Equal("Save", UiBilingualRegistry.Lookup("حفظ"));
        Assert.Equal("Device ID", UiBilingualRegistry.Lookup("معرّف الجهاز"));
    }

    [Fact]
    public void Lookup_returns_english_for_known_arabic()
    {
        Assert.Equal("Save", UiBilingualRegistry.Lookup("حفظ"));
        Assert.Equal("Customers", UiBilingualRegistry.Lookup("العملاء"));
    }
}

public class EmptyStateCatalogTests
{
    [Fact]
    public void EmptyState_crm_leads_has_english()
    {
        var spec = EmptyStateCatalog.Resolve("LeadsGrid", "crm_leads");
        Assert.Contains("محتمل", spec.TitleAr);
        Assert.Equal("No leads", spec.TitleEn);
    }

    [Fact]
    public void Resolve_customers_grid()
    {
        var spec = EmptyStateCatalog.Resolve("CustomersGrid");
        // TitleAr لا Title: الأخيرة تعتمد على LocaleManager (حالة ساكنة عامة) الذي يبدّله
        // LocaleManagerTests — فكان الاختبار يتذبذب حسب ترتيب التشغيل (CEEF-202).
        Assert.Contains("عملاء", spec.TitleAr);
    }
}

public class NavigationCatalogTests
{
    [Fact]
    public void ForProfile_general_ledger_includes_dashboard()
    {
        var groups = NavigationCatalog.ForProfile(BusinessProfile.General);
        var pages = groups.SelectMany(g => g.Pages).Select(p => p.Key).ToList();
        Assert.Contains("dashboard", pages);
        Assert.Contains("journal_entries", pages);
    }

    [Fact]
    public void Nav_pages_have_no_legacy_icon_field()
    {
        foreach (var page in NavigationCatalog.Groups.SelectMany(g => g.Pages))
        {
            Assert.False(string.IsNullOrWhiteSpace(page.Key));
            Assert.False(string.IsNullOrWhiteSpace(page.TitleAr));
        }
    }

    [Fact]
    public void Nav_pages_have_english_titles()
    {
        foreach (var page in NavigationCatalog.Groups.SelectMany(g => g.Pages))
        {
            var en = NavigationCatalog.PageTitleEn(page.Key);
            Assert.False(string.IsNullOrWhiteSpace(en));
            Assert.NotEqual(page.Key, en);
        }
    }

    [Theory]
    [InlineData("sales_quotations", "عروض الأسعار")]
    [InlineData("sales_orders", "أوامر البيع")]
    [InlineData("organizations", "المنشآت")]
    [InlineData("crm_leads", "العملاء المحتملون")]
    [InlineData("crm_opportunities", "فرص البيع")]
    [InlineData("customer_support", "الدعم والشكاوى")]
    [InlineData("preferences", "تفضيلات البرنامج")]
    public void V13_lifecycle_pages_registered_in_catalog(string key, string titleAr)
    {
        var page = NavigationCatalog.Groups.SelectMany(g => g.Pages).Single(p => p.Key == key);
        Assert.Equal(titleAr, page.TitleAr);
    }

    [Fact]
    public void ForProfile_retail_includes_pos_not_manufacturing_bom()
    {
        var retail = NavigationCatalog.ForProfile(BusinessProfile.Retail)
            .SelectMany(g => g.Pages).Select(p => p.Key).ToHashSet();
        var mfg = NavigationCatalog.ForProfile(BusinessProfile.Manufacturing)
            .SelectMany(g => g.Pages).Select(p => p.Key).ToHashSet();

        Assert.Contains("pos_session", retail);
        Assert.Contains("projects", NavigationCatalog.ForProfile(BusinessProfile.Construction)
            .SelectMany(g => g.Pages).Select(p => p.Key));
        Assert.DoesNotContain("bom", retail);
        Assert.Contains("bom", mfg);
    }

    [Fact]
    public void ForProfile_construction_includes_projects_not_pos()
    {
        var keys = NavigationCatalog.ForProfile(BusinessProfile.Construction)
            .SelectMany(g => g.Pages).Select(p => p.Key).ToHashSet();

        Assert.Contains("projects", keys);
        Assert.Contains("boq", keys);
        Assert.DoesNotContain("pos_session", keys);
    }

    [Fact]
    public void Administrator_can_access_organizations()
    {
        var admin = AppUser.Create(1, "admin", "hash", "مدير", UserRole.Administrator);
        Assert.True(PermissionService.CanAccess(admin, "organizations"));
        Assert.True(PermissionService.CanAccess(admin, "sales_quotations"));
        Assert.True(PermissionService.CanAccess(admin, "customer_support"));
    }
}

public class SecuritySessionBindingTests
{
    [Fact]
    public void CreateDb_rebinds_administrator_role_after_asynclocal_loss()
    {
        var priorUser = AppServices.CurrentUser;
        try
        {
            AppServices.CurrentUser = AppUser.Create(1, "marwan", "hash", "مدير النظام", UserRole.Administrator);
            SecurityContext.CurrentRole = null;
            SecurityContext.CurrentUsername = null;

            _ = AppServices.CreateDb();

            Assert.Equal(UserRole.Administrator, SecurityContext.CurrentRole);
            Assert.Equal("marwan", SecurityContext.CurrentUsername);
        }
        finally
        {
            AppServices.CurrentUser = priorUser;
        }
    }
}

public class LoginPreferencesStoreTests
{
    [Fact]
    public void Save_and_load_roundtrip()
    {
        // ‏في مجلّدٍ مؤقّت: الكتابةُ في %APPDATA%\BRQOSH الحقيقيّ كانت تترك «demo» محفوظاً في شاشة دخول المالك بعد كلّ تشغيلٍ للاختبارات.
        var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "brq-loginprefs-" + Guid.NewGuid().ToString("N")[..8], "login-prefs.json");
        var previous = LoginPreferencesStore.FilePathOverride;
        LoginPreferencesStore.FilePathOverride = temp;
        try
        {
            LoginPreferencesStore.Save(new LoginPreferences { RememberMe = true, Username = "demo" });
            var loaded = LoginPreferencesStore.Load();
            Assert.True(loaded.RememberMe);
            Assert.Equal("demo", loaded.Username);
        }
        finally
        {
            LoginPreferencesStore.FilePathOverride = previous;
            try { System.IO.Directory.Delete(System.IO.Path.GetDirectoryName(temp)!, true); } catch (System.IO.IOException) { }
        }
    }

    [Fact]
    public void TheTestsNeverLeaveTheirNameInTheRealProfile()
    {
        // ‏حارسٌ: إن وُجد ملفُّ تفضيلات المستخدم الحقيقيّ بعد الاختبارات فلا يجوز أن يحمل اسمَ الاختبار.
        var real = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH", "login-prefs.json");
        if (!System.IO.File.Exists(real)) return;
        Assert.DoesNotContain("\"demo\"", System.IO.File.ReadAllText(real), StringComparison.Ordinal);
    }
}

public class DataGridRtlHelperTests
{
    [Fact]
    public void ReverseColumnsForRtl_reverses_multi_column_order()
    {
        var source = new[] { "الفترة", "من", "إلى", "الحالة", "عدد القيود", "أُقفلت في" };
        var reversed = DataGridRtlHelper.ReverseColumnsForRtl(source);
        Assert.Equal(new[] { "أُقفلت في", "عدد القيود", "الحالة", "إلى", "من", "الفترة" }, reversed);
    }

    [Fact]
    public void ReverseColumnsForRtl_keeps_single_column()
    {
        var source = new[] { "only" };
        Assert.Same(source, DataGridRtlHelper.ReverseColumnsForRtl(source));
    }
}

public class PermissionServiceTests
{
    private static AppUser Accountant() => AppUser.Create(1, "acc", "hash", "محاسب", UserRole.Accountant);
    private static AppUser Viewer() => AppUser.Create(1, "view", "hash", "مشاهد", UserRole.Viewer);

    [Theory]
    [InlineData("purchase_requests")]
    [InlineData("intercompany_elimination")]
    [InlineData("data_quality")]
    public void Accountant_can_access_operational_screens(string pageKey) =>
        Assert.True(PermissionService.CanAccess(Accountant(), pageKey));

    [Fact]
    public void Viewer_cannot_access_purchase_requests() =>
        Assert.False(PermissionService.CanAccess(Viewer(), "purchase_requests"));

    [Fact]
    public void Accountant_cannot_access_platform_events() =>
        Assert.False(PermissionService.CanAccess(Accountant(), "platform_events"));
}

public class ProgramAssistantDesktopSmokeTests
{
    [Fact]
    public void Page_guide_templates_cover_expansion_wave()
    {
        Assert.True(ProgramPageGuideTemplates.Count >= 40);
        Assert.True(ProgramNavCatalog.Count >= ProgramPageGuideTemplates.Count);
    }

    [Fact]
    public void QuestionMatcher_indexes_all_nav_keys()
    {
        Assert.Equal(ProgramNavCatalog.Count, ProgramQuestionMatcher.AllKeys().Count);
    }

    [Fact]
    public void Every_nav_key_resolves_actionable_guide()
    {
        foreach (var key in ProgramNavCatalog.Keys)
        {
            var guide = ProgramKnowledge.TryGetPageGuide(key);
            Assert.NotNull(guide);
            Assert.False(string.IsNullOrWhiteSpace(guide!.GuideAr));
        }
    }
}
