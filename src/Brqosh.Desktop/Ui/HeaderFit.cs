namespace Brqosh.Desktop.Ui;

/// <summary>
/// («دفتر الحبر والنحاس») تدرّجُ الترويسة بحسب العرض المتاح لها. أدواتُها (منشأة · فرع · لغة · مساعد · مظهر · تاريخ · مستخدم) تحتاج ≈١٢٣٠ بكسلاً كاملةً، والحدّ الأدنى للنافذة ١٠٢٤
/// يترك للترويسة ٧٥٢ فقط (و٤٥٢ إن فُتحت لوحةُ المساعد) — فكانت الأدواتُ تدهس العنوان. تُسقَط الزوائدُ أوّلاً (تسميات الحقول والتاريخ ثم شارةُ المستخدم) ثم تضيق القوائم.
/// </summary>
public static class HeaderFit
{
    public readonly record struct Tier(bool ShowLabelsAndDate, bool ShowUserBadge, double CompanyWidth, double BranchWidth);

    public static Tier For(double headerWidth) => headerWidth switch
    {
        >= 1240 => new Tier(true, true, 200, 180),
        >= 960 => new Tier(false, true, 200, 180),
        >= 740 => new Tier(false, false, 150, 130),
        _ => new Tier(false, false, 120, 100),
    };
}
