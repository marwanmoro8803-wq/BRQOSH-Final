using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Infrastructure.Setup;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>installer-deployment-ops#13</c> · قرارُ المالك 2026-10-18) <b>اتّفاقيّةُ الترخيص تُقبل قبل الاستخدام.</b>
///
/// <para><b>ما كان:</b> لا اتّفاقيّةَ ولا شروطَ تُعرض — فالبرنامجُ يُستعمل بلا عقدٍ يحدّ المسؤوليّةَ ويحمي الملكيّة. <b>والآن</b>: النصُّ
/// يُشحن بجوار التنفيذيّ (<c>legal/eula.txt</c>) ويُعرض قبل الإعداد، <b>والقبولُ مربوطٌ ببصمة النصّ</b> — فنصٌّ يتغيّر يُسأل عنه من
/// جديد ولا يُحتجّ بقبولٍ لغيره. والقبولُ يُحفظ في جذر البيانات بتاريخه ومستخدم ويندوز الذي قبله.</para>
/// </summary>
public static class EulaAcceptance
{
    /// <summary>النصُّ المشحون بجوار التنفيذيّ.</summary>
    public static string TextPath => Path.Combine(AppContext.BaseDirectory, "legal", "eula.txt");

    private static string RecordPath => Path.Combine(BrqoshDataRoot.Path(), "eula-accepted.json");

    private sealed record Record(string TextSha256, DateTime AcceptedAtUtc, string WindowsUser);

    /// <summary>بصمةُ النصّ — SHA-256 لبايتاته.</summary>
    public static string Fingerprint(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>هل قُبل هذا النصُّ بعينه؟ — وغيابُ النصّ نفسِه لا يُعدّ قبولاً.</summary>
    /// <remarks>سجلٌّ تالفٌ أو محجوزٌ أو ممنوعٌ يُعامَل «غيرَ مقبول» فتُعرض الاتّفاقيّةُ من جديد — <b>ولا يُسقط الإقلاعَ كلَّ مرّة</b>.</remarks>
    public static bool IsAccepted(string text)
    {
        try
        {
            if (!File.Exists(RecordPath))
                return false;
            var record = JsonSerializer.Deserialize<Record>(File.ReadAllText(RecordPath));
            return record is not null && string.Equals(record.TextSha256, Fingerprint(text), StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>يسجّل قبولَ هذا النصّ.</summary>
    public static void Accept(string text)
    {
        Directory.CreateDirectory(BrqoshDataRoot.Path());
        File.WriteAllText(RecordPath, JsonSerializer.Serialize(
            new Record(Fingerprint(text), DateTime.UtcNow, Environment.UserName)));
    }
}
