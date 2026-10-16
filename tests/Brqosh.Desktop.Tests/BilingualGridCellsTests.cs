using System.IO;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٨) <b>خلايا الجداول تبقى عربيّةً بعد التبديل للإنجليزية —
/// و<c>I18nCoverageTests</c> لا يبلغها بالبناء.</b>
///
/// <para><b>ولماذا لا يبلغها:</b> <c>XamlUiLocalizer</c> يترجم <b>خصائصَ العناصر</b> —
/// ومنها <c>DataGridColumn.Header</c> — <b>ولا يبلغ قيمَ الخلايا</b>، فهي تأتي من الكائن
/// المربوط لا من خاصّيّةٍ على عنصر. ومستخرِجُ <c>I18nCoverageTests</c> يلتقط
/// <c>.Text =</c> و<c>.Content =</c> وما يُمرَّر لمساعدات الواجهة — <b>وذراعُ
/// <c>switch</c> تُعيد نصّاً ليست واحدةً منهما</b>.</para>
///
/// <para><b>والعلاجُ ليس إدخالَها في <c>ui-overrides.json</c></b>: السجلُّ يخدم
/// <c>XamlUiLocalizer</c>، وما لا يبلغه المُترجِمُ <b>يبقى عربيّاً والبوّابةُ خضراء</b> —
/// <b>أي يصير الحارسُ كاذباً</b>. وهو بعينه ما يحذّر منه <c>I18nCoverageTests</c> نصّاً في
/// شأن <c>MessageBox</c>. <b>فالعلاجُ <see cref="Brqosh.Desktop.Ui.LocaleManager.T"/></b>
/// عند بناء الصفّ.</para>
///
/// <para><b>وهذه البوّابةُ تُجمّد الدَّينَ ولا تدّعي سدَّه:</b> ما كان يومَ كتابتها
/// <b>٢١٠ مواضعَ في ٣٧ ملفّاً</b> مُقَرٌّ به بعدده، <b>فلا يزيد موضعٌ واحدٌ بعد اليوم</b>،
/// ولا يُنقَص موضعٌ دون أن يُسجَّل نقصُه.</para>
///
/// <para><b>وحدٌّ مُصرَّحٌ به:</b> المسحُ يرى صيغةَ <c>=> "…"</c> وحدَها (ذراعُ
/// <c>switch</c> والعضوُ ذو الجسم التعبيريّ) — وهي الصيغةُ الغالبة. <b>ولا يرى الشرطيَّ
/// الثلاثيَّ العاري</b> (<c>x ? "نعم" : "لا"</c>)، لأنّ نقطتَي الشرطيّ تردان في سياقاتٍ
/// كثيرةٍ لا علاقةَ لها. <b>وذِكرُ الحدّ خيرٌ من ادّعاءِ شمولٍ لا يقع.</b></para>
/// </summary>
public class BilingualGridCellsTests
{
    /// <summary>ذراعٌ تُعيد نصّاً عربيّاً مباشرةً — لا عبر <c>LocaleManager.T</c>.</summary>
    private static readonly Regex ArabicArrowLiteral =
        new(@"=>\s*""[^""\n]*[؀-ۿ][^""\n]*""", RegexOptions.Compiled);

    /// <summary>
    /// <b>السببُ واحدٌ لكلّ هذه الملفّات</b> — فلا يُكرَّر سطراً سطراً: نصُّ خليّةٍ يُبنى في
    /// الشيفرة ولا يبلغه المُترجِم، <b>وترجمتُه تحتاج مراجعةَ مصطلحٍ لكلّ وحدةِ عمل</b>
    /// (فـ«مستخلص» ليست <i>extract</i> بل <i>Progress Billing</i>)، <b>وسَوقُها سوقاً
    /// واحداً يُنتج مصطلحاً خاطئاً بثقة</b>.
    /// </summary>
    private const string Reason =
        "خلايا جدولٍ تُبنى في الشيفرة — لا يبلغها XamlUiLocalizer، وترجمتُها تحتاج مراجعةَ "
        + "مصطلحٍ لكلّ وحدةِ عمل، فلا تُساق سوقاً واحداً.";

    /// <summary>ما كان يومَ سنّ البوّابة — <b>بعدده لا بوجوده</b>.</summary>
    /// <summary>
    /// (ui-ux-i18n-printing#9) <b>الدَّينُ صار صفراً</b>: كان هنا ٣٧ ملفّاً بـ٢١٠ مواضع، وتُرجمت كلُّها
    /// بـ<c>LocaleManager.T</c>؛ والاستثناءُ الوحيدُ (وصفُ حركة المخزون المُخزَّن) بيانُ دفترٍ صار جدولاً.
    /// فلا إقرارَ يُضاف هنا — <b>الموضعُ الجديدُ يُترجَم عند كتابته</b>.
    /// </summary>
    private static readonly Dictionary<string, int> Declared = new(StringComparer.OrdinalIgnoreCase);

    // ── البوّابة ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>لا يزيد موضعٌ، ولا يُنقَص موضعٌ صامتاً.</b>
    ///
    /// <para>ملفٌّ غيرُ مُقَرٍّ به ⟵ سقوط: <b>خليّةٌ جديدةٌ تبقى عربيّةً بالإنجليزية</b>.
    /// وعددٌ يزيد على المُقَرّ ⟵ سقوط: <b>موضعٌ جديدٌ اختبأ تحت غطاء إقرارٍ قديم</b>.</para>
    /// </summary>
    [Fact]
    public void NoNewUntranslatedGridCell_IsAddedBeyondTheFrozenDebt()
    {
        var found = Scan();
        var problems = new List<string>();

        foreach (var (path, count) in found.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!Declared.TryGetValue(path, out var declared))
            {
                problems.Add($"{path} — {count} نصَّ خليّةٍ يبقى عربياً بالإنجليزية بلا إقرار. "
                           + "استعمل LocaleManager.T(عربي، english) عند بناء الصفّ.");
                continue;
            }

            if (declared != count)
                problems.Add($"{path} — أُقرّ له {declared} والموجودُ {count}: "
                           + (count > declared
                               ? "موضعٌ جديدٌ دخل تحت غطاء إقرارٍ قديم."
                               : "نقص الدَّينُ ولم يُحدَّث الإقرار — فالتقدّمُ لا يُقاس."));
        }

        foreach (var path in Declared.Keys.Where(k => !found.ContainsKey(k))
                     .OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            problems.Add($"{path} — إقرارٌ باقٍ ولا موضعَ فيه: أثرُ عملٍ تمّ ولم يُسجَّل.");

        Assert.True(problems.Count == 0,
            "خلايا جداولٍ تبقى عربيّةً بعد التبديل للإنجليزية — ولا يبلغها مسحُ "
            + "I18nCoverageTests بالبناء:\n  " + string.Join("\n  ", problems));
    }

    // ── حارسُ الحارس ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>ولو عمي المسحُ لمرّت البوّابةُ خضراءَ وهي لا ترى شيئاً.</b>
    ///
    /// <para>كان الشاهدُ الحيُّ هو الدَّينَ نفسَه (≥٢٠٠ موضع)؛ <b>وبعد أن صار صفراً</b> يُقاس النمطُ
    /// على شاهدين مصطنعين: ذراعٌ عربيّةٌ عارية تُعدّ، والمعالَجةُ لا تُعدّ.</para>
    /// </summary>
    [Fact]
    public void TheScan_StillSeesABareArabicCell()
    {
        const string bare = "Status.Posted => \"مرحّل\",";
        const string fixedCell = "Status.Posted => LocaleManager.T(\"مرحّل\", \"Posted\"),";

        Assert.Single(ArabicArrowLiteral.Matches(StripComments(bare)));
        Assert.Empty(ArabicArrowLiteral.Matches(StripComments(fixedCell)));
        Assert.Empty(ArabicArrowLiteral.Matches(StripComments("// Status.Posted => \"مرحّل\"")));
    }

    /// <summary>
    /// <b>وصيغةُ العلاج يراها المسحُ علاجاً.</b>
    ///
    /// <para>شاشةُ مستندات ZATCA وصنفُ عرضها <b>عولجا بـ<c>LocaleManager.T</c></b> في ح-١٨،
    /// <b>فغيابُهما عن النتائج هو الدليلُ الحيُّ</b> على أنّ البوّابةَ تُفرّق بين النصِّ
    /// المُترجَم والنصِّ العاري — <b>ولولاه لجاز أن تكون «نظيفةً» لأنّها لا ترى الصيغتين
    /// معاً</b>.</para>
    /// </summary>
    [Fact]
    public void TheFixedScreens_AreNotReportedAsDebt()
    {
        var found = Scan();

        Assert.DoesNotContain("Views/Pages/ZatcaInvoicesPage.xaml.cs", found.Keys);
        Assert.DoesNotContain("Ui/ZatcaDocumentPresentation.cs", found.Keys);
    }

    // ── المسح ───────────────────────────────────────────────────────────────

    private static Dictionary<string, int> Scan()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Brqosh.Desktop"));
        Assert.True(Directory.Exists(root), $"Desktop source root not found: {root}");

        var found = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".g.cs", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            var count = ArabicArrowLiteral.Matches(StripComments(File.ReadAllText(file))).Count;
            if (count == 0) continue;

            found[Path.GetRelativePath(root, file).Replace('\\', '/')] = count;
        }

        return found;
    }

    /// <summary>يُسقط التعليقات — <b>فشرحُ النمط ليس وقوعاً له</b>.</summary>
    private static string StripComments(string source)
    {
        var text = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(text, @"^\s*//.*$", "", RegexOptions.Multiline);
    }
}
