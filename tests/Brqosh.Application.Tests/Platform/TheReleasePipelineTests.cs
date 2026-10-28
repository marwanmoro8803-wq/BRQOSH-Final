using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (ح-١٤٢ · installer-deployment-ops#2 · commercial-readiness#9) <b>خطُّ الإصدار.</b>
///
/// <para><b>ما كان:</b> CI لا يبني MSI، والحزمةُ الوحيدةُ بناءٌ محلّيٌّ بالرقم 1.0.0 بلا سجلّ
/// تغييراتٍ ولا قناة. وقراراتُ الإصدار النقيّةُ (الرقم، والملاحظات) في <c>scripts/release-lib.ps1</c>
/// تُقاس هنا بتشغيلها؛ والبناءُ الحقيقيُّ مقيسٌ بتشغيل <c>scripts/release.ps1</c> نفسِه.</para>
/// </summary>
public class TheReleasePipelineTests : IDisposable
{
    private readonly string _changelog = Path.Combine(Path.GetTempPath(), $"brqosh-changelog-{Guid.NewGuid():N}.md");

    public void Dispose()
    {
        if (File.Exists(_changelog)) File.Delete(_changelog);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TheVersionIsThePrefixAndTheRunNumber_AndTheNotesAreItsSection()
    {
        File.WriteAllText(_changelog, "# x\n\n## 1.1.8\n\n- لاحق\n\n## 1.1.7\n\n- إصلاحٌ أمنيّ\n- تحسينٌ في الطباعة\n\n## 1.1.6\n\n- سابق\n");

        var r = Describe(7);

        Assert.Equal(0, r.Exit);
        using var json = JsonDocument.Parse(r.Stdout.Trim());
        Assert.Equal("1.1.7", json.RootElement.GetProperty("Version").GetString());
        Assert.Equal("- إصلاحٌ أمنيّ\n- تحسينٌ في الطباعة", json.RootElement.GetProperty("Notes").GetString());
    }

    /// <summary><b>لا إصدارَ بلا قسمه في السجلّ</b> — والملاحظاتُ الموقَّعةُ منه.</summary>
    [Fact]
    public void AVersionWithoutAChangelogSection_IsRefused()
    {
        File.WriteAllText(_changelog, "## 1.1.6\n\n- سابق\n");

        var r = Describe(7);

        Assert.Equal(2, r.Exit);
        Assert.Contains("1.1.7", r.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptySection_IsRefused()
    {
        File.WriteAllText(_changelog, "## 1.1.7\n\n\n## 1.1.6\n\n- سابق\n");

        Assert.Equal(2, Describe(7).Exit);
    }

    /// <summary><b>والصفرُ رقمُ البناء المحلّيّ</b> — لا يدّعي أنّه إصدار.</summary>
    [Fact]
    public void BuildNumberZero_IsNotARelease()
    {
        File.WriteAllText(_changelog, "## 1.1.0\n\n- شيء\n");

        Assert.Equal(2, Describe(0).Exit);
    }

    /// <summary>
    /// <b>سيرُ الإصدار يُشغّل السكربتَ نفسَه برقم تشغيلته</b>، يدويّاً أو بوسم — لا عند كلّ دفع،
    /// والمفتاحُ يُحذف في <c>finally</c>.
    /// </summary>
    [Fact]
    public void TheReleaseWorkflow_RunsTheSameScript_OnDemandOnly()
    {
        var yml = File.ReadAllText(Path.Combine(RepositoryRoot(), ".github", "workflows", "release.yml"));

        Assert.Contains("./scripts/release.ps1 -BuildNumber ${{ github.run_number }}", yml, StringComparison.Ordinal);
        Assert.Contains("workflow_dispatch:", yml, StringComparison.Ordinal);
        Assert.Contains("tags: ['v*']", yml, StringComparison.Ordinal);
        Assert.DoesNotContain("branches:", yml, StringComparison.Ordinal);
        Assert.Contains("dotnet tool install --global wix --version 5.0.2", yml, StringComparison.Ordinal);

        var cleanup = yml[yml.IndexOf("finally {", StringComparison.Ordinal)..];
        Assert.Contains("Remove-Item $key", cleanup, StringComparison.Ordinal);
        Assert.Contains("if-no-files-found: error", yml, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReleaseScript_PublishesAndVerifiesTheChannel()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot(), "scripts", "release.ps1"));

        Assert.Contains("installer\\build-msi.ps1') -BuildNumber $BuildNumber", script, StringComparison.Ordinal);
        Assert.Contains("-- publish $named $version $UpdateKey $Channel --notes $notes", script, StringComparison.Ordinal);
        Assert.Contains("-- verify (Join-Path $Channel 'manifest.json') $version", script, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(RepositoryRoot(), "CHANGELOG.md")));
    }

    /// <summary>
    /// <b>واستيرادُ المكتبة بالنقطة لا يدهس متغيّراتِ المستدعي.</b> كانت للمكتبة كتلةُ <c>param()</c>
    /// تُشغَّل في نطاق <c>release.ps1</c>، فصار رقمُ الإصدار صفراً ورُفض — أمسكه أوّلُ تشغيلٍ حقيقيّ.
    /// </summary>
    [Fact]
    public void DotSourcingTheLibrary_KeepsTheCallersBuildNumber()
    {
        var lib = Path.Combine(RepositoryRoot(), "scripts", "release-lib.ps1").Replace("'", "''");
        var psi = new ProcessStartInfo("pwsh") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-Command", $"$BuildNumber = 7; $Changelog = 'x'; . '{lib}'; \"$BuildNumber|$Changelog\"" })
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        Assert.True(process.WaitForExit(60_000));
        Assert.Equal("7|x", stdout.Trim());
    }

    private sealed record Run(int Exit, string Stdout, string Stderr);

    private Run Describe(int buildNumber)
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
                     "-NoProfile", "-NonInteractive", "-File", Path.Combine(RepositoryRoot(), "scripts", "release-describe.ps1"),
                     "-BuildNumber", buildNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                     "-Changelog", _changelog, "-Props", Path.Combine(RepositoryRoot(), "Directory.Build.props"),
                 })
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(60_000), "release-describe.ps1 لم تنتهِ في 60 ثانية");
        return new Run(process.ExitCode, stdout, stderr);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }
}
