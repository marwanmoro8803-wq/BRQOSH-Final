namespace Brqosh.Domain.Modules;

/// <summary>
/// السجل المركزي للوحدات: التبعيات ومصفوفة أنظمة العمل.
/// القاعدة غير القابلة للتفاوض: GeneralLedger مفعّلة دائماً ولا يمكن تعطيلها.
/// </summary>
public static class ModuleCatalog
{
    private static readonly IReadOnlyDictionary<ModuleKey, ModuleKey[]> Dependencies =
        new Dictionary<ModuleKey, ModuleKey[]>
        {
            [ModuleKey.GeneralLedger] = [],
            [ModuleKey.Sales] = [ModuleKey.GeneralLedger],
            [ModuleKey.Purchasing] = [ModuleKey.GeneralLedger, ModuleKey.Inventory],
            [ModuleKey.Inventory] = [ModuleKey.GeneralLedger],
            [ModuleKey.Treasury] = [ModuleKey.GeneralLedger],
            [ModuleKey.Reports] = [ModuleKey.GeneralLedger],
            [ModuleKey.Zatca] = [ModuleKey.GeneralLedger, ModuleKey.Sales],
            [ModuleKey.FixedAssets] = [ModuleKey.GeneralLedger],
            [ModuleKey.Payroll] = [ModuleKey.GeneralLedger],
            [ModuleKey.PointOfSale] = [ModuleKey.GeneralLedger, ModuleKey.Sales, ModuleKey.Inventory, ModuleKey.Treasury],
            [ModuleKey.Manufacturing] = [ModuleKey.GeneralLedger, ModuleKey.Inventory, ModuleKey.Purchasing],
            [ModuleKey.ProjectsConstruction] = [ModuleKey.GeneralLedger, ModuleKey.Purchasing, ModuleKey.Treasury],
            [ModuleKey.Crm] = [ModuleKey.GeneralLedger, ModuleKey.Sales],
        };

    private static readonly IReadOnlyDictionary<BusinessProfile, ModuleKey[]> ProfileModules =
        new Dictionary<BusinessProfile, ModuleKey[]>
        {
            [BusinessProfile.General] =
                [ModuleKey.GeneralLedger, ModuleKey.Crm, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.FixedAssets, ModuleKey.Payroll],
            [BusinessProfile.Construction] =
                [ModuleKey.GeneralLedger, ModuleKey.ProjectsConstruction, ModuleKey.Crm, ModuleKey.Purchasing, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll],
            [BusinessProfile.Manufacturing] =
                [ModuleKey.GeneralLedger, ModuleKey.Manufacturing, ModuleKey.Inventory, ModuleKey.Purchasing, ModuleKey.Crm, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll],
            [BusinessProfile.Retail] =
                [ModuleKey.GeneralLedger, ModuleKey.PointOfSale, ModuleKey.Sales, ModuleKey.Crm, ModuleKey.Inventory, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll],
            [BusinessProfile.Healthcare] =
                [ModuleKey.GeneralLedger, ModuleKey.Sales, ModuleKey.Crm, ModuleKey.Purchasing, ModuleKey.Inventory, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll],
            [BusinessProfile.Logistics] =
                [ModuleKey.GeneralLedger, ModuleKey.Sales, ModuleKey.Crm, ModuleKey.Purchasing, ModuleKey.Inventory, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll],
            [BusinessProfile.RealEstate] =
                [ModuleKey.GeneralLedger, ModuleKey.Sales, ModuleKey.Crm, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll],
            [BusinessProfile.Hospitality] =
                [ModuleKey.GeneralLedger, ModuleKey.PointOfSale, ModuleKey.Sales, ModuleKey.Crm, ModuleKey.Inventory, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll],
        };

    /// <summary>
    /// يُرجع الوحدات المفعّلة لنظام عمل معيّن بعد حل التبعيات بالكامل.
    /// GeneralLedger مضمونة دائماً في النتيجة.
    /// </summary>
    public static IReadOnlySet<ModuleKey> ResolveModules(BusinessProfile profile)
    {
        var resolved = new HashSet<ModuleKey>();
        var queue = new Queue<ModuleKey>(ProfileModules[profile]);
        while (queue.Count > 0)
        {
            var module = queue.Dequeue();
            if (!resolved.Add(module))
                continue;
            foreach (var dependency in Dependencies[module])
                queue.Enqueue(dependency);
        }

        resolved.Add(ModuleKey.GeneralLedger);
        return resolved;
    }

    public static IReadOnlyList<ModuleKey> DependenciesOf(ModuleKey module) => Dependencies[module];

    public static bool IsCore(ModuleKey module) => module == ModuleKey.GeneralLedger;
}
