using Brqosh.Domain.Modules;

namespace Brqosh.Domain.Tests.Modules;

public class ModuleCatalogTests
{
    [Theory]
    [InlineData(BusinessProfile.General)]
    [InlineData(BusinessProfile.Construction)]
    [InlineData(BusinessProfile.Manufacturing)]
    [InlineData(BusinessProfile.Retail)]
    public void EveryProfile_AlwaysIncludesGeneralLedger(BusinessProfile profile)
    {
        var modules = ModuleCatalog.ResolveModules(profile);
        Assert.Contains(ModuleKey.GeneralLedger, modules);
    }

    [Fact]
    public void Construction_IncludesProjectsAndItsDependencies()
    {
        var modules = ModuleCatalog.ResolveModules(BusinessProfile.Construction);

        Assert.Contains(ModuleKey.ProjectsConstruction, modules);
        // مشتريات المقاولات تعتمد على المخزون — يجب حلّها تلقائياً
        Assert.Contains(ModuleKey.Purchasing, modules);
        Assert.Contains(ModuleKey.Inventory, modules);
        Assert.Contains(ModuleKey.Treasury, modules);
    }

    [Fact]
    public void Retail_IncludesPosChain()
    {
        var modules = ModuleCatalog.ResolveModules(BusinessProfile.Retail);

        Assert.Contains(ModuleKey.PointOfSale, modules);
        Assert.Contains(ModuleKey.Sales, modules);
        Assert.Contains(ModuleKey.Inventory, modules);
        Assert.Contains(ModuleKey.Treasury, modules);
    }

    [Fact]
    public void Manufacturing_IncludesBomChain()
    {
        var modules = ModuleCatalog.ResolveModules(BusinessProfile.Manufacturing);

        Assert.Contains(ModuleKey.Manufacturing, modules);
        Assert.Contains(ModuleKey.Inventory, modules);
        Assert.Contains(ModuleKey.Purchasing, modules);
    }

    [Fact]
    public void GeneralLedger_IsTheOnlyCoreModule()
    {
        Assert.True(ModuleCatalog.IsCore(ModuleKey.GeneralLedger));
        Assert.False(ModuleCatalog.IsCore(ModuleKey.Sales));
        Assert.False(ModuleCatalog.IsCore(ModuleKey.PointOfSale));
    }
}
