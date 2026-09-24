using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (ح-٦٣ · evidence-integrity#2) <b>بوّابةُ التغطية تعدّ السطرَ مرّةً واحدة — وكانت تعدّ أسطرَ Domain مرّتين.</b>
///
/// <para><b>ما كان:</b> مفتاحُ الاتّحاد في <c>check-coverage.ps1</c> هو <c>filename</c> كما يكتبه coverlet —
/// <b>نسبيّاً إلى <c>&lt;source&gt;</c>، والجذرُ يختلف بين التقارير</b>. <c>Domain.Tests</c> لا يُجهّز إلا
/// <c>Brqosh.Domain</c> فجذرُه <c>src\Brqosh.Domain\</c>، والمشاريعُ الأخرى جذرُها <c>src\</c> — فالسطرُ الواحدُ
/// مفتاحان: <c>Manufacturing\WorkOrder.cs#12</c> و<c>Brqosh.Domain\Manufacturing\WorkOrder.cs#12</c>.
/// قِيس على تقارير الشجرة: مقامُ Domain <b>12024 = 2 × 6012</b>، والنسبةُ 54.06% والصادقةُ 81.50%.</para>
///
/// <para><b>والضررُ ليس الرقمَ وحدَه:</b> السقّاطةُ (46%) عُويِرت على الرقم الخاطئ، فلو ضاع تقريرُ
/// <c>Domain.Tests</c> كلُّه لقرأت البوّابةُ ما تعطيه المشاريعُ الأخرى وحدَها (78.28%) — <b>ومرّت</b>.</para>
///
/// <para>والاختبارُ يُشغّل <b>البوّابةَ نفسَها</b> (<c>pwsh</c>) على تقاريرَ اصطناعيّةٍ بشكل coverlet —
/// لا نسخةً من منطقها — فما يُقاس هو ما يُشغّله CI.</para>
/// </summary>
public class TheCoverageGateCountsEachLineOnceTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "brqosh-covgate-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void TheSameLineUnderTwoSourceRoots_IsCountedOnce()
    {
        // ‏تقريرُ Domain.Tests: جذرُه مجلّدُ التجميعة، فالاسمُ بلا «Brqosh.Domain\».
        WriteReport("domain-tests", Src + @"Brqosh.Domain\",
            new Cls("Brqosh.Domain", @"Manufacturing\WorkOrder.cs", FirstHit: 1, LastHit: 60));
        // ‏وتقريرُ مشروعٍ آخر: جذرُه src\ — السطورُ نفسُها باسمٍ أطول، وتغطيتُها تُكمل الأولى.
        WriteReport("application-tests", Src, Healthy()
            .Append(new Cls("Brqosh.Domain", @"Brqosh.Domain\Manufacturing\WorkOrder.cs", FirstHit: 41, LastHit: 100))
            .ToArray());

        var run = RunGate();

        Assert.True(run.ExitCode == 0, "مجموعةٌ سليمةٌ بجذرين يجب أن تمرّ:\n" + run.Output);
        Assert.Equal((100, 100), Row(run.Output, "Brqosh.Domain"));   // ‏لا 120 / 200
    }

    /// <summary>
    /// <b>السقّاطةُ فوق ما تعطيه المشاريعُ الأخرى وحدَها</b> — وإلّا ضاع مشروعُ اختبارٍ كاملٌ ولم تصرخ.
    /// الرقمُ مقيسٌ على الشجرة 2026-09-24 بإسقاط تقارير <c>Domain.Tests</c>: 78.28%.
    /// وخفضُ سقّاطة Domain تحته يُسقط هذا الاختبار — فهو حارسُ السقّاطة أيضاً.
    /// </summary>
    [Fact]
    public void LosingTheDomainTestsReport_FailsTheGate()
    {
        WriteReport("application-tests", Src, Healthy()
            .Append(new Cls("Brqosh.Domain", @"Brqosh.Domain\Manufacturing\WorkOrder.cs", FirstHit: 1, LastHit: 78))
            .ToArray());

        var run = RunGate();

        Assert.True(run.ExitCode != 0, "Domain بلا تقرير Domain.Tests يجب أن يُسقط البوّابة:\n" + run.Output);
        Assert.Contains("Assemblies below their floor: Brqosh.Domain", run.Output, StringComparison.Ordinal);
    }

    /// <summary>تقريرٌ بلا جذرٍ واحدٍ <b>يُرفض ولا يُخمَّن</b> — والتخمينُ بالاسم العاري هو العطلُ نفسُه.</summary>
    [Fact]
    public void AReportWithoutOneSourceRoot_IsRefused_NotGuessed()
    {
        WriteReport("no-root", source: null, Healthy()
            .Append(new Cls("Brqosh.Domain", @"Brqosh.Domain\Manufacturing\WorkOrder.cs", FirstHit: 1, LastHit: 100))
            .ToArray());

        var run = RunGate();

        Assert.True(run.ExitCode != 0, "تقريرٌ بلا <source> يجب أن يُرفض:\n" + run.Output);
        Assert.Contains("<source>", run.Output, StringComparison.Ordinal);
    }

    // ── الأدوات ─────────────────────────────────────────────────────────────────

    private sealed record Cls(string Package, string FileName, int FirstHit, int LastHit, int Lines = 100);

    /// <summary>جذرٌ اصطناعيّ — لا يلزم أن يوجد: المفتاحُ يُبنى من النصّ لا من القرص.</summary>
    private string Src => Path.Combine(_dir, "src") + Path.DirectorySeparatorChar;

    /// <summary>التجميعاتُ الأربعُ الأخرى فوق سقّاطاتها بهامشٍ واسع — فلا يُسقطها رفعُ سقّاطةٍ لاحقاً.</summary>
    private static IEnumerable<Cls> Healthy() =>
        new[] { "Brqosh.Infrastructure", "Brqosh.Application", "Brqosh.Api", "Brqosh.Desktop" }
            .Select(a => new Cls(a, a + @"\A.cs", FirstHit: 1, LastHit: 99));

    private void WriteReport(string folder, string? source, params Cls[] classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<coverage>");
        if (source is not null)
            sb.AppendLine($"  <sources><source>{source}</source></sources>");
        sb.AppendLine("  <packages>");
        foreach (var package in classes.GroupBy(c => c.Package))
        {
            sb.AppendLine($"    <package name=\"{package.Key}\"><classes>");
            foreach (var c in package)
            {
                sb.AppendLine($"      <class filename=\"{c.FileName}\"><lines>");
                for (var i = 1; i <= c.Lines; i++)
                    sb.AppendLine($"        <line number=\"{i}\" hits=\"{(i >= c.FirstHit && i <= c.LastHit ? 1 : 0)}\" />");
                sb.AppendLine("      </lines></class>");
            }
            sb.AppendLine("    </classes></package>");
        }
        sb.AppendLine("  </packages>");
        sb.AppendLine("</coverage>");

        var path = Path.Combine(_dir, "results", folder, "coverage.cobertura.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
    }

    private (int ExitCode, string Output) RunGate()
    {
        var psi = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in new[]
                 {
                     "-NoProfile", "-NonInteractive", "-File",
                     Path.Combine(RepositoryRoot(), "scripts", "check-coverage.ps1"),
                     "-ResultsDir", Path.Combine(_dir, "results"),
                 })
            psi.ArgumentList.Add(arg);
        // ‏البوّابةُ تُلحق جدولَها بملخّص خطوة CI إن وجدت المتغيّر — وجدولٌ اصطناعيٌّ هناك يكذب.
        psi.Environment.Remove("GITHUB_STEP_SUMMARY");

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("check-coverage.ps1 لم تنتهِ في 120 ثانية");
        }

        return (process.ExitCode, stdout.Result + "\n" + stderr.Result);
    }

    private static (int Covered, int Total) Row(string output, string assembly)
    {
        var m = Regex.Match(output, Regex.Escape(assembly) + @"\s+[\d.]+%\s+(\d+) / (\d+)");
        Assert.True(m.Success, $"لا صفَّ لـ{assembly} في مخرج البوّابة:\n{output}");
        return (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
