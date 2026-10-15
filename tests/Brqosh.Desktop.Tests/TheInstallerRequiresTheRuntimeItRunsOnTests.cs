using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٦٢ · <c>installer-deployment-ops#5</c>) <b>المُنصِّبُ يشترط وقتَ التشغيل الذي يعمل عليه
/// التطبيقُ — لا أيَّ وقتِ تشغيل.</b>
///
/// <para><b>ما كان:</b> <c>DirectorySearch</c> على المجلّد الأب <c>…\Microsoft.WindowsDesktop.App</c> — <b>وهو
/// موجودٌ لأيّ إصدار</b>: جهازٌ عليه .NET 6 أو 8 وحدَه يُنصَّب عليه «بنجاح» ثمّ لا يفتح التطبيق. <b>والتعليقُ
/// فوقه يصف فحصَ سجلٍّ لم يُنفَّذ</b>، والرسالةُ تقول «9 أو أحدث» <b>و10 وحدَه لا يُشغّله</b> (runtimeconfig يطلب 9.0
/// بتقدّمٍ ثانويٍّ افتراضيّ).</para>
///
/// <para><b>والفخُّ الذي كشفه التشغيلُ لا القراءة:</b> <c>FileSearch</c> <b>بـ<c>Id</c></b> تحت <c>DirectorySearch</c>
/// ذي عمقٍ يُولِّد صفَّ <c>DrLocator</c> للملفّ <b>بلا عمق</b> — فيُبحث عن الملفّ في المجلّد الأب وحدَه <b>فلا يُوجد
/// أبداً</b>، ويرفض المُنصِّبُ كلَّ جهاز. <b>وبلا <c>Id</c></b> يُدمج WiX توقيعَ الملفّ في صفّ العمق فيعمل. قِيس
/// بتشغيل <c>AppSearch</c> في جلسةٍ لا تُثبِّت: ٩ ⟵ 9.0.17 · ٦ ⟵ 6.0.36 (ضابطٌ موجب) · ٨ و١٠ ⟵ لا شيء.</para>
/// </summary>
public class TheInstallerRequiresTheRuntimeItRunsOnTests
{
    private static readonly XNamespace Wix = "http://wixtoolset.org/schemas/v4/wxs";

    private static XDocument Wxs() =>
        XDocument.Load(Path.Combine(RepositoryRoot(), "installer", "brqosh.wxs"));

    /// <summary>الإصدارُ الرئيسُ الذي يُبنى عليه التطبيق — من ملفّ المشروع لا من ثابتٍ مكتوبٍ مرّتين.</summary>
    private static int TargetMajor()
    {
        var csproj = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Brqosh.Desktop", "Brqosh.Desktop.csproj"));
        var match = Regex.Match(csproj, @"<TargetFramework>net(\d+)\.\d+-windows</TargetFramework>");
        Assert.True(match.Success, "لا TargetFramework مقروءٌ في Brqosh.Desktop.csproj.");
        return int.Parse(match.Groups[1].Value);
    }

    /// <summary>شرطُ الإطلاق الخاصّ بوقت التشغيل — الذي تذكر رسالتُه .NET.</summary>
    private static XElement RuntimeLaunch() =>
        Wxs().Descendants(Wix + "Launch")
            .Single(l => ((string?)l.Attribute("Message") ?? "").Contains(".NET", StringComparison.Ordinal));

    /// <summary>البحثُ الذي يضبط خاصّيّةَ الشرط.</summary>
    private static XElement RuntimeSearch()
    {
        var property = (string)RuntimeLaunch().Attribute("Condition")!;
        return Wxs().Descendants(Wix + "Property")
            .Single(p => (string?)p.Attribute("Id") == property)
            .Element(Wix + "DirectorySearch")!;
    }

    private static XElement? RuntimeFile() => RuntimeSearch().Element(Wix + "FileSearch");

    /// <summary><b>القياسُ المركزيّ</b>: البحثُ ملفٌّ محدودُ الإصدار بين أوّل الإصدار الرئيس وآخره.</summary>
    [Fact]
    public void TheCheckLooksForTheMajorVersionTheAppTargets()
    {
        var file = RuntimeFile();
        Assert.True(file is not null, "الشرطُ يبحث عن مجلّدٍ لا عن ملفٍّ بإصدار — فأيُّ وقتِ تشغيلٍ يُرضيه.");

        var major = TargetMajor();
        Assert.Equal(major, Version.Parse((string)file!.Attribute("MinVersion")!).Major);
        Assert.Equal(major, Version.Parse((string)file.Attribute("MaxVersion")!).Major);
    }

    /// <summary>والبحثُ ينزل إلى مجلّدات الإصدارات — فالملفُّ في <c>9.0.17\</c> لا في المجلّد الأب.</summary>
    [Fact]
    public void TheSearchDescendsIntoTheVersionFolders()
    {
        var depth = (string?)RuntimeSearch().Attribute("Depth");
        Assert.True(int.TryParse(depth, out var d) && d >= 1,
            $"عمقُ البحث «{depth}» — والملفُّ تحت مجلّد الإصدار، فلا يُرى من الأب.");
    }

    /// <summary>
    /// <b>والفخّ</b>: <c>FileSearch</c> بـ<c>Id</c> يُسقط العمقَ عن صفّ الملفّ فلا يُوجد أبداً — <b>ويرفض المُنصِّبُ
    /// كلَّ جهاز</b>. وقِيس بالتشغيل لا بالقراءة.
    /// </summary>
    [Fact]
    public void TheFileSearchHasNoId_SoTheDepthReachesIt()
    {
        var file = RuntimeFile();
        Assert.True(file is not null, "لا FileSearch أصلاً.");
        Assert.Null(file!.Attribute("Id"));
    }

    /// <summary>والرسالةُ لا تَعِد بما لا يعمل: إصدارٌ رئيسٌ آخرُ وحدَه لا يُشغّل تطبيقاً مبنيّاً على هذا (ح-١٣٨: صار 10).</summary>
    [Fact]
    public void TheMessageDoesNotPromiseNewerMajorVersions()
    {
        var message = (string)RuntimeLaunch().Attribute("Message")!;
        Assert.DoesNotContain("أو أحدث", message, StringComparison.Ordinal);
        Assert.Contains(TargetMajor().ToString(), message, StringComparison.Ordinal);
    }

    /// <summary><b>ضابطٌ موجب</b>: القارئُ يرى المُنصِّبَ ومشروعَه — فلا يمرّ حارسٌ لأنّه لا يقرأ شيئاً.</summary>
    [Fact]
    public void TheReaderSeesTheInstallerAndTheTarget()
    {
        Assert.NotNull(RuntimeSearch());
        Assert.Equal(10, TargetMajor());
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }
}
