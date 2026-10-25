using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Brqosh.Infrastructure.Setup;

/// <summary>
/// (التنصيبُ الجديد يبدأ فارغاً) <b>تنصيبٌ جديدٌ لا يرث بياناتِ تنصيبٍ سابقٍ بلا سؤال.</b>
///
/// <para><b>ما كان:</b> جذرُ البيانات (<c>%APPDATA%\BRQOSH</c>) يبقى بعد إزالة البرنامج وإعادةِ تنصيبه — وبه <c>setup.json</c> المكتمل واسمُ المستخدم
/// المحفوظ وإعداداتُ القاعدة. فكان التنصيبُ «الجديدُ» يتخطّى معالجَ الإعداد ويفتح شاشةَ الدخول على منشأةٍ قديمةٍ ومستخدمٍ محفوظ (<c>demo</c>)،
/// وهو ما رآه المالكُ عند أوّل تشغيلٍ بعد الشروط والأحكام.</para>
///
/// <para><b>ولا يصحّ المسحُ الصامت</b> — بياناتُ المحاسبة لا تُحذف لأنّ مستخدماً أعاد التنصيب. فالقاعدةُ: لكلّ تنصيبٍ <b>بصمةٌ</b> (مسارُ التنفيذيّ والإصدارُ
/// وتاريخُ كتابته). وتُحفَظ في الجذر عند أوّل تشغيل. فإن اختلفت عند الإقلاع:</para>
/// <list type="bullet">
/// <item><b>ترقيةٌ</b> (إصدارٌ أحدثُ من المسجَّل): تُتابَع البياناتُ بلا سؤال — هذه الترقيةُ العاديّة.</item>
/// <item><b>غيرُ ذلك</b> (إعادةُ تنصيبٍ لنفس الإصدار أو أقدم، أو جذرٌ بلا سجلّ بصمة): يُسأل المستخدمُ: يتابع بياناتِه أم يبدأ تنصيباً فارغاً؟</item>
/// </list>
/// <para>والبدءُ الفارغُ <b>ينقل</b> بياناتِ التنصيب السابق إلى <c>previous-&lt;تاريخ&gt;</c> داخل الجذر ولا يحذفها، ويُبقي الترخيصَ وعلامةَ الساعة وقبولَ
/// الاتفاقية والتفضيلات والسجلّات (ليست بياناتِ منشأة). <b>ولا يمسّ قاعدةَ PostgreSQL على الخادم</b> — فالمعالجُ يطلب قاعدةً جديدةً أو يتبنّى القائمة بسؤال.</para>
/// </summary>
public static class InstallationStamp
{
    public enum Verdict
    {
        /// <summary>لا بياناتَ سابقة أو البصمةُ نفسُها — يُتابَع بلا سؤال.</summary>
        Continue,

        /// <summary>تنصيبٌ مختلفٌ بإصدارٍ أحدث — ترقيةٌ عاديّة تُتابَع بلا سؤال وتُسجَّل.</summary>
        Upgrade,

        /// <summary>يُسأل المستخدم.</summary>
        Ask,
    }

    private const string FileName = "installation.json";

    private sealed record Record(string Stamp, string Version, DateTime AtUtc);

    /// <summary>ما لا يُنقل عند البدء الفارغ: ليست بياناتِ منشأة.</summary>
    private static readonly string[] Kept =
    [
        "license.json", "clock-mark.dat", "eula-accepted.json", "ui-prefs.json", "logs", "vendor-signing", FileName,
    ];

    /// <summary>بصمةُ هذا التنصيب: مسارُ التنفيذيّ + الإصدارُ + تاريخُ كتابته.</summary>
    public static string Current(string executablePath, string version)
    {
        var written = File.Exists(executablePath) ? File.GetLastWriteTimeUtc(executablePath).Ticks : 0L;
        var text = $"{Path.GetFullPath(executablePath).ToLowerInvariant()}|{version}|{written}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    public static Verdict Judge(string root, string stamp, string version)
    {
        // ‏جذرٌ بلا إعدادٍ مكتمل = تنصيبٌ جديدٌ فعلاً: لا شيءَ يُسأل عنه.
        if (!File.Exists(Path.Combine(root, "setup.json")))
            return Verdict.Continue;

        var record = Read(root);
        if (record is null)
            return Verdict.Ask;
        if (string.Equals(record.Stamp, stamp, StringComparison.Ordinal))
            return Verdict.Continue;

        return Version.TryParse(record.Version, out var before) && Version.TryParse(version, out var now) && now > before
            ? Verdict.Upgrade
            : Verdict.Ask;
    }

    /// <summary>يسجّل هذا التنصيبَ في الجذر — بعد أن تقرّر المتابعةُ أو البدءُ الفارغ.</summary>
    public static void Remember(string root, string stamp, string version)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, FileName),
            JsonSerializer.Serialize(new Record(stamp, version, DateTime.UtcNow)));
    }

    /// <summary>ينقل بياناتِ المنشأة السابقةَ إلى <c>previous-&lt;تاريخ&gt;</c> ويُعيد مسارَه — لا حذفَ.</summary>
    public static string StartFresh(string root, DateTime? now = null)
    {
        var archive = Path.Combine(root, $"previous-{(now ?? DateTime.Now):yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(archive);

        foreach (var entry in Directory.EnumerateFileSystemEntries(root).ToList())
        {
            var name = Path.GetFileName(entry);
            if (name.StartsWith("previous-", StringComparison.Ordinal) || Kept.Contains(name, StringComparer.OrdinalIgnoreCase))
                continue;

            var target = Path.Combine(archive, name);
            if (Directory.Exists(entry))
                Directory.Move(entry, target);
            else
                File.Move(entry, target);
        }

        return archive;
    }

    private static Record? Read(string root)
    {
        try
        {
            var path = Path.Combine(root, FileName);
            return File.Exists(path) ? JsonSerializer.Deserialize<Record>(File.ReadAllText(path)) : null;
        }
        catch (JsonException)
        {
            return null;   // ‏سجلٌّ تالفٌ = غيرُ معروف ⟵ يُسأل المستخدم ولا يُخمَّن.
        }
    }
}
