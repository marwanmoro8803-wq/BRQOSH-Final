using System.Text.RegularExpressions;
using Brqosh.Application.Tests.TestSupport;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (CEEF-229 · م2-٤) <b>لا يُطفَأ قياسُ الأداء من نصوص التشغيل — والإطفاءُ يعود بسطرٍ واحد.</b>
///
/// <para><b>ما كان:</b> <c>BRQOSH_SKIP_STRESS=1</c> حُذف من <c>ci.yml</c> ومن الـ<c>runsettings</c>
/// ومن المشاريع في م2-٤ — <b>وبقي في ثلاثة نصوص تشغيل</b>. ومنها
/// <c>run-external-gates.ps1</c> الذي يُعلن أنّه «التحقّق في المستودع»، و<c>sync-agent-status.ps1</c>
/// الذي هو <b>مصدرُ الحقيقة لعدّاد الاختبارات</b> في <c>STATUS.yaml</c>.</para>
///
/// <para><b>والأثرُ ليس في الرقم وحده:</b> النصُّ الأخير كان يكتب في الحالة المنشورة
/// <c>BRQOSH_SKIP_STRESS: "1"</c> — أي <b>يُعلن عن نفسه ما لم يعد صحيحاً</b> بعد رفع التخطّي.</para>
///
/// <para>والحارسُ هنا يمنع العودة: أيُّ نصٍّ يضبط المتغيّر إلى <c>"1"</c> يُسقطه، ما لم
/// يُقَرّ بسببٍ مكتوب.</para>
/// </summary>
public class PerformanceGatesStayOnTests
{
    private static readonly Regex TurnsGateOff = new(
        Regex.Escape(StressFactAttribute.SkipEnvironmentVariable) + @"\s*[:=]\s*""?1""?",
        RegexOptions.Compiled);

    /// <summary>مواضعُ مُقَرٌّ بها — ولكلٍّ سببٌ مكتوب. وفارغةٌ اليوم عمداً.</summary>
    private static readonly Dictionary<string, string> Declared = new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void NoScript_TurnsOffThePerformanceGates()
    {
        var offenders = new List<string>();

        foreach (var file in ScriptFiles())
        {
            var name = Path.GetFileName(file);
            if (Declared.ContainsKey(name))
                continue;

            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                var code = line.Trim();

                // ‏تعليقٌ يشرح الحذف ليس إطفاءً — وحارسٌ يقرأ التعليقات يقرأ الشرحَ عطلاً.
                if (code.StartsWith('#') || code.StartsWith("//"))
                    continue;

                if (TurnsGateOff.IsMatch(code))
                    offenders.Add($"{name}:{lineNumber}  {code}");
            }
        }

        Assert.True(offenders.Count == 0,
            "نصُّ تشغيلٍ يُطفئ بوّابتَي الأداء — فيقيس أقلَّ ممّا يدّعي:\n  "
            + string.Join("\n  ", offenders)
            + "\n\nأزِل السطر، أو أقرِر الموضعَ هنا بسببٍ مكتوب. وتشغيلُهما نحو 44 ثانية.");
    }

    /// <summary><b>حارسٌ للحارس:</b> إقرارٌ لملفٍّ غير موجود يجعل القائمةَ تكذب.</summary>
    [Fact]
    public void NoDeclaration_OutlivesItsFile()
    {
        var names = ScriptFiles().Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        var phantom = Declared.Keys.Where(k => !names.Contains(k)).ToList();

        Assert.True(phantom.Count == 0, "إقرارٌ لملفٍّ غير موجود: " + string.Join("، ", phantom));
    }

    private static IEnumerable<string> ScriptFiles()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        foreach (var folder in new[] { "scripts", ".github" })
        {
            var path = Path.Combine(root, folder);
            if (!Directory.Exists(path))
                continue;

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file);
                if (extension is ".ps1" or ".yml" or ".yaml" or ".cmd" or ".sh")
                    yield return file;
            }
        }
    }
}
