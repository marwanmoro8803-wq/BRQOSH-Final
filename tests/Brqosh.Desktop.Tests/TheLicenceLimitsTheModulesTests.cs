using Brqosh.Desktop.Navigation;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>security-licensing-tenancy#2</c> · قرارُ المالك 2026-10-18) <b>الترخيصُ يحصر الوحدات بقائمته الموقَّعة.</b>
///
/// <para><b>ما كان:</b> الحمولةُ تحمل <c>Features</c> تحت التوقيع ولا يقرؤها أحد — فأيُّ ترخيصٍ يفتح كلَّ وحدة. <b>والصيغةُ</b>
/// <c>module:&lt;ModuleKey&gt;</c>؛ وترخيصٌ قديمٌ بلا مدخلٍ منها لا يحصر (سلوكُ اليوم)، والحصرُ لا يتّسع بالتبعيّة.</para>
/// </summary>
public class TheLicenceLimitsTheModulesTests
{
    [Fact]
    public void AnOldLicenceWithoutModuleEntries_LimitsNothing()
    {
        Assert.Null(LicensedModules.From(["core", "zatca", "construction"]));
        Assert.Null(LicensedModules.From(null));
    }

    [Fact]
    public void ModuleEntries_AreRead_AndAnUnknownOne_KeepsTheLimit()
    {
        var granted = LicensedModules.From(["core", "module:Sales", "MODULE:inventory", "module:FromANewerVersion"]);

        Assert.NotNull(granted);
        Assert.Equal(new[] { ModuleKey.Sales, ModuleKey.Inventory }.OrderBy(m => m), granted!.OrderBy(m => m));
        Assert.NotNull(LicensedModules.From(["module:FromANewerVersion"]));
    }

    [Fact]
    public void RestrictingDropsWhatTheLicenceDoesNotGrant_AndNeverWidensByDependency()
    {
        var retail = ModuleCatalog.ResolveModules(BusinessProfile.Retail);
        Assert.Contains(ModuleKey.PointOfSale, retail);

        // ‏نقاطُ البيع تتبع المخزون: ترخيصٌ يمنحها بلا مخزونٍ يُسقطها، ولا يجرّ المخزونَ معها.
        var restricted = ModuleCatalog.RestrictToLicense(retail, new HashSet<ModuleKey> { ModuleKey.Sales, ModuleKey.PointOfSale });

        Assert.DoesNotContain(ModuleKey.PointOfSale, restricted);
        Assert.DoesNotContain(ModuleKey.Inventory, restricted);
        Assert.Contains(ModuleKey.Sales, restricted);
        Assert.Contains(ModuleKey.Zatca, restricted);          // من يبيع يُفوتِر إلكترونيّاً
        Assert.Contains(ModuleKey.GeneralLedger, restricted);  // النواة لا تُحجب
        Assert.Same(retail, ModuleCatalog.RestrictToLicense(retail, null));
    }

    [Fact]
    public void TheMenu_AndDirectNavigation_FollowTheLicence()
    {
        var full = NavigationCatalog.ForProfile(BusinessProfile.Retail).SelectMany(g => g.Pages).Select(p => p.Key).ToList();
        var salesOnly = new HashSet<ModuleKey> { ModuleKey.Sales };
        var limited = NavigationCatalog.ForProfile(BusinessProfile.Retail, salesOnly).SelectMany(g => g.Pages).Select(p => p.Key).ToList();

        Assert.True(limited.Count < full.Count, "الترخيصُ المحصورُ لا يُنقص القائمة");
        var withheld = full.Except(limited).First();
        Assert.True(NavigationCatalog.IsWithheldByLicense(withheld, BusinessProfile.Retail, salesOnly));
        Assert.False(NavigationCatalog.IsWithheldByLicense(withheld, BusinessProfile.Retail, null));
        Assert.False(NavigationCatalog.IsWithheldByLicense(limited.First(), BusinessProfile.Retail, salesOnly));
    }
}
