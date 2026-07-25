namespace Brqosh.Domain.Modules;

/// <summary>
/// نظام العمل الذي يختاره المستخدم عند أول تشغيل، ويحدد الوحدات المفعّلة.
/// </summary>
public enum BusinessProfile
{
    /// <summary>محاسبة عامة — النواة والخزينة والتقارير فقط.</summary>
    General,

    /// <summary>شركة مقاولات عامة — مشاريع، BOQ، مستخلصات، مقاولو باطن.</summary>
    Construction,

    /// <summary>مصنع / شركة إنتاج — BOM، أوامر إنتاج، تكلفة صناعية.</summary>
    Manufacturing,

    /// <summary>محل / شركة تسويق — نقاط بيع، باركود، مبيعات سريعة.</summary>
    Retail,

    /// <summary>منشأة صحية — عيادات، مستشفيات، صيدليات.</summary>
    Healthcare,

    /// <summary>لوجستيات ونقل — مستودعات، شحن، توزيع.</summary>
    Logistics,

    /// <summary>عقارات وإيجارات — مولات، وحدات، عقود إيجار.</summary>
    RealEstate,

    /// <summary>ضيافة ومطاعم — فنادق، مطاعم، خدمات ضيافة.</summary>
    Hospitality,
}
