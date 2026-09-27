namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٧٣ · <c>evidence-integrity#4</c>) <b>كلُّ بوّابةٍ في ملفٍّ واحد، يُشغّله خطُّها واختبارُ حساسيّتها
/// معاً.</b>
///
/// <para><b>ما كان:</b> «صفرُ متخطًّى» نصٌّ داخل <c>ci.yml</c>، و«كم نُفِّذ» أسطرٌ داخل <c>run-external-gates.ps1</c>،
/// ودفترُ الإصدارات أسطرٌ داخل <c>build-msi.ps1</c> — <b>و<c>verify-gates.ps1</c> يختبر نسخاً منها تُكتب بيدٍ فيه</b>.
/// قِيس: كُسرت بوّابةُ CI الحقيقيّة (التخطّي لا يُسقطها) <b>فبقي اختبارُ الحساسيّة أخضرَ برمز 0</b> — يعضّ نسختَه لا
/// البوّابة. وحالةُ الدفتر كانت تُلحق رقماً بالدفتر المُتعقَّب نفسِه ثمّ تُعيده.</para>
///
/// <para><b>والحارس:</b> التوقيعُ البرمجيُّ لكلّ بوّابة (<c>$counters.notExecuted</c> · <c>$counters.total</c> ·
/// <c>$shipped -contains</c>) لا يظهر إلّا في سكربتها — <b>فنسخةٌ ثانيةٌ في أيّ ملفٍّ تُسقطه</b>؛ وخطُّها يستدعيه؛
/// واختبارُ الحساسيّة يُشغّله ولا يمسّ الدفترَ المُتعقَّب. <b>وسلوكُها يُقاس في <c>verify-gates.ps1</c> نفسِه</b> —
/// بعيبٍ يعضّ وبمدخلٍ نظيفٍ يمرّ.</para>
/// </summary>
public class TheSensitivityTestRunsTheRealGatesTests
{
    private const string SensitivityTest = "scripts/ops/verify-gates.ps1";

    public static TheoryData<string, string, string> Gates => new()
    {
        { "scripts/assert-no-skipped-tests.ps1", "$counters.notExecuted", ".github/workflows/ci.yml" },
        { "scripts/assert-tests-executed.ps1", "$counters.total", "scripts/ops/run-external-gates.ps1" },
        { "scripts/assert-version-not-shipped.ps1", "$shipped -contains", "installer/build-msi.ps1" },
    };

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

    private static string Text(string relative) =>
        File.ReadAllText(Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>كلُّ ملفّات السكربتات وخطوطِ CI — حيث يمكن أن تُكتب بوّابةٌ أو نسخةٌ منها.</summary>
    private static IEnumerable<string> PipelineFiles()
    {
        var root = RepoRoot();
        return new[] { (".github", "*.yml"), ("scripts", "*.ps1"), ("installer", "*.ps1") }
            .SelectMany(x => Directory.EnumerateFiles(Path.Combine(root, x.Item1), x.Item2, SearchOption.AllDirectories))
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'));
    }

    /// <summary>منطقُ البوّابة لا يُكتب إلّا في سكربتها — نسخةٌ في أيّ ملفٍّ آخر تنحرف عنه ولا يراها أحد.</summary>
    [Theory]
    [MemberData(nameof(Gates))]
    public void TheGatesLogic_LivesInItsScriptAlone(string script, string signature, string pipeline)
    {
        _ = pipeline;
        var holders = PipelineFiles()
            .Where(f => Text(f).Contains(signature, StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        // ‏شاهدُ ضبط: السكربتُ نفسُه يُرى حاملاً توقيعَه — وإلّا فـ«لا نسخة» تمرّ على ماسحٍ أعمى أو توقيعٍ تغيّر.
        Assert.Contains(script, holders);
        Assert.True(holders.Count == 1,
            $"‏«{signature}» مكتوبٌ في غير {script}: {string.Join(" · ", holders.Where(h => h != script))} — استدعِ السكربتَ لا تنسخه.");
    }

    /// <summary>الخطُّ الذي يُشغّل البوّابةَ في الحقيقة يستدعي سكربتَها.</summary>
    [Theory]
    [MemberData(nameof(Gates))]
    public void ThePipeline_CallsTheGatesScript(string script, string signature, string pipeline)
    {
        _ = signature;
        Assert.Contains(Path.GetFileName(script), Text(pipeline), StringComparison.Ordinal);
    }

    /// <summary>واختبارُ الحساسيّة يُشغّل السكربتَ نفسَه — ولا يمسّ دفترَ الإصدارات المُتعقَّب.</summary>
    [Theory]
    [MemberData(nameof(Gates))]
    public void TheSensitivityTest_RunsTheGatesScript(string script, string signature, string pipeline)
    {
        _ = (signature, pipeline);
        var text = Text(SensitivityTest);

        Assert.Contains(Path.GetFileName(script), text, StringComparison.Ordinal);
        Assert.DoesNotContain(@"installer\shipped-versions.txt", text, StringComparison.Ordinal);
    }
}
