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
    Crm,

    // ‏القطاعاتُ الأربعة — **ومفاتيحُها أُخِّرت عمداً حتّى بُنيت شاشاتُها**.
    //
    // ‏فمفتاحٌ يمنح وصولاً إلى لا شيءٍ هو بعينه «الاسمُ بلا جسد» الذي يحاربه هذا المستودع:
    // ‏يظهر في إعدادات المنشأة وحدةً مفعَّلة، **ولا تفتح قائمتُه شيئاً**. وقد كُتب حين
    // ‏بُنيت نطاقاتُها في `ط3` أنّها تُؤجَّل إلى حينه — **وهذا حينُه**.
    RealEstate,
    Healthcare,
    Hospitality,
    Logistics,
}
