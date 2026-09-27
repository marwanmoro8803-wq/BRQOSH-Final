using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>commercial-readiness#12</c>) <b>كلُّ حزمةٍ تُشحن لها إشعارٌ، ولا إشعارَ لحزمةٍ لا تُشحن —
/// والإشعاراتُ تُنسخ مع البرنامج.</b>
///
/// <para><b>ما كان:</b> المُنصِّبُ يحصد مجلّدَ النشر كما هو، <b>ولا ملفَّ إشعاراتٍ فيه</b> — والحزمُ المشحونةُ
/// (QuestPDF وQRCoder وNpgsql وEF Core وSQLitePCLRaw وWebView2 …) تشترط تراخيصُها أن يرافق نصُّها أو إشعارُ
/// حقوقها كلَّ نسخةٍ موزَّعة. <b>فكلُّ نسخةٍ تُباع كانت تُخلّ بشرط الإسناد.</b></para>
///
/// <para><b>والمرجعُ ما يُحمَّل لا ما يُبنى به:</b> <c>Brqosh.Desktop.deps.json</c> — ينسخه مرجعُ المشروع إلى
/// جوار هذه الاختبارات، وهو عينُ ما في مجلّد النشر (٣٩ حزمةً في الاثنين، قِيس). <b>ويُسقط الحارسُ الحزمةَ حين تتغيّر
/// الحزمُ ولا يُعاد التوليد</b> (<c>python scripts/third_party_notices.py</c>) — في الاتّجاهين.</para>
/// </summary>
public class TheShippedPackagesAreAllNoticedTests
{
    private const string Notices = "THIRD-PARTY-NOTICES.txt";

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

    /// <summary>حزمُ التشغيل كما يحملها <c>Brqosh.Desktop.deps.json</c> — «المعرّف الإصدار».</summary>
    internal static SortedSet<string> ShippedPackages(string depsJson)
    {
        using var doc = JsonDocument.Parse(depsJson);
        var shipped = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var library in doc.RootElement.GetProperty("libraries").EnumerateObject())
            if (library.Value.GetProperty("type").GetString() == "package")
                shipped.Add(library.Name.Replace('/', ' '));
        return shipped;
    }

    /// <summary>الحزمُ التي يُسمّيها ملفُّ الإشعارات — سطرُ «Package: المعرّف الإصدار».</summary>
    internal static SortedSet<string> NoticedPackages(string notices) =>
        new(Regex.Matches(notices.Replace("\r\n", "\n"), @"^Package: (\S+ \S+)$", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value),
            StringComparer.Ordinal);

    private static SortedSet<string> Shipped()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Brqosh.Desktop.deps.json");
        Assert.True(File.Exists(path), $"‏{path} غائب — مرجعُ المشروع لم ينسخ رسمَ تبعيّات البرنامج.");
        return ShippedPackages(File.ReadAllText(path));
    }

    private static string NoticesText() => File.ReadAllText(Path.Combine(RepoRoot(), Notices));

    [Fact]
    public void EveryShippedPackage_HasANotice_AndNoNoticeNamesAnAbsentPackage()
    {
        var shipped = Shipped();
        var noticed = NoticedPackages(NoticesText());

        var missing = shipped.Except(noticed).ToList();
        var stale = noticed.Except(shipped).ToList();

        Assert.True(missing.Count == 0 && stale.Count == 0,
            "‏الإشعاراتُ لا تطابق ما يُشحن — أعِد التوليد (python scripts/third_party_notices.py). "
            + $"بلا إشعار: {string.Join(" · ", missing)} | إشعارٌ لغير مشحون: {string.Join(" · ", stale)}");
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ للقارئَين:</b> رسمُ التبعيّات يُقرأ حزماً حقيقيّةً (وإلّا فالمطابقةُ بين فراغين خضراء)، وسطرُ
    /// «Components» في رأس الملفّ يساوي عددَ ما يُسمّيه — فلا يُحذف مدخلٌ ويبقى العدد.
    /// </summary>
    [Fact]
    public void TheReaders_SeeRealPackages_AndTheHeaderCountAgrees()
    {
        var shipped = Shipped();
        Assert.True(shipped.Count >= 30, $"‏رسمُ التبعيّات قُرئ {shipped.Count} حزمةً فقط.");
        Assert.Contains(shipped, p => p.StartsWith("QuestPDF ", StringComparison.Ordinal));
        Assert.Contains(shipped, p => p.StartsWith("Npgsql ", StringComparison.Ordinal));

        var text = NoticesText();
        var header = Regex.Match(text, @"^Components: (\d+)$", RegexOptions.Multiline);
        Assert.True(header.Success, "‏رأسُ الملفّ بلا «Components: N».");
        Assert.Equal(int.Parse(header.Groups[1].Value), NoticedPackages(text).Count);
    }

    /// <summary>
    /// <b>والملفُّ يُشحن:</b> عنصرُ المحتوى في <c>Brqosh.Desktop.csproj</c> ينسخه إلى مخرَج البرنامج — ومرجعُ المشروع
    /// ينقله إلى هنا بالآليّة نفسِها التي تنقل <c>config/templates</c>؛ والمُنصِّبُ يحصد مجلّدَ النشر كلَّه.
    /// </summary>
    [Fact]
    public void TheNotices_AreCopiedBesideTheProgram()
    {
        var copied = Path.Combine(AppContext.BaseDirectory, Notices);

        Assert.True(File.Exists(copied), $"‏{Notices} لم يُنسخ مع البرنامج — فلا يبلغ مجلّدَ النشر ولا المُنصِّب.");
        Assert.Equal(NoticesText(), File.ReadAllText(copied));
    }
}
