namespace Brqosh.Domain.Tests;

/// <summary>
/// ‏<b>لا محرفَ تحكّمٍ خاماً في نصٍّ مُودَع — وكان الحارسُ ذاكرةً لا بوّابة.</b>
///
/// <para>‏الهيردوكُ في الصدفة يأكل الشرطةَ الخلفيّة: <c>\u001F</c> يصل محرفاً خاماً،
/// و<c>D:\brqosh-vm</c> يصل <c>D:</c> ثمّ «مسافةٌ للخلف» ثمّ <c>rqosh-vm</c>. <b>والناتجُ لا
/// يُرى</b> — خانةُ كودٍ فارغةٌ في وثيقة، أو مسارٌ ينقصه حرف، أو محرفٌ في المصدر يعيش بين
/// علامتَي اقتباس. <b>ولم يمسكه شيءٌ لأنّ الفحصَ كان درساً في الذاكرة</b> يُجرى على ما يُحرَّر
/// في الإيداع نفسِه لا على الشجرة.</para>
///
/// <para>‏<b>وقِيس قبل هذا الحارس ثلاثةٌ</b>: <c>0x1F</c> في <c>OPEN_ITEMS.md</c> منذ
/// <c>8d2592d</c>، و<c>0x08</c> في دليل هويّة الحزمة منذ <c>13c1661</c>، <b>و<c>0x1F</c> في
/// <c>AuditLogEntry.cs</c> — فاصلُ سلسلة تجزئة سجلّ التدقيق نفسُه</b>، مكتوباً خاماً منذ
/// CEEF-212. فمحرّرٌ يُطهّر محارفَ التحكّم كان سيُغيّر صيغةَ كلّ تجزئةٍ في المنتَج <b>ولا يتغيّر
/// في الملفّ حرفٌ مرئيّ</b>.</para>
///
/// <para>‏والمسموحُ ثلاثة: الجدولةُ ونهايتا السطر. <b>وما يُحتاج إليه من غيرها يُكتب هروباً</b>
/// (<c>'\u001F'</c>) — فيُرى ويُبحث عنه ويبقى بعد أيّ أداة.</para>
/// </summary>
public class NoRawControlCharactersInTheTreeTests
{
    private static readonly string[] Roots = ["src", "tests", "docs", "scripts", "installer", "tools", "dist"];

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".xaml", ".md", ".ps1", ".psm1", ".py", ".sh", ".cmd", ".bat", ".json", ".yml", ".yaml",
        ".csproj", ".props", ".targets", ".wxs", ".wxi", ".sql", ".sln",
    };

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", "TestResults", "node_modules",
    };

    [Fact]
    public void NoTrackedTextFile_CarriesARawControlCharacter()
    {
        var root = FindRepoRoot();
        var files = TextFiles(root).ToList();

        // ‏شاهدا ضبطٍ موجبان — **فماسحٌ لا يمشي في الشجرة يقول «لا شيء» صادقاً وكاذباً معاً**:
        // ‏عددٌ يليق بالمستودع، وملفٌّ بعينه نعلم أنّ المشيَ يبلغه.
        Assert.True(files.Count > 1_000, $"مشى الماسحُ في {files.Count} ملفّاً فقط — المشيُ معطوب.");
        Assert.Contains(files, f => f.EndsWith(
            Path.Combine("src", "Brqosh.Domain", "Security", "AuditLogEntry.cs"), StringComparison.Ordinal));

        var offenders = files
            .SelectMany(f => Offenders(File.ReadAllBytes(f))
                .Select(o => $"{Path.GetRelativePath(root, f)}:{o.Line} (0x{o.Byte:X2})"))
            .ToList();

        Assert.True(offenders.Count == 0,
            "‏محارفُ تحكّمٍ خامٌ في نصوصٍ مُودَعة — **والأرجحُ هيردوكٌ أكل شرطةً خلفيّة**؛ "
            + "اكتب السكربتَ بـWrite ثمّ شغّله، وما يلزم من محرفٍ يُكتب هروباً ('\\u001F'):\n"
            + string.Join("\n", offenders));
    }

    /// <summary>‏والكاشفُ نفسُه يُقاس: يمسك ما يجب ويترك ما يجب.</summary>
    [Fact]
    public void TheDetector_FlagsRawBytes_AndSparesTabsAndLineEnds()
    {
        Assert.Equal([(2, (byte)0x1F)], Offenders("a\r\nb\u001Fc\td\n"u8.ToArray()));
        Assert.Equal([(1, (byte)0x08)], Offenders("D:\brqosh-vm"u8.ToArray()));
        Assert.Empty(Offenders("x\ty\r\nz\n"u8.ToArray()));
        Assert.Empty(Offenders("'\\u001F'"u8.ToArray()));
    }

    private static List<(int Line, byte Byte)> Offenders(byte[] bytes)
    {
        var found = new List<(int, byte)>();
        var line = 1;
        foreach (var b in bytes)
        {
            if (b == (byte)'\n') line++;
            else if (b < 0x20 && b != (byte)'\t' && b != (byte)'\r') found.Add((line, b));
        }

        return found;
    }

    private static IEnumerable<string> TextFiles(string root)
    {
        foreach (var top in Roots)
        {
            var dir = Path.Combine(root, top);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Walk(dir))
                yield return file;
        }

        foreach (var file in Directory.EnumerateFiles(root))
            if (TextExtensions.Contains(Path.GetExtension(file)))
                yield return file;
    }

    private static IEnumerable<string> Walk(string dir)
    {
        foreach (var file in Directory.EnumerateFiles(dir))
            if (TextExtensions.Contains(Path.GetExtension(file)))
                yield return file;

        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            if (SkippedDirectories.Contains(Path.GetFileName(sub))) continue;
            foreach (var file in Walk(sub))
                yield return file;
        }
    }

    private static string FindRepoRoot()
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
}
