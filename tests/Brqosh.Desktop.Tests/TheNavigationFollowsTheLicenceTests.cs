using System.IO;
using Brqosh.Desktop.Navigation;
using Brqosh.Domain.Modules;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٦٤ · security-licensing-tenancy#2) <b>القائمةُ لنظام العمل وللترخيص معاً — والتنقّلُ يرفض ما حجبه.</b>
///
/// <para>‏إخفاءُ زرّ القائمة وحدَه لا يحجب شيئاً: الشاشةُ تُفتح بمفتاحها من روابط اللوحة والشاشات الأخرى.
/// ‏فالحكمُ في <c>Navigate</c> نفسِه (<see cref="NavigationCatalog.IsWithheldByLicense"/>)، والقائمةُ تُبنى منه.</para>
///
/// <para>‏<b>ولمَ إثباتٌ في النصّ لـ<c>MainWindow</c>:</b> كودٌ خلف WPF يفتح <c>AppServices</c> الساكنة — كما في
/// ‏<see cref="TheNavigationFollowsTheCompanysProfileTests"/>؛ والحكمُ نفسُه مقيسٌ سلوكاً هنا على الكتالوج.</para>
/// </summary>
public class TheNavigationFollowsTheLicenceTests
{
    private static IReadOnlySet<ModuleKey> Set(params ModuleKey[] keys) => new HashSet<ModuleKey>(keys);

    private static IEnumerable<string> Keys(IReadOnlyList<NavGroup> groups) =>
        groups.SelectMany(g => g.Pages).Select(p => p.Key);

    [Fact]
    public void WithoutAModuleList_TheMenuIsTheProfilesMenu()
    {
        foreach (var profile in Enum.GetValues<BusinessProfile>())
            Assert.Equal(Keys(NavigationCatalog.ForProfile(profile)), Keys(NavigationCatalog.ForProfile(profile, null)));
    }

    [Fact]
    public void AWithheldModule_LeavesTheMenu_AndItsPageIsRefused()
    {
        var licensed = Set(ModuleKey.Sales);

        var keys = Keys(NavigationCatalog.ForProfile(BusinessProfile.Retail, licensed)).ToList();

        Assert.DoesNotContain("pos_session", keys);
        Assert.DoesNotContain("payroll", keys);
        Assert.Contains("sales_invoices", keys);
        Assert.Contains("chart_of_accounts", keys);
        Assert.True(NavigationCatalog.IsWithheldByLicense("pos_session", BusinessProfile.Retail, licensed));
        Assert.True(NavigationCatalog.IsWithheldByLicense("payroll", BusinessProfile.Retail, licensed));
        Assert.False(NavigationCatalog.IsWithheldByLicense("sales_invoices", BusinessProfile.Retail, licensed));
    }

    /// <summary>‏ما لا يعرضه نظامُ العمل أصلاً ليس من شأن الترخيص — والصفحةُ خارج الكتالوج كذلك؛ فالحجبُ لا يتّسع إلى غيره.</summary>
    [Fact]
    public void TheLicenceRefusesOnlyWhatTheProfileWouldShow()
    {
        var licensed = Set(ModuleKey.Sales);

        Assert.False(NavigationCatalog.IsWithheldByLicense("pos_session", BusinessProfile.General, licensed));
        Assert.False(NavigationCatalog.IsWithheldByLicense("no_such_page", BusinessProfile.Retail, licensed));
        Assert.False(NavigationCatalog.IsWithheldByLicense("pos_session", BusinessProfile.Retail, null));
    }

    [Fact]
    public void TheWindowBuildsItsMenu_AndNavigates_ThroughTheLicence()
    {
        var source = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop", "MainWindow.xaml.cs"));

        Assert.Contains("NavigationCatalog.ForProfile(_setup.Profile, _licensedModules)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigationCatalog.ForProfile(_setup.Profile)", source, StringComparison.Ordinal);
        Assert.Contains("_licensedModules = Brqosh.Infrastructure.Licensing.LicenseStore.LicensedModulesFor(", source, StringComparison.Ordinal);

        var navigate = source.IndexOf("private void Navigate(string pageKey, string title)", StringComparison.Ordinal);
        Assert.True(navigate > 0, "‏لا «Navigate» في MainWindow.xaml.cs.");
        var guard = source.IndexOf("NavigationCatalog.IsWithheldByLicense(pageKey, _setup.Profile, _licensedModules)", navigate, StringComparison.Ordinal);
        var firstPage = source.IndexOf("PageHost.Content =", navigate, StringComparison.Ordinal);
        Assert.True(guard > 0 && guard < firstPage, "‏الحجبُ بالترخيص لا يسبق فتحَ الشاشة في Navigate.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }
}
