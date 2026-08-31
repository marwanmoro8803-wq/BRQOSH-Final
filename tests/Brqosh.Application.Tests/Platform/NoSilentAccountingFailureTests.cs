using System.Text.RegularExpressions;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (CEEF-229) <b>لا يُبتلَع فشلٌ محاسبيّ صامتاً — والقاعدةُ حارسٌ لا إصلاح.</b>
///
/// <para><b>ما استدعى هذا الحارس:</b> في م1-٦ وُجد <c>catch (AccountingException) { }</c>
/// حول توليد مستند إلغاءٍ لدى الهيئة. فحين يفشل التوليدُ لأيّ سبب — هويةٌ ناقصة، شهادةٌ
/// غائبة، حارسٌ من حرّاس هذه الحملة — <b>يُلغى المستندُ في الدفاتر ويبقى قائماً لدى
/// الهيئة</b>، بلا رسالةٍ ولا أثر. وانشقاقٌ كهذا لا يُصلَح بأثرٍ رجعيّ.</para>
///
/// <para><b>ونتيجةُ المسح الذي تلاه كانت سالبة:</b> مُسح المستودعُ كلُّه فلم يُوجد نظيرٌ
/// ثالث. والأربعةُ المرشَّحة مشروعة — <c>try/finally</c> للقياس، وفرعُ مخطَّطٍ مرشَّحٌ
/// بشرطٍ يستثني الإنتاج، وناشرُ أحداثٍ <b>يُسجّل الفشلَ ويُبلّغ</b>، وتابعُ <c>Try…</c>
/// يُعيد <c>null</c> بعقدٍ معلن.</para>
///
/// <para><b>فقيمةُ هذا الملفّ أن تبقى النتيجةُ سالبة.</b> ونتيجةٌ سالبة بلا حارسٍ تُصبح
/// موجبةً عند أوّل دفعة.</para>
/// </summary>
public class NoSilentAccountingFailureTests
{
    /// <summary>
    /// كتلةُ <c>catch</c> تلتقط <see cref="Brqosh.Domain.Accounting.AccountingException"/>
    /// ولا تفعل شيئاً — لا رمياً ولا تسجيلاً ولا إعادةَ قيمةٍ معلنة.
    /// </summary>
    private static readonly Regex SwallowsAccountingException = new(
        @"catch\s*\(\s*AccountingException[^)]*\)\s*\{(?<body>[^{}]*)\}",
        RegexOptions.Compiled);

    /// <summary><b>مواضعُ مُقَرٌّ بها — ولكلٍّ سببٌ مكتوب.</b></summary>
    private static readonly Dictionary<string, string> Declared = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ExchangeRateService.cs"] =
            "‏`TryGetRateAsync` عقدُها في اسمها: تُعيد `decimal?` و`null` تعني «لا سعر». "
            + "‏والمستدعي يقرّر — إمّا يرمي برسالةٍ تُسمّي العملتين والفترة، وإمّا يتخطّى "
            + "‏السطر. فالغيابُ هنا قيمةٌ متوقَّعة لا فشلٌ مبتلَع، والفرقُ أنّ المعلومة "
            + "‏تصل المستدعي بدل أن تُمحى.",
    };

    [Fact]
    public void NoAccountingFailure_IsSwallowedSilently()
    {
        var offenders = new List<string>();

        foreach (var file in SourceFiles())
        {
            // ‏تُنزع التعليقاتُ **قبل** المطابقة لا بعدها.
            //
            // وأوّلُ صياغةٍ نزعتها من جسم الكتلة وحده، فطابقت الصيغةُ **تعليقاً كتبتُه أنا**
            // يشرح العطلَ المُصلَح — فأبلغ الحارسُ عن موضعين أصلحتُهما قبل ساعة. **حارسٌ
            // يقرأ التعليقاتِ يقرأ شرحَ العطل عطلاً.**
            var source = StripComments(File.ReadAllText(file));
            var name = Path.GetFileName(file);

            foreach (Match m in SwallowsAccountingException.Matches(source))
            {
                var body = m.Groups["body"].Value.Trim();

                // ‏فارغٌ تماماً، أو يعود بلا بيانٍ — كلاهما ابتلاع.
                if (body.Length > 0 && !body.StartsWith("return", StringComparison.Ordinal))
                    continue;

                if (Declared.ContainsKey(name))
                    continue;

                var line = source[..m.Index].Count(c => c == '\n') + 1;
                offenders.Add($"{name}:{line}");
            }
        }

        Assert.True(offenders.Count == 0,
            "فشلٌ محاسبيّ يُبتلَع صامتاً — والمستندُ يمضي في الدفاتر ولا يُبلَّغ:\n  "
            + string.Join("\n  ", offenders)
            + "\n\nإمّا يُرمى فيوقف العملية، أو يُسجَّل بأثرٍ يُقرأ، أو يُقَرّ هنا بسببٍ مكتوب.");
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> اسمٌ في قائمة الإقرار لا يقابله ملفٌّ قائم يجعل القائمةَ تكذب
    /// — تُقرّ بما لا وجود له، فتُخفي عن القارئ أنّ الإقرار صار بلا محلّ.
    /// </summary>
    [Fact]
    public void NoDeclaration_OutlivesItsFile()
    {
        var names = SourceFiles().Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        var phantom = Declared.Keys.Where(k => !names.Contains(k)).ToList();

        Assert.True(phantom.Count == 0,
            "إقرارٌ لملفٍّ غير موجود: " + string.Join("، ", phantom));
    }

    private static IEnumerable<string> SourceFiles()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        Assert.True(Directory.Exists(root), $"جذر المصدر غير موجود: {root}");

        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.EndsWith(".g.cs", StringComparison.Ordinal));
    }

    private static string StripComments(string body) =>
        Regex.Replace(Regex.Replace(body, @"/\*.*?\*/", "", RegexOptions.Singleline), @"//[^\n]*", "");
}
