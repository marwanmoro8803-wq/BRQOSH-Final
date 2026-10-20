using System.Diagnostics;
using System.Text;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Setup;

namespace Brqosh.Application.Tests.Setup;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>installer-deployment-ops#13</c> · قرارُ المالك 2026-10-18) <b>اتّفاقيّةُ الترخيص تُقبل بنصّها — والمسوّدةُ لا تُشحن.</b>
///
/// <para>القبولُ مربوطٌ ببصمة النصّ فيُسأل عن نصٍّ تغيّر؛ والنصُّ المشحونُ مسوّدةٌ موسومةٌ حتى يعتمده المستشارُ القانونيّ،
/// <b>وخطُّ الإصدار يرفضه ما دام الوسمُ قائماً</b> (<c>Assert-BrqoshEulaFinal</c>).</para>
/// </summary>
public class TheLicenceAgreementIsAcceptedBeforeUseTests : IDisposable
{
    private readonly string _root;

    public TheLicenceAgreementIsAcceptedBeforeUseTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "brqosh-eula-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, _root);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, null);
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Acceptance_IsForThisTextOnly()
    {
        Assert.False(EulaAcceptance.IsAccepted("النصّ الأوّل"));

        EulaAcceptance.Accept("النصّ الأوّل");

        Assert.True(EulaAcceptance.IsAccepted("النصّ الأوّل"));
        Assert.False(EulaAcceptance.IsAccepted("النصّ الأوّل بعد تعديل"));
    }

    [Fact]
    public void AnUnreadableRecord_AsksAgain_InsteadOfFailingStartup()
    {
        EulaAcceptance.Accept("النصّ");
        var record = Path.Combine(_root, "eula-accepted.json");

        using (new FileStream(record, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.False(EulaAcceptance.IsAccepted("النصّ"));

        Assert.True(EulaAcceptance.IsAccepted("النصّ"));
    }

    [Fact]
    public void TheShippedText_IsBilingual_AndStillMarkedAsADraft()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "assets", "legal", "eula.txt"));

        Assert.StartsWith("[DRAFT", text, StringComparison.Ordinal);
        Assert.Contains("End-User Licence Agreement", text, StringComparison.Ordinal);
        Assert.Contains("اتّفاقيّة ترخيص المستخدم النهائيّ", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[DRAFT — UNDER LEGAL REVIEW]\nنصّ", false)]
    [InlineData("", false)]
    [InlineData("اتّفاقيّة معتمدة", true)]
    public void TheReleaseGate_RefusesADraftOrEmptyAgreement(string text, bool passes)
    {
        var eula = Path.Combine(_root, "eula.txt");
        File.WriteAllText(eula, text, new UTF8Encoding(false));
        var lib = Path.Combine(RepositoryRoot(), "scripts", "release-lib.ps1").Replace("'", "''");
        var psi = new ProcessStartInfo("pwsh") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-Command",
                     $"$ErrorActionPreference='Stop'; . '{lib}'; Assert-BrqoshEulaFinal -Eula '{eula.Replace("'", "''")}'" })
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(60_000));
        Assert.Equal(passes, process.ExitCode == 0);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("لم يُعثر على جذر المستودع.");
    }
}
