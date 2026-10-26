using System.IO;
using Brqosh.Desktop.Navigation;
using Brqosh.Domain.Modules;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// («دفتر الحبر والنحاس» · طلبُ المالك 2026-10-26) <b>كلُّ ما يخصّ الإعدادات في خانةٍ واحدةٍ</b> تُفتح من قائمة المستخدم (بطاقةُ المستخدم أسفل القائمة الجانبيّة وشارتُه في الترويسة)،
/// وتنتقل صفحاتُها من القائمة الجانبيّة. الأقسامُ بيانات في <see cref="NavigationCatalog.SettingsSections"/> — فهذه الحراسُ تقيس البيانات والوصلَ بنيويّاً:
/// ما في الخانة صفحةٌ حقيقيّةٌ من الكتالوج مرّةً واحدة، وبعنوانٍ بالعربيّة والإنجليزيّة، وزرُّ البطاقة موصولٌ بالنافذة.
/// </summary>
public class SettingsLivesInTheUserMenuTests
{
    private static string Source(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(new[] { dir!.FullName, "src", "Brqosh.Desktop" }.Concat(relative.Split('/')).ToArray()));
    }

    private static readonly string[] AllKeys =
        NavigationCatalog.Groups.SelectMany(g => g.Pages).Select(p => p.Key).ToArray();

    [Fact]
    public void EverySettingsKey_IsARealCatalogPage_ListedOnce()
    {
        var keys = NavigationCatalog.SettingsSections.SelectMany(s => s.Keys).ToList();

        Assert.NotEmpty(keys);
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        var unknown = keys.Where(k => !AllKeys.Contains(k, StringComparer.Ordinal)).ToList();
        Assert.True(unknown.Count == 0, "مفاتيحُ إعداداتٍ ليست في الكتالوج: " + string.Join("، ", unknown));
        Assert.Contains(NavigationCatalog.SupportKey, AllKeys);
    }

    [Fact]
    public void EverySettingsSection_HasBothTitles_AndAnEnglishTitleForEveryPage()
    {
        foreach (var section in NavigationCatalog.SettingsSections)
        {
            Assert.False(string.IsNullOrWhiteSpace(section.TitleAr));
            Assert.False(string.IsNullOrWhiteSpace(section.TitleEn));
            foreach (var key in section.Keys)
                Assert.NotEqual(key, NavigationCatalog.PageTitleEn(key));   // ‏العنوانُ الإنجليزيّ مُعرَّفٌ لا ارتدادٌ إلى المفتاح
        }

        Assert.Equal("Settings", NavigationCatalog.GroupTitleEn("الإعدادات"));
    }

    [Fact]
    public void TheGatesStayOnTheCatalog_SoMovingThePagesDoesNotOpenAnyLicenceOrProfileHole()
    {
        // ‏الصفحاتُ باقيةٌ في Groups — فـForProfile وIsWithheldByLicense (الحاجبان) يريانها كما كانا.
        foreach (var profile in Enum.GetValues<BusinessProfile>())
        {
            var shown = NavigationCatalog.ForProfile(profile).SelectMany(g => g.Pages).Select(p => p.Key).ToHashSet();
            if (!shown.Contains("zatca_settings"))
                continue;
            // ‏ترخيصٌ بلا وحدة الفوترة الإلكترونيّة يحجب إعداداتِها ولو انتقلت إلى قائمة المستخدم.
            Assert.True(NavigationCatalog.IsWithheldByLicense("zatca_settings", profile, new HashSet<ModuleKey> { ModuleKey.GeneralLedger }));
        }

        Assert.True(NavigationCatalog.IsInUserMenu("users"));
        Assert.True(NavigationCatalog.IsInUserMenu("customer_support"));
        Assert.False(NavigationCatalog.IsInUserMenu("customers"));
        Assert.False(NavigationCatalog.IsInUserMenu("audit_log"));   // ‏الرقابةُ تبقى في «الأمان والتدقيق» لا في الإعدادات
    }

    [Fact]
    public void TheUserCard_IsAButtonWiredToTheMenu_AndTheHeaderBadgeOpensItToo()
    {
        var xaml = Source("MainWindow.xaml");
        var code = Source("MainWindow.xaml.cs");

        Assert.Contains("x:Name=\"UserCardButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"UserMenuButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnUserCardClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MouseLeftButtonUp=\"OnHeaderUserBadgeClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("internal ContextMenu BuildUserMenu()", code, StringComparison.Ordinal);
        // ‏والقائمةُ الجانبيّةُ لا تبني أزرارَ ما انتقل: تُخرجه بالكتالوج نفسِه.
        Assert.Contains("!NavigationCatalog.IsInUserMenu(p.Key)", code, StringComparison.Ordinal);
    }
}
