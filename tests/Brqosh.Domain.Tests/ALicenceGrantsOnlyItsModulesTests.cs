using Brqosh.Domain.Modules;

namespace Brqosh.Domain.Tests;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٦٤ · security-licensing-tenancy#2 · commercial-readiness#5) <b>الترخيصُ يمنح وحداتِه وحدَها.</b>
///
/// <para>‏<b>ما كان:</b> حمولةُ الترخيص تحمل <c>Tier</c> و<c>Features</c> تحت التوقيع، ولا يقرؤهما أحد — فالقائمةُ
/// ‏تُبنى من نظام العمل وحدَه، <b>وأيُّ ترخيصٍ يفتح كلَّ وحدة</b>.</para>
///
/// <para>‏<b>والحكمُ ثلاثُ قواعد:</b> لا حصرَ بلا قائمة (فلا يُقفَل عميلٌ قائم)، والحصرُ لا يتّسع بالتبعيّة (نقاطُ البيع
/// ‏بلا مخزونٍ ممنوحٍ تسقط ولا تجرّه)، والفوترةُ الإلكترونيّةُ تتبع المبيعاتِ الممنوحةَ دائماً (التزامٌ لا فئة).</para>
/// </summary>
public class ALicenceGrantsOnlyItsModulesTests
{
    private static IReadOnlySet<ModuleKey> Set(params ModuleKey[] keys) => new HashSet<ModuleKey>(keys);

    public static TheoryData<BusinessProfile> AllProfiles()
    {
        var data = new TheoryData<BusinessProfile>();
        foreach (var profile in Enum.GetValues<BusinessProfile>())
            data.Add(profile);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void NoModuleListMeansNoRestriction(BusinessProfile profile)
    {
        var active = ModuleCatalog.ResolveModules(profile);

        Assert.Equal(active.OrderBy(m => m), ModuleCatalog.RestrictToLicense(active, null).OrderBy(m => m));
    }

    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void ALicenceGrantingEverything_LeavesTheProfileAsItIs(BusinessProfile profile)
    {
        var active = ModuleCatalog.ResolveModules(profile);

        var restricted = ModuleCatalog.RestrictToLicense(active, Set(Enum.GetValues<ModuleKey>()));

        Assert.Equal(active.OrderBy(m => m), restricted.OrderBy(m => m));
    }

    [Fact]
    public void ALicenceWithholdsWhatItDoesNotGrant()
    {
        var retail = ModuleCatalog.ResolveModules(BusinessProfile.Retail);

        var restricted = ModuleCatalog.RestrictToLicense(retail,
            Set(ModuleKey.Sales, ModuleKey.Purchasing, ModuleKey.Inventory, ModuleKey.Treasury));

        Assert.Equal(
            new[] { ModuleKey.GeneralLedger, ModuleKey.Sales, ModuleKey.Purchasing, ModuleKey.Inventory, ModuleKey.Treasury, ModuleKey.Zatca }
                .OrderBy(m => m),
            restricted.OrderBy(m => m));
        Assert.DoesNotContain(ModuleKey.PointOfSale, restricted);
        Assert.DoesNotContain(ModuleKey.Payroll, restricted);
    }

    /// <summary>‏<b>الحصرُ لا يتّسع بالتبعيّة</b> — نقاطُ البيع الممنوحةُ بلا مخزونٍ ولا خزينةٍ ممنوحين لا تجرّهما، بل تسقط.</summary>
    [Fact]
    public void AGrantedModuleWhoseDependenciesAreWithheld_IsWithheldToo_NotWidened()
    {
        var retail = ModuleCatalog.ResolveModules(BusinessProfile.Retail);

        var restricted = ModuleCatalog.RestrictToLicense(retail, Set(ModuleKey.PointOfSale, ModuleKey.Sales));

        Assert.DoesNotContain(ModuleKey.PointOfSale, restricted);
        Assert.DoesNotContain(ModuleKey.Inventory, restricted);
        Assert.DoesNotContain(ModuleKey.Treasury, restricted);
        Assert.Contains(ModuleKey.Sales, restricted);
    }

    /// <summary>‏والترخيصُ لا يُضيف إلى نظام العمل ما لا يعرضه — فالتصنيعُ الممنوحُ لا يظهر في منشأة تجزئة.</summary>
    [Fact]
    public void ALicenceDoesNotAddModulesTheProfileLacks()
    {
        var retail = ModuleCatalog.ResolveModules(BusinessProfile.Retail);

        var restricted = ModuleCatalog.RestrictToLicense(retail, Set(ModuleKey.Manufacturing, ModuleKey.Inventory, ModuleKey.Purchasing));

        Assert.DoesNotContain(ModuleKey.Manufacturing, restricted);
        Assert.Subset(new HashSet<ModuleKey>(retail), new HashSet<ModuleKey>(restricted));
    }

    /// <summary>‏مَن يبيع في السعوديّة يُفوتِر إلكترونيّاً — فالفوترةُ تتبع المبيعاتِ الممنوحة ولو لم تُذكر، ولا تُمنح بغيرها.</summary>
    [Fact]
    public void EInvoicingFollowsGrantedSales_AndNothingElse()
    {
        var general = ModuleCatalog.ResolveModules(BusinessProfile.General);

        Assert.Contains(ModuleKey.Zatca, ModuleCatalog.RestrictToLicense(general, Set(ModuleKey.Sales)));
        Assert.DoesNotContain(ModuleKey.Zatca, ModuleCatalog.RestrictToLicense(general, Set(ModuleKey.Treasury)));
    }

    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void TheCoreIsNeverWithheld_AndTheResultIsDependencyClosed(BusinessProfile profile)
    {
        var restricted = ModuleCatalog.RestrictToLicense(ModuleCatalog.ResolveModules(profile), Set());

        Assert.Equal(new[] { ModuleKey.GeneralLedger }, restricted);

        foreach (var granted in Enum.GetValues<ModuleKey>())
        {
            var result = ModuleCatalog.RestrictToLicense(ModuleCatalog.ResolveModules(profile), Set(granted));
            Assert.Contains(ModuleKey.GeneralLedger, result);
            Assert.All(result, m => Assert.All(ModuleCatalog.DependenciesOf(m), d => Assert.Contains(d, result)));
        }
    }
}
