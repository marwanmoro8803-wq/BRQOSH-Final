using Brqosh.Domain.Modules;

namespace Brqosh.Infrastructure.Licensing;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٦٤ · security-licensing-tenancy#2) <b>الوحداتُ التي يمنحها ترخيصٌ موقَّع — من حقل <c>Features</c>.</b>
///
/// <para>‏<b>ما كان:</b> الحمولةُ تحمل <c>Tier</c> و<c>Features</c> تحت التوقيع، <b>ولا يقرؤهما أحد</b> — فأيُّ ترخيصٍ يفتح كلَّ وحدة.</para>
///
/// <para>‏<b>والصيغةُ مدخلٌ صريحٌ لكلّ وحدة: <c>module:&lt;ModuleKey&gt;</c></b> (مثل <c>module:Sales</c>). <b>ولمَ لا أسماءُ الوحدات عارية:</b>
/// ‏كلُّ ترخيصٍ صدر قبل هذا يحمل <c>["core","zatca","construction"]</c> من أداتَي الإصدار — وسومٌ لا تعني شيئاً. ولو قُرئت قائمةً
/// ‏لأُقفلت نقاطُ البيع والمخزونُ على عميل تجزئةٍ قائمٍ صباحَ التحديث. <b>فترخيصٌ بلا أيّ مدخل <c>module:</c> لا يحصر</b> (<c>null</c>)،
/// ‏وهو سلوكُ اليوم بعينه؛ والحصرُ يبدأ بأوّل ترخيصٍ يُصدَر بمدخلاته.</para>
///
/// <para>‏والمدخلُ بوحدةٍ لا يعرفها هذا الإصدار يُتجاهَل <b>ويبقى الحصرُ قائماً</b> — فترخيصٌ من إصدارٍ أحدث لا يصير مفتوحاً هنا.</para>
/// </summary>
public static class LicensedModules
{
    public const string Prefix = "module:";

    /// <returns>الوحداتُ الممنوحة، أو <c>null</c> إن لم يحصر الترخيصُ شيئاً.</returns>
    public static IReadOnlySet<ModuleKey>? From(IEnumerable<string>? features)
    {
        if (features is null)
            return null;

        HashSet<ModuleKey>? granted = null;
        foreach (var feature in features)
        {
            if (feature is null || !feature.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            granted ??= [];
            var name = feature[Prefix.Length..].Trim();
            if (Enum.TryParse<ModuleKey>(name, ignoreCase: true, out var key) && Enum.IsDefined(key))
                granted.Add(key);
        }

        return granted;
    }

    /// <summary>مدخلاتُ <c>Features</c> لمجموعة وحدات — لأداتَي الإصدار.</summary>
    public static string[] ToFeatures(IEnumerable<ModuleKey> modules) =>
        modules.Distinct().Select(m => Prefix + m).ToArray();
}
