using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٧٤ · كشفه العمل) <b>دليلُ حساسيّة البوّابات يُسمّي كلَّ حالةٍ في السكربت — ولا حالةً غيرَها.</b>
///
/// <para><b>ما كان:</b> <c>docs/ops/GATE-SENSITIVITY.md</c> يقول «بوّابةٌ بلا حالةٍ في هذا الملفّ = بوّابةٌ غيرُ مُتحقَّقٍ من
/// حساسيّتها»، <b>وحالةُ <c>coverage-two-roots</c> أُضيفت إلى السكربت في ح-٦٣ ولا صفَّ لها فيه</b>. وقد عالج ن-٥٣ العددَ في
/// العنوان («لا عددَ في العنوان — العددُ يُقرأ من الجدول») ولم يعالج الجدولَ نفسَه: <b>والجدولُ ذاكرةٌ لا تُحدَّث إلّا إن
/// تذكّرها أحد</b> — كرأس <c>OPEN_ITEMS</c> قبل ح-٦٥.</para>
///
/// <para><b>والحارس:</b> أسماءُ <c>$cases['…']</c> في السكربت = أسماءُ الصفوف تحت «## الحالات» في الدليل، في الاتّجاهين.</para>
/// </summary>
public class TheGateSensitivityGuideListsEveryCaseTests
{
    private const string Script = "scripts/ops/verify-gates.ps1";
    private const string Guide = "docs/ops/GATE-SENSITIVITY.md";
    private const string Section = "## الحالات";

    private static readonly Regex CaseDeclaration = new(@"^\$cases\['([a-z0-9-]+)'\]\s*=");
    private static readonly Regex GuideRow = new(@"^\| `([a-z0-9-]+)` \|");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }

    private static string[] Lines(string relative) =>
        File.ReadAllText(Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar)))
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .ToArray();

    private static List<string> Cases() =>
        Lines(Script).Select(l => CaseDeclaration.Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();

    private static List<string> Rows()
    {
        var lines = Lines(Guide);
        var start = Array.FindIndex(lines, l => l.StartsWith(Section, StringComparison.Ordinal));
        Assert.True(start >= 0, $"‏لا قسمَ «{Section}» في {Guide}.");
        var end = Array.FindIndex(lines, start + 1, l => l.StartsWith("## ", StringComparison.Ordinal));

        return lines[start..(end < 0 ? lines.Length : end)]
            .Select(l => GuideRow.Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();
    }

    /// <summary>كلُّ حالةٍ في السكربت لها صفٌّ في الدليل، وكلُّ صفٍّ حالةٌ موجودة.</summary>
    [Fact]
    public void TheGuide_NamesEveryCase_AndNoOther()
    {
        var cases = Cases();
        var rows = Rows();

        // ‏شاهدا ضبط: القارئان يريان حالاتٍ وصفوفاً فعلاً — وإلّا فتساوي مجموعتين فارغتين يمرّ على أيّ دليل.
        Assert.True(cases.Count >= 10, $"‏قارئُ {Script} لا يرى حالات ({cases.Count}).");
        Assert.True(rows.Count >= 10, $"‏قارئُ {Guide} لا يرى صفوفاً ({rows.Count}).");

        var missing = cases.Except(rows, StringComparer.Ordinal).ToList();
        var phantom = rows.Except(cases, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0 && phantom.Count == 0,
            $"‏حالاتٌ بلا صفٍّ في الدليل: [{string.Join(", ", missing)}] · صفوفٌ بلا حالة: [{string.Join(", ", phantom)}]");
    }

    /// <summary>ولا حالةَ تُسمّى مرّتين — في السكربت (قاموسٌ يدهس الأولى صامتاً) ولا في الدليل.</summary>
    [Fact]
    public void NoCase_IsNamedTwice()
    {
        var twiceInScript = Cases().GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        var twiceInGuide = Rows().GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.True(twiceInScript.Count == 0, $"‏حالةٌ مُعرَّفةٌ مرّتين في {Script} — الثانيةُ تدهس الأولى: {string.Join(", ", twiceInScript)}");
        Assert.True(twiceInGuide.Count == 0, $"‏صفٌّ مكرّرٌ في {Guide}: {string.Join(", ", twiceInGuide)}");
    }
}
