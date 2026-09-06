using System.IO;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (م-٣ · ن-٢٩) <b>بوّابةُ مواضعِ الاستدعاء — الحدُّ الذي أعلنه <c>NumericInputTests</c> عن نفسِه.</b>
///
/// <para>يقول ذلك الملفُّ حرفيّاً: «هذا الحارس يقيس الناتج بالأرقام، <b>ولا يفحص أن الاستدعاء
/// موجود في الملف</b>». <b>وبند ن-٢٩ يسكن في هذا الحدِّ بالضبط</b>: الحارسُ مبنيٌّ ومقيسٌ
/// وموثَّق، و<b>ستّةٌ وعشرون حقلاً ماليّاً وكمّيّاً لا تصله</b>. وهو النمطُ الجامعُ لهذا
/// التدقيق كلِّه — «مبنيٌّ ولا يصل».</para>
///
/// <para><b>ولمَ بوّابةٌ نصّيّةٌ لا اختبارُ سلوك:</b> لأنّ اختبارَ السلوك يقيس ما يُستدعى،
/// <b>ولا يرى ما لا يُستدعى</b>. وحقلٌ جديدٌ يُضاف غداً بـ<c>decimal.TryParse</c> عارٍ يمرّ
/// بمجموعةٍ خضراءَ كاملة — كما مرّت هذه الستّةُ والعشرون سنةً كاملة.</para>
///
/// <para><b>والعطلُ مقيسٌ على .NET 9 في هذا المشروع، والصيغتان مكسورتان بالتساوي:</b>
/// <c>decimal.TryParse(t, out v)</c> على <c>ar-SA</c>، و<c>decimal.TryParse(t, NumberStyles.Number,
/// CultureInfo.InvariantCulture, out v)</c> في كلّ مكان — كلتاهما تعطي
/// <c>"1,5"</c> ⟵ <b>15</b>، و<c>"12,34"</c> ⟵ <b>1234</b>، و<c>"1,5,5"</c> ⟵ <b>155</b>.
/// فالسببُ <c>AllowThousands</c> داخل <c>NumberStyles.Number</c>، و.NET لا يتحقّق من حجم
/// المجموعة. <b>فمن يكتب الفاصلةَ العشريّةَ بالطريقة الأوروبيّة يُرحَّل له عشرةُ أضعاف.</b></para>
///
/// <para><b>ونطاقُ البوّابة سطحُ المكتب وحدَه</b> — وهو حيث يُكتب نصٌّ بيد إنسان. أمّا الواجهةُ
/// البرمجيّة فمدخلاتُها JSON مُنمَّطة، ولا تمرّ بتحليلِ نصٍّ حرّ.</para>
/// </summary>
public class NumericInputIsTheOnlyParserTests
{
    private const string BareParse = "decimal.TryParse";

    /// <summary>الحارسُ نفسُه — وهو الموضعُ الوحيدُ الذي يجوز فيه النداءُ العاري.</summary>
    private static readonly string TheOnlyLegitimateCaller =
        Path.Combine("Ui", "NumericInput.cs");

    /// <summary>
    /// <b>القياس:</b> لا ملفَّ في سطح المكتب يُحلّل نصَّ مستخدمٍ بنداءٍ عارٍ.
    ///
    /// <para>والرسالةُ تُسمّي كلَّ مخالفٍ بملفِّه وسطرِه ونصِّه — فبوّابةٌ تقول «فشل» ولا تقول
    /// «أين» تُكلّف من يقرؤها ما كلّفنا اكتشافُها.</para>
    /// </summary>
    [Fact]
    public void NoDesktopFile_ParsesUserTextWithABareDecimalTryParse()
    {
        var offenders = Offenders();

        Assert.True(offenders.Count == 0,
            $"‏{offenders.Count} موضعاً يُحلّل نصّاً بـ{BareParse} عارياً بدل NumericInput.\n"
            + "‏و«1,5» تُقرأ 15 في الصيغتين معاً — بمعاملين وبأربعة.\n\n"
            + string.Join("\n", offenders));
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وبدونه «صفرُ مخالفين» لا يعني شيئاً.</b>
    ///
    /// <para>فالنتيجةُ الخضراءُ تبدو واحدةً سواءٌ فُحِص الشيءُ فلم يُوجد، <b>أم لم يُفحص
    /// أصلاً</b>: مسارٌ خاطئ، أو مرشّحٌ لا يُطابق، أو امتدادٌ منسيّ. فيُشترط هنا أن يجد
    /// الماسحُ النداءَ الشرعيَّ الوحيد — داخل <c>NumericInput</c> نفسِه.</para>
    /// </summary>
    [Fact]
    public void TheScanner_FindsTheOneLegitimateCall_SoAnEmptyResultMeansSomething()
    {
        var guard = Path.Combine(DesktopRoot(), TheOnlyLegitimateCaller);
        Assert.True(File.Exists(guard), $"‏الحارسُ نفسُه غيرُ موجود: {guard}");

        var hits = MatchesIn(guard);

        Assert.True(hits.Count > 0,
            $"‏لم يجد الماسحُ {BareParse} داخل NumericInput.cs — فالماسحُ أعمى، "
            + "و«صفرُ مخالفين» في الاختبار الآخر خبرٌ عن الماسح لا عن الكود.");
    }

    /// <summary>
    /// <b>وشاهدٌ ثانٍ: الشجرةُ المفحوصةُ ليست فارغة.</b> مسارٌ خاطئٌ يُعطي صفرَ ملفّاتٍ
    /// وصفرَ مخالفين — وهو أخطرُ من مخالفةٍ تُرى.
    /// </summary>
    [Fact]
    public void TheScan_ActuallyReachesTheDesktopSources()
    {
        var files = DesktopSources();

        Assert.True(files.Count > 100,
            $"‏الماسحُ رأى {files.Count} ملفّاً فقط في سطح المكتب — المسارُ مشكوكٌ فيه.");
    }

    // ── الماسح ───────────────────────────────────────────────────────────────

    private static List<string> Offenders()
    {
        var root = DesktopRoot();
        var guard = Path.Combine(root, TheOnlyLegitimateCaller);
        var found = new List<string>();

        foreach (var file in DesktopSources())
        {
            if (string.Equals(file, guard, StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var (line, text) in MatchesIn(file))
                found.Add($"  {Path.GetRelativePath(root, file)}:{line}  {text}");
        }

        return found;
    }

    /// <summary>
    /// المطابقاتُ في ملفٍّ واحد — <b>والتعليقاتُ تُستثنى</b>، إذ تشرح تعليقاتُ الإصلاح
    /// العطلَ بنصِّه، <b>وشرحُ عطلٍ ليس إعادةً له</b>. ونصُّ التعليق هو الذي يمنع «تبسيطاً»
    /// يُعيده، فحذفُه ليُرضيَ ماسحاً خسارةٌ صافية.
    /// </summary>
    private static List<(int Line, string Text)> MatchesIn(string file)
    {
        var hits = new List<(int, string)>();
        var lines = File.ReadAllLines(file);

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
            if (trimmed.StartsWith("///", StringComparison.Ordinal)) continue;
            if (trimmed.StartsWith("*", StringComparison.Ordinal)) continue;

            if (lines[i].Contains(BareParse, StringComparison.Ordinal))
                hits.Add((i + 1, trimmed));
        }

        return hits;
    }

    private static List<string> DesktopSources() =>
        Directory.EnumerateFiles(DesktopRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                    StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                                    StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static string DesktopRoot()
    {
        var path = Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop");
        Assert.True(Directory.Exists(path), $"‏مصدرُ سطح المكتب غيرُ موجود: {path}");
        return path;
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
