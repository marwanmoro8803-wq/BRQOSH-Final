using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (ح-١٦٣ · ui-ux#9) <b>كلُّ رسالة خطأٍ محاسبيّةٍ تُترجم في الوضع الإنجليزيّ — الثابتةُ بالسجلّ، والمركَّبةُ بقالبها.</b>
///
/// <para><b>ما كان:</b> ادّعاءُ «الترجمة ١٠٠٪» يغطّي نصوصَ XAML الثابتة وحدَها، و١٢٨٢ موضعاً يرمي <c>AccountingException</c> برسالةٍ
/// عربيّة لا يبلغها السجلُّ — فالمستخدمُ الإنجليزيُّ يرى عربيّاً عند كلّ خطأ. وكان ٤٤ منها فقط مترجَماً.</para>
///
/// <para><b>والقياسُ هنا على المصدر نفسِه</b>: كلُّ رسالةٍ تُكتب نصّاً واحداً (١١٦٨ موضعاً) يجب أن تُترجم — الثابتةُ في
/// <c>ui-overrides.json</c> والمركَّبةُ (<c>$"…{x}…"</c>) قالباً في <c>message-patterns.json</c>. <b>وما يُكتب بغير ذلك</b> (نصوصٌ
/// مجموعةٌ بـ<c>+</c>، أو متغيّرٌ) دَينٌ معدودٌ بسقف: يُترجم بأداة الاستخراج، ولا يزيد إلا بقرار.</para>
/// </summary>
public class EveryAccountingMessageIsTranslatedTests
{
    /// <summary>‏مواضعُ لا تُقرأ نصّاً واحداً — مقيسةٌ عند ح-١٦٣؛ تنقص ولا تزيد.</summary>
    private const int UnreadableSitesCap = 114;

    private static readonly Regex Site = new(@"new\s+(?:[\w.]*\.)?AccountingException\(");
    private static readonly Regex Simple = new(@"new\s+(?:[\w.]*\.)?AccountingException\(\s*(\$?)""((?:[^""\\]|\\.)*)""\s*[,)]");
    private static readonly Regex Arabic = new(@"[؀-ۿ]");
    private static readonly string Root = FindRepoRoot();

    [Fact]
    public void EveryMessageWrittenAsOneString_IsTranslated_AndTheRestStaysUnderItsCap()
    {
        var overrides = ReadJson(Path.Combine(Root, "assets", "i18n", "ui-overrides.json"));
        var patterns = ReadJson(Path.Combine(Root, "assets", "i18n", "message-patterns.json"));
        int total = 0, readable = 0;
        var untranslated = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var source = File.ReadAllText(file);
            total += Site.Matches(source).Count;
            foreach (Match m in Simple.Matches(source))
            {
                readable++;
                var text = Unescape(m.Groups[2].Value);
                if (!Arabic.IsMatch(text))
                    continue;
                var known = m.Groups[1].Value == "$" ? patterns.Contains(ToTemplate(text)) : overrides.Contains(text);
                if (!known)
                    untranslated.Add($"{Path.GetFileName(file)}: {text}");
            }
        }

        Assert.True(untranslated.Count == 0,
            $"{untranslated.Count} رسالةً بلا ترجمة — أضفها إلى ui-overrides.json (الثابتة) أو message-patterns.json (المركَّبة):\n  "
            + string.Join("\n  ", untranslated.Take(20)));
        Assert.True(total - readable <= UnreadableSitesCap,
            $"{total - readable} موضعاً لا يُقرأ نصّاً واحداً والسقفُ {UnreadableSitesCap} — اكتب الرسالةَ نصّاً واحداً لتُحرَس ترجمتُها.");
        Assert.True(readable > 1000, $"لم يُقرأ إلا {readable} موضعاً — المسحُ أعمى.");
    }

    [Fact]
    public void AComposedMessage_IsTranslatedWithItsValues()
    {
        Assert.Equal(
            "Asset FA-7 has been depreciated — reverse the depreciation run first, then cancel the acquisition.",
            UiMessages.Translate("الأصل FA-7 أُهلك — اعكس تشغيلَ الإهلاك أوّلاً ثمّ ألغِ الاقتناء."));
        Assert.Equal(
            "The entry is not balanced: total debit 1,000.00 does not equal total credit 900.00.",
            UiMessages.Translate("القيد غير متوازن: مجموع المدين 1,000.00 لا يساوي مجموع الدائن 900.00."));
    }

    [Fact]
    public void AFixedMessage_IsTranslated_AndAnUnknownOne_IsReturnedAsItIs()
    {
        Assert.Equal("The quantity must be positive.", UiMessages.Translate("الكمية يجب أن تكون موجبة."));
        Assert.Equal("نصٌّ لا يعرفه أحد", UiMessages.Translate("نصٌّ لا يعرفه أحد"));
    }

    /// <summary>
    /// ‏<b>قالبٌ عامٌّ قصيرٌ لا يبتلع رسالةً لها قالبٌ أخصّ.</b> «تعذّر تنزيلُ الحزمة — {0}» يطابق أيضاً «…القناةُ ردّت 404.»
    /// ‏فيُبقي في الإنجليزيّة نصّاً عربيّاً — تداخلٌ حقيقيٌّ وجده مسحُ القوالب كلِّها.
    /// </summary>
    [Fact]
    public void TheMoreSpecificTemplate_Wins()
    {
        Assert.Equal(
            "Could not download the package — the channel responded 404.",
            UiMessages.Translate("تعذّر تنزيلُ الحزمة — القناةُ ردّت 404."));
        Assert.Equal(
            "The total of the allocations 50.00 must equal the voucher amount 60.00 (or leave the allocation empty).",
            UiMessages.Translate("مجموع التخصيصات 50.00 يجب أن يساوي مبلغ السند 60.00 (أو اترك التخصيص فارغاً)."));
    }

    /// <summary>
    /// ‏<b>والأطولُ يُجرَّب أوّلاً أيّاً كان ترتيبُ الملفّ</b> — فالتداخلان أعلاه سليمان اليومَ لأنّ الأخصَّ سبق في الملفّ مصادفةً،
    /// ‏وقالبٌ يُضاف غداً في أوّله يقلبهما. فالترتيبُ نفسُه يُقاس.
    /// </summary>
    [Fact]
    public void Templates_AreTriedLongestFirst()
    {
        var lengths = UiMessages.TrialOrderLiteralLengths;
        Assert.True(lengths.Count > 200, $"لم يُحمَّل إلا {lengths.Count} قالباً — المورد لم يُضمَّن.");
        for (var i = 1; i < lengths.Count; i++)
            Assert.True(lengths[i - 1] >= lengths[i], $"القالب {i} أطولُ من سابقه — يُجرَّب العامُّ قبل الخاصّ.");
    }

    [Fact]
    public void BothDisplayPaths_TranslateThroughUiMessages()
    {
        var desktop = Path.Combine(Root, "src", "Brqosh.Desktop");
        Assert.Contains("UiMessages.Translate(text)", File.ReadAllText(Path.Combine(desktop, "Ui", "UiDialog.cs")), StringComparison.Ordinal);
        Assert.Contains("UiMessages.Translate(message)", File.ReadAllText(Path.Combine(desktop, "UiFeedback.cs")), StringComparison.Ordinal);
    }

    private static string ToTemplate(string text)
    {
        var n = 0;
        return Regex.Replace(text, @"\{\{|\}\}|\{[^{}]*\}", m => m.Value is "{{" or "}}" ? m.Value : "{" + n++ + "}");
    }

    private static string Unescape(string body) =>
        Regex.Replace(body, @"\\(.)", m => m.Groups[1].Value switch { "n" => "\n", "t" => "\t", "r" => "\r", var c => c });

    private static HashSet<string> ReadJson(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
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
