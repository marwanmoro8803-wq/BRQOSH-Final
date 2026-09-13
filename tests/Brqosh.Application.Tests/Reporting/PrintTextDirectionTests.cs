using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Brqosh.Infrastructure.Reporting;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-226) التواريخ والأرقام اللاتينية في المطبوعات معزولة عن إعادة الترتيب.
///
/// **العلّة مقيسة بالعين على PDF مولَّد:** الكود يكتب <c>التاريخ: 2026-03-14</c>
/// والمطبوع يعرض <c>التاريخ: 14-03-2026</c> — الخوارزمية ثنائية الاتجاه تعكس مقاطع
/// الرقم داخل فقرة عربية. والتذييل في **الوثيقة نفسها** يعرض <c>2026-07-25</c>
/// صحيحاً لأنّه يبدأ بحرف لاتينيّ. صيغتان متناقضتان في ورقة واحدة، ومستندٌ ماليّ
/// لا يحتمل لبساً في التاريخ.
///
/// الحارس يمنع رجوع الصيغة الخام: أيّ <c>{x:yyyy-MM-dd}</c> داخل نصّ مطبوع يجب أن
/// يمرّ عبر <see cref="PrintText"/>.
/// </summary>
public class PrintTextDirectionTests
{
    private const char Lri = '⁦';
    private const char Pdi = '⁩';

    [Fact]
    public void Date_IsIsoAndIsolated()
    {
        var text = PrintText.Date(new DateOnly(2026, 3, 14));

        Assert.Equal($"{Lri}2026-03-14{Pdi}", text);
    }

    [Fact]
    public void Timestamp_IsIsolated()
    {
        var text = PrintText.Timestamp(new DateTime(2026, 3, 14, 21, 5, 0));

        Assert.Equal($"{Lri}2026-03-14 21:05{Pdi}", text);
    }

    [Fact]
    public void Code_IsolatesDocumentNumbers()
    {
        Assert.Equal($"{Lri}INV-2026-000001{Pdi}", PrintText.Code("INV-2026-000001"));
    }

    /// <summary>
    /// ‏(م-٥ · ن-٤٧) <b>والتقويمُ لا يتبع الجهاز</b> — وهذا ما يفعله <c>PrintText</c> زيادةً
    /// على العزل، وهو نصفُ العطل الذي قِيس.
    ///
    /// <para><b>وبناءُ الثقافة هنا مقصودٌ حرفيّاً:</b> <c>new CultureInfo(name, useUserOverride: false)</c>.
    /// فـ<c>CultureInfo.GetCultureInfo</c> و<c>CurrentCulture</c> <b>يحترمان تجاوزَ المستخدم في
    /// لوحة التحكّم</b> — وعلى هذه الآلة تحديداً يجعل ذلك <c>ar-SA</c> ميلاديّةً،
    /// <b>فيمرّ اختبارٌ مكتوبٌ بها أخضرَ والعطلُ قائم</b>. والقيمةُ الافتراضيّةُ للثقافة
    /// بلا تجاوزٍ هي ما يقع على جهازٍ لم يُبدَّل إعدادُه.</para>
    /// </summary>
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("fa-IR")]
    [InlineData("th-TH")]
    public void Date_IgnoresTheMachineCalendar(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var machine = new CultureInfo(culture, useUserOverride: false);
            CultureInfo.CurrentCulture = machine;

            // ‏شاهدُ ضبطٍ موجب: هذه الثقافةُ فعلاً تُغيّر التقويمَ — وإلّا فالقياسُ أجوف.
            Assert.NotEqual("2026-03-14", new DateOnly(2026, 3, 14).ToString("yyyy-MM-dd"));

            Assert.Equal($"{Lri}2026-03-14{Pdi}", PrintText.Date(new DateOnly(2026, 3, 14)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>نصّ فارغ لا يُلفّ — وإلا امتلأت المطبوعات بمحارف تحكّم بلا محتوى.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyValues_AreNotWrapped(string? value)
    {
        Assert.Equal(string.Empty, PrintText.Code(value));
    }


    /// <summary>
    /// ‏(م-٥ · ن-٤٧) <b>الحارسُ البنيويّ — وقد قُلب من قائمةِ منعٍ إلى قائمةِ سماح.</b>
    ///
    /// <para><b>ما كان:</b> نمطٌ واحد <c>\{[^{}]*:yyyy-MM-dd[^{}]*\}</c> يُطبَّق سطراً سطراً.
    /// وهو يرى <b>صيغةَ الإقحام وحدَها</b>، و<c>date.ToString("yyyy-MM-dd")</c> لا يُطابقه.
    /// <b>فالبوّابةُ أعلنت هذا الحدَّ بعينِه — «لا مولّد PDF يطبع تاريخاً بالصيغة الخام» —
    /// ثمّ قاست واحدةً من طريقَي عبوره</b>، ومرّت خضراءَ على موضعين قائمين.</para>
    ///
    /// <para><b>ولمَ قائمةُ سماح:</b> قائمةُ المنع تُلاحق الهجاءَ، وكلُّ هجاءٍ جديدٍ ثغرة —
    /// قِيس أنّ <c>ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)</c> يمرّ منها
    /// <b>وهو صحيحُ التقويم عاري الاتّجاه</b>، أي يُسقط بالضبط ما وُجدت هذه البوّابةُ له.
    /// <b>فالمطلوبُ إيجابيّ</b>: في مولّدات الـPDF، أيُّ تنسيقِ تاريخٍ يجب أن يقع
    /// <b>داخل نداء <c>PrintText.</c></b> — ولا يُسأل عن هجائه.</para>
    ///
    /// <para><b>والملفُّ يُقرأ كاملاً لا سطراً سطراً</b>: لفُّ السطر من المُحرِّر كان يُعمي
    /// القراءةَ السطريّة. <b>والتعليقاتُ تُمحى</b> — وهذا التعليقُ نفسُه يذكر الصيغةَ الخام.</para>
    ///
    /// <para><b>وحدُّه مُعلَنٌ ولا يُقرأ أوسعَ منه:</b> يمسح <c>*PdfGenerator.cs</c>
    /// و<c>PrintBranding.cs</c> في مجلّد التقارير. <b>ولا يمسح</b> ما يُنسّق تاريخاً في
    /// طبقةٍ أخرى ثمّ يُسلّمه نصّاً جاهزاً إلى مولّد — وذلك طريقٌ ثالثٌ قائمٌ فُتح في ن-٤٧
    /// (<c>PosReportQuery</c>) وأُغلق بالقياس لا بالبوّابة — <b>ولا يمسح سجلّاتِ التدقيق
    /// ولا أسماءَ الملفّات ولا شبكاتِ سطح المكتب</b>، وتلك بنودٌ مستقلّةٌ بقياسها.</para>
    /// </summary>
    [Fact]
    public void EveryPrintedDate_GoesThroughPrintText()
    {
        var offenders = new List<string>();

        foreach (var file in PrintedSurfaceFiles())
        {
            var cleaned = Blank(File.ReadAllText(file));
            foreach (Match m in DateFormat.Matches(cleaned))
            {
                if (IsInsidePrintTextCall(cleaned, m.Index)) continue;
                var line = cleaned.Take(m.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(file)}:{line} → {m.Value.Trim()}");
            }
        }

        Assert.True(offenders.Count == 0,
            "‏تنسيقُ تاريخٍ في مطبوعٍ خارجَ PrintText — يتبع تقويمَ الجهاز ويُعكس في الفقرة العربية:\n  - "
            + string.Join("\n  - ", offenders));
    }

    /// <summary>‏شاهدُ ضبطٍ موجب: الماسحُ يرى الملفّاتِ فعلاً، ويجد فيها نداءاتِ PrintText.</summary>
    [Fact]
    public void TheScanner_ReachesThePrintedSurface_AndSeesItsLegitimateCalls()
    {
        var files = PrintedSurfaceFiles();
        Assert.True(files.Count >= 8, $"‏الماسحُ رأى {files.Count} ملفّاً فقط — المسارُ مشكوكٌ فيه.");

        var withPrintText = files.Count(f => File.ReadAllText(f).Contains("PrintText.", StringComparison.Ordinal));
        Assert.True(withPrintText >= 5,
            $"‏{withPrintText} ملفّاً وحدَه يُنادي PrintText — فالماسحُ أعمى أو الطبقةُ تغيّرت.");
    }

    /// <summary>
    /// <b>واختبارٌ ذاتيٌّ للبوّابة</b> — والحالتان (ج) و(د) هما اللتان أسقطتا سابقتَها:
    /// هجاءٌ لم يكن في قائمة المنع، وصيغةٌ صحيحةُ التقويم عاريةُ الاتّجاه.
    /// </summary>
    [Theory]
    [InlineData("table.Cell().Text(d.ToString(\"yyyy-MM-dd\"));", true)]                       // ‏(أ)
    [InlineData("var s = $\"{asOf:yyyy-MM-dd}\";", true)]                                        // ‏(ب)
    [InlineData("d.ToString(\"yyyy-MM-dd\", CultureInfo.CurrentCulture)", true)]                 // ‏(ج) هجاءٌ آخر
    [InlineData("d.ToString(\"yyyy-MM-dd\", CultureInfo.InvariantCulture)", true)]               // ‏(د) عاري الاتّجاه
    [InlineData("table.Cell().Text(\n    d.ToString(\"yyyy-MM-dd\"));", true)]                   // ‏(هـ) سطران
    [InlineData("table.Cell().Text(PrintText.Date(d));", false)]
    [InlineData("$\"من {PrintText.Date(from)} إلى {PrintText.Date(to)}\"", false)]
    [InlineData("// d.ToString(\"yyyy-MM-dd\")", false)]                                         // ‏تعليقٌ ليس كوداً
    [InlineData("x.ToString(\"N2\")", false)]                                                    // ‏مبلغٌ لا تاريخ
    [InlineData("id.ToString()", false)]
    [InlineData("var a = b.IsComplete\n        ? c.ToPrintLine()\n        : d;", false)]   // ‏كتلةُ كودٍ لا مُحدِّد
    [InlineData("if (x) { y = z; }", false)]
    public void TheGate_Discriminates(string snippet, bool shouldBeCaught)
    {
        var cleaned = Blank(snippet);
        var caught = DateFormat.Matches(cleaned).Any(m => !IsInsidePrintTextCall(cleaned, m.Index));

        Assert.Equal(shouldBeCaught, caught);
    }

    // ── الماسح ───────────────────────────────────────────────────────────────

    /// <summary>‏تنسيقُ تاريخ — بالهجاءَين: <c>ToString("…")</c> وفتحةُ إقحامٍ بمُحدِّد.</summary>
    private static readonly Regex DateFormat = new(
        // ‏**وفتحةُ الإقحام لا تعبر سطراً.** أوّلُ صياغةٍ سمحت بذلك فطابقت **كتلةَ كودٍ**
        // ‏فيها نقطتان وحرفا `dd` — فسقط الحارسُ على `PrintBranding.cs:23` وهو سليم.
        // ‏فالمُحدِّدُ قصيرٌ وفي سطرٍ واحد، كما هو في لغة C#.
        @"\{[^{}""
]{0,60}:[^{}""
]{0,30}(?:yyyy|MMM|dd|HH:mm)[^{}""
]{0,30}\}"
        + @"|\.ToString\s*\(\s*""[^""]*(?:yyyy|MMM|dd|HH:mm)[^""]*""",
        RegexOptions.Compiled);

    /// <summary>‏هل تقع المطابقةُ داخلَ نداءِ <c>PrintText.</c>؟ — يُقرأ ما قبلها بعدِّ الأقواس.</summary>
    private static bool IsInsidePrintTextCall(string cleaned, int at)
    {
        var depth = 0;
        for (var i = at; i >= 0; i--)
        {
            if (cleaned[i] == ')') depth++;
            else if (cleaned[i] == '(')
            {
                if (depth == 0)
                {
                    var head = cleaned[Math.Max(0, i - 40)..i];
                    if (head.Contains("PrintText.", StringComparison.Ordinal)) return true;
                    depth = 0;
                }
                else depth--;
            }
        }
        return false;
    }

    private static List<string> PrintedSurfaceFiles()
    {
        var root = Path.Combine(FindRepoRoot(), "src", "Brqosh.Infrastructure", "Reporting");
        Assert.True(Directory.Exists(root), $"‏مجلّدُ التقارير غير موجود: {root}");

        return [.. Directory.EnumerateFiles(root, "*PdfGenerator.cs")
            .Concat(Directory.EnumerateFiles(root, "PrintBranding.cs"))];
    }

    /// <summary>يمسح التعليقاتِ مُبقياً الأسطر — <b>والنصوصُ الحرفيّةُ تبقى</b>، فالصيغةُ نصّ.</summary>
    private static string Blank(string source)
    {
        var b = new System.Text.StringBuilder(source);

        for (var i = 0; i < source.Length - 1; i++)
        {
            int end;
            if (source[i] == '/' && source[i + 1] == '/')
            {
                end = source.IndexOf('\n', i);
                if (end < 0) end = source.Length;
            }
            else if (source[i] == '/' && source[i + 1] == '*')
            {
                end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
            }
            else continue;

            for (var k = i; k < end; k++)
                if (source[k] != '\n' && source[k] != '\r') b[k] = ' ';
            i = end - 1;
        }

        return b.ToString();
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
