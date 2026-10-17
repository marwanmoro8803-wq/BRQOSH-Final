using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٦٣ · <c>ui-ux#9</c>) <b>ترجمةُ الرسائل في الوضع الإنجليزيّ — الثابتةُ والمركَّبة.</b>
///
/// <para><b>ما كان:</b> <see cref="UiBilingualRegistry"/> يطابق النصَّ حرفاً، فرسالةٌ تحمل رمزاً أو رقماً — «الأصل FA-1 أُهلك…» — لا
/// تطابق شيئاً وتظهر عربيّةً في الواجهة الإنجليزيّة. فالترجمةُ هنا حرفيّةٌ أوّلاً، ثمّ بالنمط: قوالبُ <c>message-patterns.json</c>
/// (القالبُ العربيُّ بثقوبٍ مرقّمة ومقابلُه الإنجليزيّ) تُطابَق كاملةً، وما في الثقوب يُنقل كما هو.</para>
///
/// <para><b>والأطولُ نصّاً يُجرَّب أوّلاً</b> — فقالبٌ عامٌّ قصيرٌ («{0} غير مسموح — الحالة: {1}.») لا يبتلع رسالةً لها قالبٌ أخصّ.</para>
/// </summary>
public static class UiMessages
{
    internal const string PatternsResource = "Brqosh.Desktop.MessagePatterns.json";

    private static readonly Lazy<IReadOnlyList<(Regex Arabic, string English, int Literal)>> Patterns = new(LoadPatterns);

    /// <summary>‏يُترجم الرسالةَ إلى الإنجليزيّة — أو يُعيدها كما هي إن لم يعرفها السجلُّ ولا نمط.</summary>
    public static string Translate(string arabic)
    {
        if (string.IsNullOrEmpty(arabic))
            return arabic;
        var exact = UiBilingualRegistry.Lookup(arabic);
        if (!ReferenceEquals(exact, arabic) && exact != arabic)
            return exact;

        foreach (var (pattern, english, _) in Patterns.Value)
        {
            var m = pattern.Match(arabic);
            if (!m.Success)
                continue;
            var result = english;
            for (var i = m.Groups.Count - 1; i >= 1; i--)
                result = result.Replace("{" + (i - 1) + "}", m.Groups[i].Value, StringComparison.Ordinal);
            return result;
        }

        return arabic;
    }

    /// <summary>عدد القوالب المحمَّلة — لاختبار أنّ المورد شُحن ويُقرأ.</summary>
    internal static int PatternCount => Patterns.Value.Count;

    /// <summary>‏طولُ النصّ الحرفيّ لكلّ قالبٍ بترتيب التجربة — لاختبار أنّ الأخصَّ يُجرَّب أوّلاً أيّاً كان ترتيبُ الملفّ.</summary>
    internal static IReadOnlyList<int> TrialOrderLiteralLengths =>
        Patterns.Value.Select(p => p.Literal).ToList();

    private static IReadOnlyList<(Regex, string, int)> LoadPatterns()
    {
        using var stream = typeof(UiMessages).Assembly.GetManifestResourceStream(PatternsResource)
            ?? throw new InvalidOperationException($"المورد {PatternsResource} غير مُضمَّن في البرنامج.");
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.EnumerateObject()
            .Where(p => !p.Name.StartsWith('_'))
            .OrderByDescending(p => LiteralLength(p.Name))
            .Select(p => (ToRegex(p.Name), p.Value.GetString()!, LiteralLength(p.Name)))
            .ToList();
    }

    /// <summary>القالبُ نمطاً: النصُّ حرفيٌّ مُهرَّب، و<c>{n}</c> ثقبٌ، و<c>{{ }}</c> قوسٌ حرفيّ — والمطابقةُ للرسالة كلِّها.</summary>
    internal static Regex ToRegex(string template)
    {
        var sb = new StringBuilder("^");
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (c == '{' && i + 1 < template.Length && template[i + 1] == '{') { sb.Append(Regex.Escape("{")); i++; continue; }
            if (c == '}' && i + 1 < template.Length && template[i + 1] == '}') { sb.Append(Regex.Escape("}")); i++; continue; }
            if (c == '{')
            {
                var end = template.IndexOf('}', i);
                sb.Append("(.*?)");
                i = end;
                continue;
            }
            sb.Append(Regex.Escape(c.ToString()));
        }
        return new Regex(sb.Append('$').ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    private static int LiteralLength(string template) => Regex.Replace(template, @"\{\d+\}", "").Length;
}
