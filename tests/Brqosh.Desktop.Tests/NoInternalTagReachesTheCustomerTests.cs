using System.IO;
using System.Text.RegularExpressions;
using Brqosh.Desktop.Support;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>commercial-readiness#7</c>) <b>لا وسمَ داخليٌّ ولا رقمٌ مؤقّتٌ في نصٍّ يراه العميل — ولا وعدُ خدمةٍ
/// لم يُعتمد.</b>
///
/// <para><b>ما كان:</b> شاشةُ «دعم العملاء» تعرض <c>9200XXXXXX (MVP — يُحدَّث عند الإطلاق)</c> و<c>G-31 · … (MVP)</c>،
/// وثلاثَ صفحاتٍ أخرى تعرض وسومَ خطّة الفجوات في عناوينها (<c>G-10</c> · <c>G-32</c> · <c>G-69-C</c>) — معرّفاتُ وثيقة
/// تخطيطٍ داخليّةٍ في واجهةٍ تُباع. <b>والحارسُ على الصنف لا على الشاشة</b>: كلُّ نصٍّ معروضٍ في XAML وكلُّ نصٍّ يُسنَد من
/// الشيفرة الخلفيّة، بأيّ لغة (فـ«CRM · G-10 · Leads» لا عربيَّ فيه، وماسحُ التوطين لا يراه).</para>
/// </summary>
public class NoInternalTagReachesTheCustomerTests
{
    /// <summary>خصائصُ العرض في XAML — والقيمةُ التي تبدأ بـ<c>{</c> ربطٌ لا نصّ.</summary>
    private static readonly Regex XamlDisplay = new(
        @"\b(?:Header|Content|Text|Title|ToolTip|Watermark|PlaceholderText)=""([^""{][^""]*)""", RegexOptions.Compiled);

    /// <summary>إسنادُ نصٍّ إلى خاصيّة عرضٍ من الشيفرة الخلفيّة (والمُستكمَلُ معه).</summary>
    private static readonly Regex CodeBehindDisplay = new(
        @"\.(?:Text|Content|Header|Title|ToolTip|PlaceholderText)\s*=\s*\$?""([^""]*)""", RegexOptions.Compiled);

    internal static readonly (string Name, Regex Pattern)[] Leaks =
    [
        ("وسمُ فجوةٍ داخليّ (G-nn)", new Regex(@"\bG-\d+", RegexOptions.Compiled)),
        ("«MVP»", new Regex(@"\bMVP\b", RegexOptions.Compiled)),
        ("رقمٌ مؤقّت (XXXX)", new Regex(@"X{4,}", RegexOptions.Compiled)),
    ];

    internal static List<string> LeaksIn(string text) =>
        Leaks.Where(l => l.Pattern.IsMatch(text)).Select(l => l.Name).ToList();

    private static string DesktopRoot()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
        Assert.True(Directory.Exists(root), $"‏مصدرُ Desktop مفقود: {root}");
        return root;
    }

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    /// <summary>كلُّ نصٍّ معروض — (الملفّ، النصّ). والتعليقاتُ لا تُعرض فتُنزع أوّلاً.</summary>
    internal static List<(string File, string Text)> DisplayedTexts()
    {
        var root = DesktopRoot();
        var found = new List<(string, string)>();

        foreach (var file in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories).Where(f => !IsBuildOutput(f)))
        {
            var xaml = Regex.Replace(File.ReadAllText(file), "<!--.*?-->", " ", RegexOptions.Singleline);
            foreach (Match m in XamlDisplay.Matches(xaml))
                found.Add((Path.GetRelativePath(root, file), m.Groups[1].Value));
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !IsBuildOutput(f) && !f.EndsWith(".g.cs", StringComparison.Ordinal)))
        {
            foreach (Match m in CodeBehindDisplay.Matches(File.ReadAllText(file)))
                found.Add((Path.GetRelativePath(root, file), m.Groups[1].Value));
        }

        return found;
    }

    /// <summary>
    /// <b>وبالإنجليزيّة أيضاً:</b> ما يُعرض عند التبديل هو ترجمةُ السجلّ لا النصُّ المصدر — فمفتاحٌ نظيفٌ ترجمتُه
    /// «Active companies list — G-69-C» يُسرّب الوسمَ والحارسُ لا يرى إلّا العربيّة.
    /// </summary>
    [Fact]
    public void NoDisplayedText_CarriesAnInternalTagOrAPlaceholder()
    {
        var leaks = DisplayedTexts()
            .SelectMany(t => new[] { (t.File, Shown: t.Text), (t.File, Shown: UiBilingualRegistry.Lookup(t.Text)) })
            .Select(t => (t.File, t.Shown, Kinds: LeaksIn(t.Shown)))
            .Where(t => t.Kinds.Count > 0)
            .Select(t => $"{t.File}: «{t.Shown}» ({string.Join("، ", t.Kinds)})")
            .Distinct()
            .ToList();

        Assert.True(leaks.Count == 0, "‏نصٌّ يراه العميل يحمل وسماً داخليّاً أو رقماً مؤقّتاً:\n" + string.Join("\n", leaks));
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ للماسح وللأنماط:</b> الماسحُ يرى نصوصاً معروضةً حقيقيّةً من XAML ومن الشيفرة الخلفيّة، والأنماطُ تلتقط
    /// ما كان وتترك ما يُشبهه — وإلّا فالحارسُ أخضرُ على فراغ.
    /// </summary>
    [Fact]
    public void TheScanner_SeesRealText_AndThePatternsTellLeaksFromLookalikes()
    {
        var texts = DisplayedTexts();
        Assert.Contains(texts, t => t.Text == "حالة النظام");
        Assert.Contains(texts, t => t.File.EndsWith(".cs", StringComparison.Ordinal));

        Assert.Equal(2, LeaksIn("G-31 · SLA · قنوات التواصل · تذاكر الدعم (MVP)").Count);
        Assert.Single(LeaksIn("الهاتف: 9200XXXXXX"));
        Assert.Single(LeaksIn("قائمة الشركات النشطة — G-69-C"));
        Assert.Empty(LeaksIn("CRM · Leads"));
        Assert.Empty(LeaksIn("GUID-12 · SG-1 · Xbox"));
    }

    /// <summary>
    /// <b>ولا وعدَ خدمةٍ لم يُعتمد:</b> بلا ملفّ الدعم لا قناةَ ولا باقة — والملفُّ التالفُ يقول سببَه ولا يُبتلع؛
    /// والمعتمَدُ يُعرض كما كُتب.
    /// </summary>
    [Fact]
    public void TheSupportPage_ShowsOnlyWhatTheVendorApproved()
    {
        Assert.False(SupportContact.Parse(null).IsConfigured);
        Assert.Empty(SupportContact.Parse(null).Plans);
        Assert.False(SupportContact.Parse("{}").IsConfigured);

        var broken = SupportContact.Parse("{ \"Email\": ");
        Assert.False(broken.IsConfigured);
        Assert.False(string.IsNullOrWhiteSpace(broken.Problem));

        var approved = SupportContact.Parse(
            "{ \"Email\": \"help@example.test\", \"Plans\": [ { \"Plan\": \"قياسي\", \"Response\": \"24 ساعة عمل\", \"Hours\": \"أحد–خميس\" } ] }");
        Assert.True(approved.IsConfigured);
        Assert.Equal("help@example.test", approved.Email);
        Assert.Null(approved.Phone);
        Assert.Equal(new SupportPlan("قياسي", "24 ساعة عمل", "أحد–خميس"), Assert.Single(approved.Plans));

        Assert.False(SupportContact.LoadBeside(Path.Combine(Path.GetTempPath(), "brqosh-no-such-" + Guid.NewGuid().ToString("N"))).IsConfigured);
    }
}
