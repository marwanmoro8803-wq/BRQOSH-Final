using Brqosh.Infrastructure.Operations;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>data-upgrade-performance#6</c> · <c>installer-deployment-ops#10</c> · قرارُ المالك 2026-10-18)
/// <b>أدواتُ النسخ تُشحن مع البرنامج — والبرنامجُ يجدها.</b>
///
/// <para><b>ما كان:</b> النسخُ المجدولُ يستدعي <c>pg_dump</c> من تثبيت PostgreSQL على الجهاز، والمُنصِّبُ لا يشحنها ولا
/// يفحصها — فلا نسخَ إلّا حيث ثبّت أحدٌ PostgreSQL يدويّاً. والحلُّ نصفان <b>يجب أن يتّفقا على اسم المجلّد</b>: المُنصِّبُ
/// يضعها في <c>pgtools</c> تحت الحمولة، و<see cref="PgToolLocator"/> يفتّش <c>pgtools</c> بجوار التنفيذيّ.</para>
///
/// <para><b>وحدُّ هذه البوّابة:</b> نصفُ المُنصِّب يُقاس <b>بنيةً</b> (كأخواتها في هذا المجلّد) — النسخُ قبل <c>wix build</c>
/// والرفضُ بلا أدوات؛ وبناءُ الحزمة نفسُها يُقاس خارج الاختبارات. ونصفُ البرنامج يُقاس <b>سلوكاً</b> بدرز التفتيش.</para>
/// <para>و<c>SequentialEnv</c> لأنّ اختباراً هنا يُغيّر <c>PATH</c> للعمليّة كلِّها.</para>
/// </summary>
[Collection("SequentialEnv")]
public class TheBackupToolsShipWithTheProgramTests
{
    private const string Script = "installer/build-msi.ps1";

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

    private static string ScriptText()
    {
        var path = Path.Combine(RepoRoot(), Script.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"‏ملفٌّ مفقود: {Script}");
        var text = File.ReadAllText(path);
        Assert.Contains("BRQOSH MSI Build", text, StringComparison.Ordinal);   // شاهدُ ضبط: القارئُ يقرأ السكربتَ نفسَه
        return text;
    }

    [Fact]
    public void TheInstaller_CopiesBothTools_IntoPgtools_BeforeTheMsiIsBuilt()
    {
        var text = ScriptText();

        Assert.Contains("'pg_dump.exe'", text, StringComparison.Ordinal);
        Assert.Contains("'pg_restore.exe'", text, StringComparison.Ordinal);
        Assert.Contains("$PgToolsDir = Join-Path $PublishDir \"pgtools\"", text, StringComparison.Ordinal);

        var copy = text.IndexOf("Copy-Item (Join-Path $PgBin $f) $PgToolsDir", StringComparison.Ordinal);
        var harvest = text.IndexOf("wix build ", StringComparison.Ordinal);
        Assert.True(copy > 0, "‏لا نسخَ للأدوات إلى pgtools");
        Assert.True(copy < harvest, "‏الأدواتُ تُنسخ بعد `wix build` — فلا تدخل الحزمة");
    }

    [Fact]
    public void TheInstaller_RefusesToBuild_WithoutTheTools_OrWithAnOldPgDump()
    {
        var text = ScriptText();

        var missing = text.IndexOf("if ($missingPg.Count -gt 0)", StringComparison.Ordinal);
        Assert.True(missing > 0, "‏لا فحصَ لنقص الأدوات");
        Assert.Contains("exit 1", text[missing..text.IndexOf('}', missing)], StringComparison.Ordinal);
        Assert.Contains("[int]$Matches[1] -lt 17", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheInstaller_ShipsTheToolsLicenceNotices_AsTheirSourceShippedThem()
    {
        var text = ScriptText();

        Assert.Contains("'server_license.txt'", text, StringComparison.Ordinal);
        Assert.Contains("'commandlinetools_3rd_party_licenses.txt'", text, StringComparison.Ordinal);
        Assert.Contains("foreach ($f in $PgNotices) { Copy-Item", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheProgram_LooksInPgtools_BesideItsExecutable()
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "pgtools"), PgToolLocator.BundledDirectory);
    }

    [Fact]
    public void TheBundledTool_IsSearchedFirst_AndChosenWhenItIsTheNewest()
    {
        var bundled = Path.Combine(Path.GetTempPath(), $"brqosh-pgtools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bundled);
        var tool = Path.Combine(bundled, "pg_dump.exe");
        File.WriteAllBytes(tool, []);
        try
        {
            // ‏المشحونُ ٩٩ وكلُّ ما عداه على هذا الجهاز ١ — فالاختيارُ لا يتعلّق بما ثُبّت هنا.
            var found = PgToolLocator.Find("pg_dump", bundled,
                path => string.Equals(path, tool, StringComparison.OrdinalIgnoreCase) ? new Version(99, 0) : new Version(1, 0),
                out var searched);

            Assert.NotNull(found);
            Assert.Equal(tool, found!.Path);
            Assert.Equal(new Version(99, 0), found.Version);
            Assert.Equal(bundled, searched[0]);
        }
        finally
        {
            Directory.Delete(bundled, recursive: true);
        }
    }

    [Fact]
    public void ANewerLocalInstall_StillWinsOverTheBundledTool()
    {
        var bundled = Path.Combine(Path.GetTempPath(), $"brqosh-pgtools-{Guid.NewGuid():N}");
        var local = Path.Combine(Path.GetTempPath(), $"brqosh-pglocal-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bundled);
        Directory.CreateDirectory(local);
        var tool = Path.Combine(bundled, "pg_dump.exe");
        var newer = Path.Combine(local, "pg_dump.exe");
        File.WriteAllBytes(tool, []);
        File.WriteAllBytes(newer, []);
        var path = Environment.GetEnvironmentVariable("PATH");
        try
        {
            // ‏الخادمُ رُقّي: تثبيتٌ محلّيٌّ أحدثُ على المسار — والمشحونُ أقدمُ منه لا يُفرض عليه.
            Environment.SetEnvironmentVariable("PATH", local + Path.PathSeparator + path);
            var found = PgToolLocator.Find("pg_dump", bundled,
                p => string.Equals(p, tool, StringComparison.OrdinalIgnoreCase) ? new Version(17, 0)
                   : string.Equals(p, newer, StringComparison.OrdinalIgnoreCase) ? new Version(98, 0)
                   : new Version(1, 0),
                out _);

            Assert.Equal(newer, found!.Path);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", path);
            Directory.Delete(bundled, recursive: true);
            Directory.Delete(local, recursive: true);
        }
    }
}
