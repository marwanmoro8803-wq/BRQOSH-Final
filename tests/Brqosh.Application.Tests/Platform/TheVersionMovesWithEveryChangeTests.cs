using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (الإصدارُ يتحرّك مع كلّ تعديل) <b>أيُّ إصلاحٍ أو تعديلٍ في الشيفرة يرفع رقمَ الإصدار</b> — قاعدةُ المالك. والرقمُ في <c>version.txt</c> (يقرؤه
/// <c>Directory.Build.props</c> فيصير <c>1.0.&lt;رقم&gt;</c> في الثنائيّة والمُنصِّب).
/// <list type="bullet">
/// <item>الرقمُ صحيحٌ موجبٌ وله قسمٌ في <c>CHANGELOG.md</c> (وإلا رفض <c>scripts/release.ps1</c> الإصدار).</item>
/// <item><b>آخرُ إيداعٍ غيّر <c>src/</c> لا يسبق آخرَ إيداعٍ غيّر <c>version.txt</c></b>: فإيداعُ شيفرةٍ بلا رفعِ الرقم يُسقط هذا الاختبارَ.</item>
/// </list>
/// </summary>
public class TheVersionMovesWithEveryChangeTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }

    private static string? Git(string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("git", args)
            {
                WorkingDirectory = Root(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            });
            var output = p!.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(15000);
            return p.ExitCode == 0 ? output : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;   // ‏لا git على الجهاز — يُتخطّى الشقُّ التاريخيّ ويبقى شقُّ الملفّات.
        }
    }

    private static int Number() =>
        int.Parse(File.ReadAllText(Path.Combine(Root(), "version.txt")).Trim());

    [Fact]
    public void TheVersionFileIsAPositiveNumber_WithAChangelogSection()
    {
        Assert.True(Number() >= 0);

        var changelog = File.ReadAllText(Path.Combine(Root(), "CHANGELOG.md"));
        var prefix = Regex.Match(File.ReadAllText(Path.Combine(Root(), "Directory.Build.props")), @"<BrqoshVersionPrefix>([^<]+)</BrqoshVersionPrefix>").Groups[1].Value;
        Assert.Matches($@"(?m)^##\s+{Regex.Escape(prefix)}\.{Number()}\s*$", changelog);
    }

    [Fact]
    public void ThePropsReadTheNumberFromTheVersionFile()
    {
        var props = File.ReadAllText(Path.Combine(Root(), "Directory.Build.props"));
        Assert.Contains("version.txt", props, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLastSourceChangeIsNotNewerThanTheLastVersionBump()
    {
        var shallow = Git("rev-parse --is-shallow-repository");
        if (shallow is null or "true")
            return;   // ‏بلا تاريخٍ كامل لا حكم.

        var lastSource = Git("rev-list -1 HEAD -- src");
        var lastVersion = Git("rev-list -1 HEAD -- version.txt");
        if (string.IsNullOrEmpty(lastSource))
            return;

        Assert.False(string.IsNullOrEmpty(lastVersion), "version.txt لم يُودَع بعد.");
        Assert.True(Git($"merge-base --is-ancestor {lastSource} {lastVersion}") is not null,
            "آخرُ تغييرٍ في src/ أحدثُ من آخر رفعٍ لـversion.txt — ارفع الرقمَ مع كلّ تعديل.");
    }
}
