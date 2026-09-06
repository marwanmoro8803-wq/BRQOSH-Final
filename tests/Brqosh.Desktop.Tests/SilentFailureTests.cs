using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-231 · O-36) <b>عطلٌ يُبتلع صامتاً في المستودع كلِّه — لا في ملفٍّ واحد.</b>
///
/// <para><b>ما كان:</b> أُغلق <c>ط5-١٠</c> بحارسٍ يقيس <c>BrqoshSchemaUpdater</c> وحدَه —
/// أربعةَ عشرَ موضعاً من اثنين وعشرين. <b>وحارسٌ يُغلق ملفّاً يترك صنفَه مفتوحاً</b>،
/// وهو درسُ هذه الحملة نفسِه مُعاداً: ما يُقاس في موضعٍ يعود في الموضع الذي لم يُقَس.</para>
///
/// <para><b>وهذا يقيس القاعدةَ على <c>src</c> كلِّها</b>: لا <c>catch</c> فارغاً — أو
/// إقرارٌ بملفّه وعدده <b>وسببٍ مكتوب</b>. <b>والإقرارُ قرارٌ يُوقَّع لا تسكيتٌ للحارس</b>:
/// موضعٌ يُضاف إلى الجدول بلا سببٍ يصمد هو العطلُ نفسُه بثوبٍ جديد.</para>
///
/// <para><b>والتعليقاتُ تُسقط قبل البحث</b> — فذِكرُ النمط في شرحٍ له ليس وقوعاً له، وهو
/// المطبُّ الذي أوقع كاتبَ الحارس السابق في يوم كتابته.</para>
/// </summary>
public class SilentFailureTests
{
    /// <summary>
    /// مواضعُ ابتلاعٍ <b>مقصودة</b> — ولكلٍّ سببٌ مكتوب وعددٌ مقيس.
    ///
    /// <para><b>والعددُ جزءٌ من الإقرار</b>: ملفٌّ أُقرّ له موضعان ثمّ صار فيه ثلاثة
    /// <b>يعني موضعاً جديداً دخل تحت غطاء إقرارٍ قديم</b>.</para>
    /// </summary>
    private static readonly Dictionary<string, (int Count, string Reason)> Declared =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [@"Brqosh.Application\Setup\AppEnvironment.cs"] =
                (2, "قراءةُ بيئةِ التشغيل قبل أن يوجد سجلّ — وسياسةُ جهازٍ تمنع القراءة "
                  + "حالةٌ متوقَّعة، والافتراضُ الآمن مكتوبٌ بعدها."),

            [@"Brqosh.Infrastructure\Platform\PersistingDomainEventPublisher.cs"] =
                (1, "حفظُ سببِ الفشل داخل مُعالِج فشلٍ يُسجّل الأصلَ بعده — "
                  + "ورميُ الثاني يُخفي الأوّل، وهو الخبرُ الحقيقيّ."),

            [@"Brqosh.Infrastructure\Operations\CrashLog.cs"] =
                (2, "كاتبُ سجلّ الأعطال نفسُه — ويجري داخل معالج استثناء. "
                  + "واستثناءٌ هنا يُخفي العطلَ الأصليَّ ويستبدل به عطلَ الكاتب، "
                  + "وتدويرُ الملفّات القديمة يفشل ولا يمنع كتابةَ السجلّ وهو الغرض."),

            [@"Brqosh.Infrastructure\Persistence\BrqoshSchemaUpdater.cs"] =
                (1, "كتابةُ تقرير الخطوات المبتلَعة على القرص — وهو نفسُه أداةُ إظهار. "
                  + "فتعذّرُ كتابته لا يمنع الإقلاع، والفشلُ الذي يصفه محفوظٌ في الذاكرة "
                  + "ويُقرأ من شاشة حالة النظام."),

            [@"Brqosh.Infrastructure\Operations\BackupService.cs"] =
                (2, "محوُ الملفّ المرحليّ في finally بعد نسخةٍ نجحت وتُحقّقت ونُقلت باسمها — "
                  + "ورميٌ هنا يُفشل عمليةً تمّت، أو يُخفي سببَ فشلٍ أصليٍّ في طريقه للصعود. "
                  + "وبقاءُ الملفّ لا يضرّ: لاحقتُه لا تُطابق نمطَي البحث فلا يُعدّ نسخةً، "
                  + "ويُمحى في التشغيلة التالية."),

            [@"Brqosh.Infrastructure\Operations\OffsiteBackupService.cs"] =
                (1, "تنظيفُ نسخةٍ قديمةٍ في الوجهة بعد التحقّق من الجديدة — "
                  + "وملفٌّ مقفلٌ يبقى ويُحذف في المرّة التالية. ورميٌ هنا يُفشل نسخةً "
                  + "نُقلت وتُحقّقت بصمتُها بالفعل، فيُقرأ النجاحُ فشلاً."),
        };

    /// <summary>
    /// <b>القياسُ الحاكم: لا <c>catch</c> فارغاً في <c>src</c> — أو إقرارٌ مطابق.</b>
    /// </summary>
    [Fact]
    public void NoSilentCatch_AnywhereInSource()
    {
        var root = FindRepoDirectory("src");
        if (root is null) return;   // ‏المصادرُ غيرُ متاحةٍ في هذا التشغيل.

        // ‏**والالتقاطُ المُنمَّطُ يُحسب**: `catch (IOException) { }` صامتٌ صمتَ
        // ‏`catch { }` سواءً بسواء، **والنمطُ الضيّقُ كان يراه بريئاً**.
        var silent = new Regex(@"catch\s*(?:\([^)]*\))?\s*\{\s*\}", RegexOptions.Compiled);
        var found = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                file.EndsWith(".g.cs", StringComparison.Ordinal) ||
                file.EndsWith(".g.i.cs", StringComparison.Ordinal))
                continue;

            var count = silent.Matches(StripComments(File.ReadAllText(file))).Count;
            if (count == 0) continue;

            found[Relative(root, file)] = count;
        }

        var problems = new List<string>();

        foreach (var (path, count) in found.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!Declared.TryGetValue(path, out var declared))
            {
                problems.Add($"{path} — {count} موضعاً يبتلع فشلاً بلا إقرارٍ ولا تسجيل.");
                continue;
            }

            if (declared.Count != count)
                problems.Add($"{path} — أُقرّ له {declared.Count} والموجودُ {count}: "
                           + "موضعٌ دخل تحت غطاء إقرارٍ قديم، أو إقرارٌ لم يُنظَّف.");
        }

        // ‏**وحارسٌ للحارس**: إقرارٌ لملفٍّ لم يعد فيه ابتلاعٌ يُخفي أنّ العمل تمّ.
        foreach (var path in Declared.Keys.Where(k => !found.ContainsKey(k))
                     .OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            problems.Add($"{path} — إقرارٌ باقٍ ولا ابتلاعَ فيه: أثرُ تنظيفٍ نُسي.");

        Assert.True(problems.Count == 0,
            "ابتلاعُ فشلٍ بلا إقرارٍ ولا تسجيل — والعطلُ يُقرأ نجاحاً حتى يظهر في موضعٍ "
            + "بعيدٍ عن سببه:\n  " + string.Join("\n  ", problems));
    }

    /// <summary>
    /// <b>وكلُّ إقرارٍ يحمل سبباً يصمد — لا كلمةً تُسكِت.</b>
    ///
    /// <para><b>وطولُ السبب ليس معياراً جمالياً</b>: «ignore» و«todo» و«temp» ليست
    /// أسباباً، <b>وهي بالضبط ما يُكتب حين لا يوجد سبب</b>.</para>
    /// </summary>
    [Fact]
    public void EveryDeclaration_CarriesAWrittenReason()
    {
        var weak = Declared
            .Where(d => d.Value.Reason.Trim().Length < 40
                        || d.Value.Reason.Contains("ignore", StringComparison.OrdinalIgnoreCase)
                        || d.Value.Reason.Contains("TODO", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Key)
            .ToList();

        Assert.True(weak.Count == 0,
            "إقراراتٌ بلا سببٍ يصمد — وسطرٌ يُكتب لتسكيت الحارس هو العطلُ نفسُه:\n  "
            + string.Join("\n  ", weak));
    }

    /// <summary>يُسقط التعليقات — <b>فذِكرُ النمط في شرحٍ له ليس وقوعاً له</b>.</summary>
    private static string StripComments(string source)
    {
        var text = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(text, @"//.*?$", " ", RegexOptions.Multiline);
    }

    private static string Relative(string root, string file) =>
        file[(root.Length + 1)..];

    private static string? FindRepoDirectory(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }
}
