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

            // (CEEF-230 · ط6-٤) ‏والقطاعاتُ الأربعة كلُّها تلزمها **المبيعات**: عقدُ الإيجار
            // ‏والزيارةُ وحسابُ النزيل والبوليصة **تنتهي كلُّها إلى فاتورة**. ومَن ملك القطاعَ
            // ‏بلا مبيعاتٍ ملك جداولَ لا تُفوتِر.
            [ModuleKey.RealEstate] = [ModuleKey.GeneralLedger, ModuleKey.Sales],
            [ModuleKey.Healthcare] = [ModuleKey.GeneralLedger, ModuleKey.Sales],

            // ‏**والضيافةُ تلزمها المخزون** — الوصفةُ تصرف مكوّناتِها، وبلا مخزونٍ لا يُصرف
            // ‏شيءٌ ولا تُقيَّد تكلفة. وهي الهويّةُ التي بُني `ط3-٣أ` لأجلها.
            [ModuleKey.Hospitality] = [ModuleKey.GeneralLedger, ModuleKey.Sales, ModuleKey.Inventory],
            [ModuleKey.Logistics] = [ModuleKey.GeneralLedger, ModuleKey.Sales],
        };

    private static readonly IReadOnlyDictionary<BusinessProfile, ModuleKey[]> ProfileModules =
        new Dictionary<BusinessProfile, ModuleKey[]>
        {
            // (CEEF-230 · ط0-١) ‏أُضيفت Zatca — **مَن يبيع في السعودية يُفوتِر إلكترونياً**.
            //
            // ‏الملفُّ لا يُعلن `Sales` صراحةً، **لكنّه يُحلّ إليها بالتبعية** — فمنشأةُ
            // ‏«محاسبة عامة» تُصدر فواتيرَ مبيعات. وبلا `Zatca` تُصدَر **خارج نظام الهيئة**:
            // ‏لا بصمةَ ولا رمزَ استجابةٍ ولا إرسال، وهو مخالفةٌ نظامية لا نقصُ ميزة.
            //
            // ‏ولم يُكشف إلا بمسبار القطاعات: الخريطةُ الخام تقول «بلا مبيعات» والمحلولةُ
            // ‏تقول غيرَه — **وهو الفرقُ بين ما كُتب وما يعمل**.
            [BusinessProfile.General] =
                [ModuleKey.GeneralLedger, ModuleKey.Crm, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.FixedAssets, ModuleKey.Payroll, ModuleKey.Zatca],
            [BusinessProfile.Construction] =
                [ModuleKey.GeneralLedger, ModuleKey.ProjectsConstruction, ModuleKey.Crm, ModuleKey.Purchasing, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll],
            [BusinessProfile.Manufacturing] =
                [ModuleKey.GeneralLedger, ModuleKey.Manufacturing, ModuleKey.Inventory, ModuleKey.Purchasing, ModuleKey.Crm, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll],
            // (CEEF-229 · م2-١٩) أُضيفت Purchasing — كانت غائبة عن التجزئة والضيافة وحدهما
            // من بين كل الأنظمة التي تملك مخزوناً. ومتجرٌ يشتري بضاعته كما يشتريها مصنع:
            // بغيابها تختفي شاشات الموردين وأوامر الشراء وفواتير المشتريات من القائمة، فلا
            // تدخل بضاعةٌ إلى المستودع أصلاً (فيرفض أوّلُ بيعٍ بحارس «الرصيد غير كافٍ»)،
            // ويُقدَّم الإقرار الضريبيّ بلا مدخلاتٍ قابلة للخصم فتُسدَّد للهيئة زيادة.
            // وهو عطلٌ تأسيسيّ لا رجعة فيه: UpdateBusinessProfile بلا مستدعٍ في المستودع.
            [BusinessProfile.Retail] =
                [ModuleKey.GeneralLedger, ModuleKey.PointOfSale, ModuleKey.Sales, ModuleKey.Purchasing, ModuleKey.Crm, ModuleKey.Inventory, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll],
            [BusinessProfile.Healthcare] =
                [ModuleKey.GeneralLedger, ModuleKey.Sales, ModuleKey.Crm, ModuleKey.Purchasing, ModuleKey.Inventory, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll, ModuleKey.Healthcare],
            [BusinessProfile.Logistics] =
                [ModuleKey.GeneralLedger, ModuleKey.Sales, ModuleKey.Crm, ModuleKey.Purchasing, ModuleKey.Inventory, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll, ModuleKey.Logistics],
            [BusinessProfile.RealEstate] =
                [ModuleKey.GeneralLedger, ModuleKey.Sales, ModuleKey.Crm, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll, ModuleKey.RealEstate],
            [BusinessProfile.Hospitality] =
                [ModuleKey.GeneralLedger, ModuleKey.PointOfSale, ModuleKey.Sales, ModuleKey.Purchasing, ModuleKey.Crm, ModuleKey.Inventory, ModuleKey.Treasury, ModuleKey.Reports, ModuleKey.Zatca, ModuleKey.FixedAssets, ModuleKey.Payroll, ModuleKey.Hospitality],
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
