using System.Windows;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// («دفتر الحبر والنحاس») <b>كثافةُ الجداول: مريحة أو مضغوطة.</b> محاسبٌ يقرأ مئاتِ القيود يريد ثلاثين صفّاً في الشاشة لا عشرين، وآخرُ
/// يعمل بلمسٍ أو نظرٍ ضعيف يريد الصفَّ أعلى — فالارتفاعُ مفتاحُ موردٍ واحد (<c>GridRowHeight</c>) يقرؤه نمطُ DataGrid بـ<c>DynamicResource</c>
/// فيتغيّر في كلّ جدولٍ دون لمس صفحة. ويُحفظ في <see cref="UiPreferences.Compact"/> ويعود عند كلّ تشغيل.
/// </summary>
public static class DensityManager
{
    /// <summary>ارتفاعُ الصفّ المريح (الأصل) والمضغوط.</summary>
    public const double ComfortableRowHeight = 40;

    public const double CompactRowHeight = 32;

    public static bool IsCompact { get; private set; }

    public static event Action? Changed;

    public static void Apply(bool compact)
    {
        var changed = IsCompact != compact;
        IsCompact = compact;
        ApplyTo(System.Windows.Application.Current.Resources, compact);
        if (changed)
            Changed?.Invoke();
    }

    /// <summary>يطبّق الكثافةَ على قاموسٍ بعينه — يفصلها عن <c>Application.Current</c> فتُقاس بلا تشغيل التطبيق.</summary>
    public static void ApplyTo(ResourceDictionary resources, bool compact) =>
        resources["GridRowHeight"] = compact ? CompactRowHeight : ComfortableRowHeight;
}
