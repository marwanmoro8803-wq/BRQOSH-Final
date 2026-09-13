using System.IO;
using System.Text;
using Brqosh.Application.Setup;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// ‏(م-٥ · ن-٤٦) <b>مصدرٌ رسميٌّ لا يُفهَم نصُّه كان يخفض التركيبَ إلى تطويرٍ بلا صوت.</b>
///
/// <para><b>ما كان:</b> المصدرُ الرسميُّ يمرّ بـ<c>Parse</c> ونهايتُها <c>_ => Development</c>،
/// <b>وبلا فرعٍ للتطوير أصلاً</b> — فـ<c>development</c> و<c>staging</c> و<c>Productoin</c>
/// تخرج من المخرج نفسِه. والطريقُ الحيُّ مكتوبٌ في وثائق التشغيل: <c>docs/postgres-production.md:13</c>
/// و<c>installer/README.md:20</c> يأمران المُشغّلَ أن <b>يكتب بيده</b>
/// <c>SetEnvironmentVariable("BRQOSH_ENV", "production", "Machine")</c>.</para>
///
/// <para><b>ولمَ يُقاس هنا بالدالّة النقيّة لا بالبيئة:</b> <c>ResolveAuthoritative</c>
/// <b>أوّلُ مصدرٍ يفوز</b> (AppEnvironment.cs:106-108) — فمتى ضُبط متغيّرُ نطاقِ الجهاز لم
/// يُقرأ ملفُّ العلامة أصلاً. <b>فحارسٌ يقود فرعَ الملفّ وحدَه يمرّ أخضرَ على إصلاحٍ نصفيّ</b>
/// يترك فرعَ الجهاز — وهو الفرعُ الحيُّ الوحيد. وتثبيتُ الدالّة يغطّي <b>الموضعَين معاً
/// بالبناء</b>، ولا يكتب متغيّرَ بيئةٍ ولا ملفّاً في مجلّد التشغيل — <b>فلا يُسمّم اختباراً
/// متوازياً</b>، وهو عطلٌ وقع في هذه الحزمة فعلاً.</para>
/// </summary>
public class AuthoritativeEnvironmentFailsClosedTests
{
    // ── ١: نصٌّ رسميٌّ لا يُفهَم ⟵ إنتاج ────────────────────────────────────

    /// <summary>
    /// <b>الشكُّ يُقرأ أشدَّ لا أضعف.</b> وكلُّ هذه كانت تُقرأ <c>Development</c>.
    /// </summary>
    [Theory]
    [InlineData("Productoin")]      // ‏حرفان مقلوبان — وهو ما يُكتب باليد
    [InlineData("prodction")]
    [InlineData("staging")]         // ‏اسمُ بيئةٍ جديدٍ لا يعرفه الجدول
    [InlineData("preprod")]
    [InlineData("production ;")]
    [InlineData("produc tion")]
    [InlineData("إنتاج")]  // ‏«إنتاج»
    [InlineData("﻿production")]                // ‏BOM لا يمرّ بملفّ بل بمتغيّر
    [InlineData("production\0")]                    // ‏صفرٌ لاحق — Trim لا يزيله
    [InlineData("‏production")]                // ‏علامةُ اتّجاه
    public void AnUnreadableAuthoritativeDeclaration_IsReadAsProduction(string raw) =>
        Assert.Equal(AppEnvironment.Production, AppEnvironmentConfig.ParseAuthoritative(raw));

    /// <summary>
    /// <b>وحارسٌ يردّ كلَّ شيءٍ إنتاجاً ليس حارساً.</b> فالإعلانُ المفهومُ يُحترَم كما هو —
    /// وبدون هذه الأسطر يمرّ «أعِد إنتاجاً دائماً» ويُلغي وضعَ التطوير من المستودع كلِّه.
    /// </summary>
    [Theory]
    [InlineData("development", AppEnvironment.Development)]
    [InlineData("DEVELOPMENT", AppEnvironment.Development)]
    [InlineData("  dev  ", AppEnvironment.Development)]
    [InlineData("test", AppEnvironment.Test)]
    [InlineData("production", AppEnvironment.Production)]
    [InlineData("prod", AppEnvironment.Production)]
    public void AReadableAuthoritativeDeclaration_IsHonouredExactly(string raw, AppEnvironment expected) =>
        Assert.Equal(expected, AppEnvironmentConfig.ParseAuthoritative(raw));

    // ── ٢: المدخلان مسمومان معاً — وهي حالةُ التركيب المشحون ────────────────

    /// <summary>
    /// <b>ولمَ لا يكفي إسقاطُ المصدر الرسميّ.</b> متغيّرُ نطاقِ الجهاز <b>يُورَّث حرفيّاً إلى
    /// كتلة بيئة العمليّة</b>، فالنصُّ المعطوبُ نفسُه يصل إلى فرع العمليّة المتساهل.
    ///
    /// <para>فلو أُعيد <c>null</c> من القراءة الرسميّة لصار <c>Decide(null, "Productoin", null)</c>
    /// ⟵ <b>تطويراً</b> — <b>العطلُ نفسُه من بابٍ آخر</b>. وهذا الاختبارُ يُسقط ذلك الإصلاحَ
    /// الوسطَ تحديداً.</para>
    /// </summary>
    [Theory]
    [InlineData("Productoin")]
    [InlineData("staging")]
    [InlineData("﻿production")]
    public void WhenBothTheAuthoritativeSourceAndTheProcessCarryTheSameBadString_ItIsStillProduction(string poison)
    {
        var authoritative = AppEnvironmentConfig.ParseAuthoritative(poison);

        Assert.Equal(AppEnvironment.Production,
            AppEnvironmentConfig.Decide(authoritative, poison, null));
    }

    /// <summary>‏شاهدُ ضبطٍ سالب: الإصلاحُ الوسطُ («أعِد <c>null</c>») يسقط فعلاً.</summary>
    [Fact]
    public void TheHalfFix_ThatResolvesToNull_WouldStillDowngrade() =>
        Assert.Equal(AppEnvironment.Development,
            AppEnvironmentConfig.Decide(null, "Productoin", null));

    // ── ٣: وأنّ الموضعَين كليهما يمرّان بالدالّة الصحيحة ───────────────────

    /// <summary>
    /// <b>تثبيتٌ بنيويّ:</b> لا موضعَ في <c>ResolveAuthoritative</c> يُنادي <c>Parse</c>
    /// المتساهلة. <b>وهذا ما يمنع الإصلاحَ النصفيّ</b> — أن يُحرَس فرعُ الملفّ ويُنسى
    /// فرعُ الجهاز، وهو الفرعُ الذي يفوز أوّلاً فيُخفي الآخر.
    /// </summary>
    [Fact]
    public void NeitherAuthoritativeBranch_StillCallsTheLenientParse()
    {
        var body = ResolveAuthoritativeBody(ReadSource());

        Assert.Equal(0, CountLenientParseCalls(body));
        Assert.True(body.Contains("ParseAuthoritative(machine)", StringComparison.Ordinal),
            "‏فرعُ متغيّر الجهاز لا يمرّ بالقراءة الرسميّة — وهو الفرعُ الحيُّ الوحيد.");
        Assert.True(body.Contains("ParseAuthoritative(content)", StringComparison.Ordinal),
            "‏فرعُ ملفّ العلامة لا يمرّ بالقراءة الرسميّة.");
    }

    /// <summary>
    /// <b>واختبارٌ ذاتيٌّ للعدّاد</b> — فحارسٌ يقرأ ملفّاً صحيحاً لا يُثبت أنّه يرفض خاطئاً.
    /// وتُعاد الصيغةُ القديمةُ في الذاكرة ويُطالَب العدّادُ بأن يراها.
    /// </summary>
    [Theory]
    [InlineData("return Parse(machine);", 1)]
    [InlineData("return Parse(content);", 1)]
    [InlineData("return Parse(machine);\nreturn Parse(content);", 2)]
    [InlineData("return ParseAuthoritative(machine);", 0)]   // ‏لا يُخدَع باللاحقة
    [InlineData("// return Parse(machine);", 0)]             // ‏تعليقٌ ليس نداءً
    [InlineData("var s = \"return Parse(machine);\";", 0)]   // ‏نصٌّ حرفيٌّ ليس نداءً
    public void TheStructuralCounter_Discriminates(string snippet, int expected) =>
        Assert.Equal(expected, CountLenientParseCalls(Blank(snippet)));

    // ── الأدوات ─────────────────────────────────────────────────────────────

    /// <summary>‏نداءُ <c>Parse</c> المتساهلة — <b>و<c>ParseAuthoritative</c> ليس منها</b>.</summary>
    private static int CountLenientParseCalls(string cleanedBody)
    {
        var count = 0;
        var i = 0;
        while ((i = cleanedBody.IndexOf("Parse(", i, StringComparison.Ordinal)) >= 0)
        {
            var precededByAuthoritative =
                i >= "Authoritative".Length
                && cleanedBody.AsSpan(i - "Authoritative".Length, "Authoritative".Length)
                    .SequenceEqual("Authoritative");

            if (!precededByAuthoritative) count++;
            i += "Parse(".Length;
        }

        return count;
    }

    private static string ResolveAuthoritativeBody(string source)
    {
        var cleaned = Blank(source);

        // ‏**التصريحُ لا النداء.** وأوّلُ صياغةٍ هنا بحثت عن «ResolveAuthoritative()» فوجدت
        // ‏**نداءَها داخل `Resolve`** فاستخرجت جسمَ `Decide` — وفيه `return Parse(processEnv)`
        // ‏بحقّ، فسقط الحارسُ على كودٍ سليم. **والموقِّعُ يُثبَّت كما يُثبَّت المعدود.**
        const string Declaration = "private static AppEnvironment? ResolveAuthoritative()";
        var at = cleaned.IndexOf(Declaration, StringComparison.Ordinal);
        Assert.True(at >= 0, $"‏لم يُعثر على تصريح «{Declaration}» — تغيّر التوقيعُ فبطل القياس.");

        var open = cleaned.IndexOf('{', at);
        Assert.True(open >= 0, "‏ResolveAuthoritative بلا جسم — القياسُ أجوف.");

        var depth = 0;
        for (var k = open; k < cleaned.Length; k++)
        {
            if (cleaned[k] == '{') depth++;
            else if (cleaned[k] == '}' && --depth == 0)
            {
                var body = cleaned[open..k];

                // ‏شاهدُ ضبطٍ موجب: الجسمُ المُستخرَجُ هو الجسمُ المقصود، لا جسمٌ مجاور.
                Assert.True(body.Contains("EnvironmentVariableTarget.Machine", StringComparison.Ordinal),
                    "‏الجسمُ المُستخرَجُ لا يقرأ نطاقَ الجهاز — استُخرجت دالّةٌ أخرى.");
                Assert.True(body.Contains("AppContext.BaseDirectory", StringComparison.Ordinal),
                    "‏الجسمُ المُستخرَجُ لا يقرأ ملفَّ العلامة — استُخرجت دالّةٌ أخرى.");

                return body;
            }
        }

        throw new InvalidOperationException("‏كتلةٌ غيرُ مُغلقة — المصدرُ لا يُحلَّل.");
    }

    /// <summary>يمسح التعليقاتِ والنصوصَ الحرفيّةَ مُبقياً الأطوالَ — فنداءٌ في تعليقٍ ليس نداءً.</summary>
    private static string Blank(string source)
    {
        var b = new StringBuilder(source);

        void Wipe(int from, int to)
        {
            for (var k = from; k < to && k < source.Length; k++)
                if (source[k] != '\n' && source[k] != '\r') b[k] = ' ';
        }

        for (var i = 0; i < source.Length; i++)
        {
            var next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (source[i] == '/' && next == '/')
            {
                var end = source.IndexOf('\n', i);
                if (end < 0) end = source.Length;
                Wipe(i, end);
                i = end;
            }
            else if (source[i] == '/' && next == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                Wipe(i, end);
                i = end - 1;
            }
            else if (source[i] is '"' or '\'')
            {
                var quote = source[i];
                var verbatim = quote == '"' && i > 0 && source[i - 1] == '@';
                var j = i + 1;

                while (j < source.Length)
                {
                    if (!verbatim && source[j] == '\\') { j += 2; continue; }
                    if (source[j] != quote) { j++; continue; }
                    if (verbatim && j + 1 < source.Length && source[j + 1] == quote) { j += 2; continue; }
                    break;
                }

                Wipe(i, Math.Min(j + 1, source.Length));
                i = j;
            }
        }

        return b.ToString();
    }

    private static string ReadSource()
    {
        var path = Path.Combine(FindRepoRoot(), "src", "Brqosh.Application", "Setup", "AppEnvironment.cs");
        Assert.True(File.Exists(path), $"‏لم يُعثر على {path} — الحارسُ أعمى، ولا يمرّ أعمى.");
        return File.ReadAllText(path);
    }

    /// <summary>‏رميةٌ لا تخطٍّ صامت: حارسٌ لا يجد ما يحرسه يجب أن يسقط، لا أن يمرَّ أخضر.</summary>
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
