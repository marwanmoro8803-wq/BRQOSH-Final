using System.Security.Cryptography;
using System.Text;

namespace Brqosh.Infrastructure.Security;

/// <summary>تشفير أسرار محلية (DPAPI على Windows) — ZATCA-002.</summary>
public static class LocalSecretProtector
{
    private const string Prefix = "BRQOSH1:";

    /// <summary>
    /// (م-٦ · تبعيّةُ ت-٣) <b>هل هذه القيمةُ محميّةٌ فعلاً؟</b> يلزم لترقية الملفّات
    /// القديمة: <c>Unprotect</c> يمرّر الصريحَ كما هو، <b>فلا يُعرف منه أكانت محميّةً أم لا</b>.
    /// </summary>
    public static bool IsProtected(string? stored) =>
        stored is not null && stored.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// (م-٦ · تبعيّةُ ت-٣) <b>قرارٌ واحدٌ لسؤال «أتتوفّر الحمايةُ على هذه المنصّة؟»</b> —
    /// يستشيره <c>Protect</c> و<c>Unprotect</c>، فلا يتفرّق الجوابُ على موضعين ويركب
    /// أحدُهما الأضعف (ن-٣٨).
    ///
    /// <para><b>وما لا يفعله، ويُقال:</b> ترقيةُ الملفّات القديمة في
    /// <c>DatabaseConfigFile.Load</c> <b>لا تستشيره</b>. فهو يجيب عن <b>السبب</b>، وهي
    /// تسأل عن <b>الأثر</b>: «هل ستُنتج الحمايةُ شيئاً محميّاً فعلاً؟» — سؤالٌ يغطّي
    /// المنصّةَ <b>والسلسلةَ الفارغةَ</b> وما لم يخطر بعد. <b>وكلاهما يجب أن يتّفقا</b>،
    /// ويُقاس اتّفاقُهما في <c>DatabaseConfigFileTests</c> بلا فرعٍ للمنصّة.</para>
    /// </summary>
    public static bool ProtectionIsAvailable => OperatingSystem.IsWindows();

    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain))
            return plain ?? "";
        if (plain.StartsWith(Prefix, StringComparison.Ordinal))
            return plain;

        if (!ProtectionIsAvailable)
            return plain;

        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(bytes);
    }

    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
            return stored ?? "";

        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            return stored;

        if (!ProtectionIsAvailable)
            return "";

        var payload = stored[Prefix.Length..];
        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(payload), null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
