using System.IO;
using System.Text.RegularExpressions;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-229) <b>نصوصُ الحوارات كانت خارج آلية الترجمة كلِّها.</b>
///
/// <para><c>MessageBox.Show</c> نافذةُ Win32 لا عنصرٌ في شجرة الواجهة، <b>ولا يبلغها
/// <c>XamlUiLocalizer</c> مهما وُضع في السجلّ</b>. فبوّابةُ الترجمة تُقرّ بذلك وتستثنيه
/// صراحةً — <b>وكان الاستثناءُ صادقاً: لا آليّةَ أصلاً</b>، وإدخالُ النصوص في السجلّ كان
/// يُخضِّر البوّابةَ والنصُّ عربيٌّ كما هو.</para>
///
/// <para><b>فالعلاجُ آليّةٌ لا مدخلٌ في سجلّ:</b> <see cref="UiDialog"/> يترجم عند الاستدعاء
/// عبر <c>UiBilingualRegistry.Lookup</c> حين تكون الواجهةُ إنجليزية. وهذان الحارسان يمنعان
/// عودةَ الباب.</para>
/// </summary>
public class DialogLocalizationTests
{
    /// <summary>الملفّات المسموح لها باستدعاء <c>MessageBox.Show</c> مباشرةً — ولكلٍّ سبب.</summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UiDialog.cs"] = "‏الغلافُ نفسُه — وهو الطريقُ الوحيد المقصود.",
    };

    /// <summary>
    /// <b>القياس الأوّل — الوصلة.</b> لا نصَّ حوارٍ يبلغ الشاشةَ خارج الغلاف. وحارسٌ
    /// يترجم بلا أن يمرّ به أحدٌ لا يترجم شيئاً.
    /// </summary>
    [Fact]
    public void NoScreenCallsMessageBoxDirectly()
    {
        var offenders = new List<string>();

        foreach (var file in DesktopSources())
        {
            var name = Path.GetFileName(file);
            if (Allowed.ContainsKey(name)) continue;

            var source = StripComments(File.ReadAllText(file));
            var hits = Regex.Matches(source, @"\bMessageBox\.Show\s*\(").Count;
            if (hits > 0)
                offenders.Add($"{Path.GetFileName(file)} ({hits})");
        }

        Assert.True(offenders.Count == 0,
            $"‏{offenders.Count} ملفّاً يستدعي MessageBox.Show مباشرةً — فنصُّه يبقى عربياً "
            + "عند التبديل للإنجليزية، ولا يبلغه أيُّ مترجِم:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// <b>القياس الثاني — الترجمة نفسُها.</b> كلُّ نصٍّ عربيٍّ <b>حرفيّ</b> يُمرَّر إلى
    /// الغلاف له مدخلٌ في السجلّ. وغلافٌ يبحث عمّا ليس هناك يُعيد العربيةَ كما هي.
    ///
    /// <para><b>والحرفيّ وحدَه</b> — فالنصوصُ المركَّبة (<c>$"…{x}…"</c>) ورسائلُ الاستثناءات
    /// لا تُطابَق ككلٍّ، وهي مُعلَنةٌ في <see cref="TheUntranslatableRemainder_IsDeclaredAndDoesNotGrow"/>.</para>
    /// </summary>
    [Fact]
    public void EveryLiteralDialogTextHasATranslation()
    {
        var missing = DialogLiterals()
            .Where(text => !UiBilingualRegistry.Map.ContainsKey(text))
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            $"‏{missing.Count} نصَّ حوارٍ بلا ترجمة في السجلّ — يظهر عربياً في واجهةٍ إنجليزية:\n  "
            + string.Join("\n  ", missing.Take(20))
            + (missing.Count > 20 ? $"\n  ... و{missing.Count - 20} غيرها" : ""));
    }

    /// <summary>
    /// <b>وما لا يُترجَم يُعلَن ويُعَدّ.</b> النصوصُ المركَّبة ورسائلُ الاستثناءات لا يبلغها
    /// البحثُ عن نصٍّ كامل. <b>وتركُها بلا عدٍّ يجعلها تنمو صامتة</b> حتى تعود الواجهةُ
    /// نصفَ مترجَمةٍ من بابٍ آخر — وهو بعينه ما وقع مع بوّابة الترجمة الأولى.
    ///
    /// <para>فالسقفُ هنا ليس هدفاً بل <b>مِرصاد</b>: تجاوزُه يعني أنّ نصّاً جديداً كُتب
    /// بصيغةٍ لا تُترجَم، فيُراجَع لا يُرفع الرقم.</para>
    /// </summary>
    [Fact]
    public void TheUntranslatableRemainder_IsDeclaredAndDoesNotGrow()
    {
        const int DeclaredCeiling = 75;

        var count = DialogCalls().Count(c => !IsPlainLiteral(c));

        Assert.True(count <= DeclaredCeiling,
            $"‏نصوصُ الحوارات غيرُ القابلة للبحث صارت {count} وكان السقفُ المُعلَن {DeclaredCeiling}. "
            + "نصٌّ جديد كُتب مركَّباً أو من رسالة استثناء — يُصاغ بـLocaleManager.T أو يُنقل "
            + "إلى نصٍّ حرفيّ، لا يُرفع السقف.");
    }

    /// <summary>
    /// <b>القياس السلوكيّ — وهو ما لا يقوله أيُّ حارسٍ بنيويّ.</b> الحارسان أعلاه يُثبتان
    /// أنّ كلَّ نداءٍ يمرّ بالغلاف وأنّ لكلّ نصٍّ مدخلاً؛ <b>ولا يُثبتان أنّ الترجمةَ تقع</b>.
    ///
    /// <para>فهنا تُقلَب اللغةُ ويُقاس المخرَج: عربيٌّ في الوضع العربيّ، وإنجليزيٌّ بعد
    /// التبديل. <b>ونصٌّ لا مدخلَ له يعود كما هو</b> — فلا يختفي ولا يصير فراغاً.</para>
    /// </summary>
    [Fact]
    public void TheWrapperActuallyTranslates_AndFallsBackWhenItCannot()
    {
        const string Known = "تم الحفظ بنجاح.";
        const string Unknown = "نصٌّ لا وجود له في السجلّ إطلاقاً.";

        Assert.True(UiBilingualRegistry.Map.ContainsKey(Known),
            "القياسُ أجوف — النصُّ المختار ليس في السجلّ أصلاً.");

        var wasEnglish = LocaleManager.IsEnglish;
        try
        {
            if (LocaleManager.IsEnglish) LocaleManager.Toggle();
            Assert.Equal(Known, UiDialog.Localize(Known));

            LocaleManager.Toggle();
            Assert.Equal(UiBilingualRegistry.Map[Known], UiDialog.Localize(Known));
            Assert.NotEqual(Known, UiDialog.Localize(Known));

            Assert.Equal(Unknown, UiDialog.Localize(Unknown));
            Assert.Equal("", UiDialog.Localize(""));
        }
        finally
        {
            if (LocaleManager.IsEnglish != wasEnglish) LocaleManager.Toggle();
        }
    }

    // ── الأدوات ──────────────────────────────────────────────────────────────

    private static IEnumerable<string> DesktopSources() =>
        Directory.EnumerateFiles(DesktopRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase));

    private static string DesktopRoot()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
        Assert.True(Directory.Exists(root), $"Desktop source root not found: {root}");
        return root;
    }

    /// <summary>الوسيطُ النصّيّ لكلّ استدعاء حوار — من الغلاف أو من MessageBox مباشرةً.</summary>
    private static List<string> DialogCalls()
    {
        var calls = new List<string>();
        foreach (var file in DesktopSources())
        {
            // ‏الغلافُ نفسُه ليس مستدعياً — تواقيعُه تمرّر ما يصلها.
            if (Path.GetFileName(file).Equals("UiDialog.cs", StringComparison.OrdinalIgnoreCase))
                continue;

            var source = StripComments(File.ReadAllText(file));
            foreach (Match m in Regex.Matches(source, @"\b(?:UiDialog|MessageBox)\.Show\s*\("))
            {
                var args = ArgumentsAt(source, m.Index + m.Length);
                if (args.Count == 0) continue;

                // ‏حملُ المالك يُزيح النصَّ إلى الوسيط الثاني — <b>والمالكُ يُسمّى صراحةً</b>.
                // ‏وأوّلُ صياغةٍ فحصت `this` وحدَها فعدّت وسيطَ مالكٍ اسمُه `owner` نصّاً؛
                // ‏وثانيةٌ قلبت القاعدة إلى «ما ليس نصّاً فهو مالك» <b>فأزاحت رسالةً في
                // ‏متغيّرٍ وعدّت الوسمَ نصَّها</b>. فالقائمةُ المغلقة أصدقُ من الاستنتاج.
                var text = IsOwner(args[0]) && args.Count > 1 ? args[1] : args[0];

                if (ContainsArabic(text) || !IsPlainLiteral(text))
                    calls.Add(text);
            }
        }
        return calls;
    }

    private static IEnumerable<string> DialogLiterals() =>
        DialogCalls()
            .Where(IsPlainLiteral)
            .Select(t => t[1..^1])
            .Where(ContainsArabic)
            .Distinct(StringComparer.Ordinal);

    private static bool IsPlainLiteral(string arg) =>
        arg.Length >= 2 && arg[0] == '"' && arg[^1] == '"' && !arg[1..^1].Contains('"');

    /// <summary>‏المالكُ يُسمّى صراحةً — قائمةٌ مغلقة لا استنتاج.</summary>
    private static bool IsOwner(string arg) =>
        arg is "this" or "owner" || arg.EndsWith("Window", StringComparison.Ordinal);
    private static bool ContainsArabic(string s) => s.Any(c => c >= '؀' && c <= 'ۿ');

    private static List<string> ArgumentsAt(string source, int start)
    {
        var args = new List<string>();
        var depth = 0;
        var current = new System.Text.StringBuilder();

        for (var i = start; i < source.Length; i++)
        {
            var c = source[i];
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}')
            {
                if (depth == 0) { args.Add(current.ToString().Trim()); break; }
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                args.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }
            current.Append(c);
        }

        return args;
    }

    /// <summary>‏يُجرَّد التعليقُ قبل المطابقة — درسُ مسح الابتلاع: تعليقٌ يقتبس العطلَ يُبلَّغ عنه.</summary>
    private static string StripComments(string source) =>
        Regex.Replace(
            Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline),
            @"//[^\n]*", "");
}
