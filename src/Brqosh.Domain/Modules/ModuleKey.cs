namespace Brqosh.Domain.Modules;

/// <summary>
/// المعرّفات الثابتة لوحدات النظام. النواة المحاسبية (GeneralLedger) لا تُعطَّل أبداً.
/// </summary>
public enum ModuleKey
{
    GeneralLedger,
    Sales,
    Purchasing,
    Inventory,
    Treasury,
    Reports,
    Zatca,
    FixedAssets,
    Payroll,
    PointOfSale,
    Manufacturing,
    ProjectsConstruction,
}
