using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-231 · ط1-٤ب) <b>كلُّ مستندٍ يحمل عملةً في النطاق، تصل إليها شاشتُه.</b>
///
/// <para><b>ما كان:</b> بُنيت العملاتُ في <c>ط1-٤</c> ثمّ في <c>ط4-٥</c> — الفاتورةُ
/// والمشترياتُ والسنداتُ كلُّها تحمل <c>SetCurrency</c>، <b>وفرقُ العملة المحقَّق يُقيَّد
/// عند السداد</b>. <b>ولم يصل إلى السنداتِ حقلٌ واحد.</b> فيُدخل المحاسبُ التحصيلَ
/// الدولاريَّ محوَّلاً بيده: <b>تضيع القيمةُ الأصلية</b>، ولا يُقيَّد فرقٌ محقَّق —
/// والحسابان <c>4108</c> و<c>5218</c> بلا كاتبٍ من الشاشة — <b>وتبقى الذمّةُ مفتوحةً
/// بفرقٍ لا يُغلق</b>.</para>
///
/// <para><b>ولماذا حارسٌ صنفيٌّ لا اختبارُ شاشة:</b> اختبارٌ لكلّ شاشةٍ يُغلق <b>الحالات</b>
/// المعروفة اليوم، <b>ومستندٌ يُمنح عملةً غداً يولد بلا حقل</b> — وهو بعينه ما وقع:
/// أُضيفت العملةُ إلى الفاتورة فصار لها حقل، وأُضيفت إلى السندات فلم يُنتبه.
/// <b>فهذا يقيس القاعدةَ: مَن حمل <c>SetCurrency</c> في النطاق يجد له حقلاً في شاشته.</b></para>
///
/// <para><b>وحدُّه مُعلَن</b>: يُثبت أنّ للشاشة مسلكاً إلى العملة، <b>لا أنّ الرقمَ الناتجَ
/// صحيح</b> — ذلك تقيسه <c>RealizedFxTests</c> على النطاق نفسِه.</para>
/// </summary>
public class DocumentCurrencyEntryTests
{
    /// <summary>
    /// المستنداتُ التي تحمل عملةً — <b>واسمُ شاشتها المتوقَّع</b>.
    ///
    /// <para><b>وإقرارٌ بغياب شاشةٍ يُكتب بسببه</b>: مستندٌ لا شاشةَ له اليومَ ليس عطلَ
    /// عملة.</para>
    /// </summary>
    private static readonly Dictionary<string, string?> DialogForDocument = new(StringComparer.Ordinal)
    {
        ["SalesInvoice"] = "SalesInvoiceDialog",
        ["SalesOrder"] = "SalesOrderDialog",
        ["PurchaseInvoice"] = "PurchaseInvoiceDialog",
        ["ReceiptVoucher"] = "ReceiptVoucherDialog",
        ["PaymentVoucher"] = "PaymentVoucherDialog",

        // (CEEF-231 · ط1-٤ب) ‏**والحسابُ البنكيّ كشفه هذا الحارسُ فور كتابته**: النطاقُ
        // ‏يحمل عملتَه، **وشاشةُ إعادة تقييم العملات مبنيّةٌ لتقييمه** — ولا حقلَ يُنشئ
        // ‏حساباً بالدولار أصلاً، فتبقى تلك الشاشةُ بلا شيءٍ تُقيّمه.
        ["BankAccount"] = "BankAccountDialog",

        // ‏(جاهزيّةُ الإطلاق · ح-٣٥) **والإشعارُ الدائنُ بلا حقلِ عملةٍ عمداً.
        //
        // ‏المرتجعُ **يردّ ما قُبِض بسعرِ صرفِه يومَ قُبِض**، فالعملةُ
        // ‏والسعرُ **يُورَّثان من الفاتورة الأصليّة ولا يُختاران**.
        //
        // ‏**وحقلٌ هنا عطلٌ لا ميزة**: يُجيز ردّ فاتورةٍ دولاريّةٍ باليورو،
        // ‏أو بسعرِ اليوم — **فيُخلَق فرقُ صرفٍ لم يقع**، ولا يُصفّي
        // ‏مرتجعٌ كاملٌ ذمّةً أبداً.
        //
        // ‏**والوراثةُ مقيسةٌ لا موعودة** — في
        // ‏`CreditNoteCarriesItsInvoiceCurrencyTests`، ولولاها لَكان هذا الإقرارُ دعوى.
        ["CreditNote"] = null,

        // ‏(جاهزيّةُ الإطلاق · ح-٣٩) **والإشعارُ المدينُ كذلك — يرث ولا يختار**:
        // ‏`DebitNoteDraftBuilder` ينقل عملةَ فاتورة الشراء وسعرَها، **ومنتقي عملةٍ هنا
        // ‏عطلٌ لا ميزة**. والوراثةُ مقيسةٌ في `DebitNoteCarriesItsInvoiceCurrencyTests`.
        ["DebitNote"] = null,
    };

    /// <summary>
    /// <b>القياسُ المركزيّ: كلُّ مستندٍ بعملةٍ له حقلٌ يصل إليها.</b>
    /// </summary>
    [Fact]
    public void EveryCurrencyBearingDocument_HasACurrencyFieldInItsDialog()
    {
        var missing = new List<string>();

        foreach (var (document, dialog) in DialogForDocument)
        {
            if (dialog is null) continue;

            var markup = DialogMarkup(dialog);
            var source = DialogSource(dialog);

            if (markup is null || source is null)
            {
                missing.Add($"{document} — شاشتُه «{dialog}» غير موجودة.");
                continue;
            }

            if (!markup.Contains("x:Name=\"CurrencyBox\"", StringComparison.Ordinal))
            {
                missing.Add($"{document} — «{dialog}» بلا حقل عملة.");
                continue;
            }

            // ‏**وحقلٌ يُعرض ولا يُقرأ أسوأُ من غيابه**: يُدخل المستخدمُ عملةً ويظنّها حُفظت.
            if (!source.Contains("CurrencyBox", StringComparison.Ordinal))
                missing.Add($"{document} — «{dialog}» يعرض حقلَ عملةٍ ولا يقرؤه.");
        }

        Assert.True(missing.Count == 0,
            "مستنداتٌ تحمل عملةً في النطاق ولا يصل إليها حقلٌ في شاشتها — "
            + "فتُدخَل القيمةُ محوَّلةً بيدٍ وتضيع الأصلية، ولا يُقيَّد فرقُ عملةٍ محقَّق:\n  "
            + string.Join("\n  ", missing));
    }

    /// <summary>
    /// <b>وحارسٌ للحارس: كلُّ اسمٍ في الجدول مستندٌ قائمٌ يحمل <c>SetCurrency</c> فعلاً.</b>
    ///
    /// <para>فاسمٌ يبقى بعد أن نُزعت عنه العملةُ <b>يجعل الحارسَ يحرس شيئاً لا وجودَ له</b>،
    /// ومستندٌ يُمنح عملةً ولا يُدرَج هنا <b>يمرّ بلا قياس</b>.</para>
    /// </summary>
    [Fact]
    public void TheDocumentList_MatchesTheDomain()
    {
        var bearers = typeof(Brqosh.Domain.Accounting.JournalEntry).Assembly
            .GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract
                        && t.GetMethod("SetCurrency", BindingFlags.Public | BindingFlags.Instance) is not null)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        var undeclared = bearers.Except(DialogForDocument.Keys).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var stale = DialogForDocument.Keys.Except(bearers).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(undeclared.Count == 0,
            "مستنداتٌ تحمل عملةً ولم تُدرَج في هذا الحارس — فتمرّ بلا قياس:\n  "
            + string.Join("\n  ", undeclared));

        Assert.True(stale.Count == 0,
            "أسماءٌ في الجدول لم تعد تحمل عملةً — والحارسُ يحرس شيئاً لا وجودَ له:\n  "
            + string.Join("\n  ", stale));
    }

    /// <summary>
    /// (CEEF-231 · O-34) <b>وكلُّ شاشةٍ تعرض حقلَ عملةٍ تقرؤه — والاتجاهُ الثاني للقاعدة.</b>
    ///
    /// <para>الحارسُ أعلاه يمشي من النطاق إلى الشاشة، <b>فلا يرى حقلاً أُضيف إلى شاشةٍ
    /// لا يحمل مستندُها <c>SetCurrency</c></b> — كشاشة القيد اليدويّ، <b>وعملتُه مذكرةٌ
    /// على السطر لا خاصيّةٌ على الرأس</b>. وهذا يمشي من الشاشة إلى الشيفرة.</para>
    ///
    /// <para><b>وحقلٌ يُعرض ولا يُقرأ أسوأُ من غيابه</b>: يُدخل المحاسبُ «USD» ويرى
    /// الحقلَ مملوءاً <b>ويُرحَّل القيدُ بالأرقام كأنّها ريالات</b> — وهو خطأٌ لا يظهر
    /// في ميزانٍ لأنّه متوازن.</para>
    /// </summary>
    [Fact]
    public void EveryDialogThatShowsACurrencyField_ReadsIt()
    {
        var root = FindRepoDirectory("src/Brqosh.Desktop");
        if (root is null) return;   // ‏المصادرُ غيرُ متاحةٍ في هذا التشغيل.

        var decorative = new List<string>();

        foreach (var markupPath in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
        {
            if (markupPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                markupPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            if (!File.ReadAllText(markupPath).Contains("x:Name=\"CurrencyBox\"", StringComparison.Ordinal))
                continue;

            var codePath = markupPath + ".cs";
            if (!File.Exists(codePath))
            {
                decorative.Add($"{Path.GetFileName(markupPath)} — بلا شيفرةٍ خلفية.");
                continue;
            }

            var source = Regex.Replace(File.ReadAllText(codePath), @"/\*.*?\*/", " ", RegexOptions.Singleline);
            source = Regex.Replace(source, @"//.*?$", " ", RegexOptions.Multiline);

            if (!source.Contains("CurrencyBox", StringComparison.Ordinal))
                decorative.Add($"{Path.GetFileName(markupPath)} — يعرض حقلَ عملةٍ ولا يقرؤه.");
        }

        Assert.True(decorative.Count == 0,
            "شاشاتٌ تعرض حقلَ عملةٍ ولا تقرؤه — فيُدخل المستخدمُ عملةً ويُرحَّل المستندُ "
            + "بأرقامها كأنّها ريالات، وهو خطأٌ متوازنٌ لا يكشفه ميزان:\n  "
            + string.Join("\n  ", decorative));
    }

    /// <summary>وسمُ الشاشة — <b>وفيه يُعلَن الحقل</b>.</summary>
    private static string? DialogMarkup(string dialog)
    {
        var root = FindRepoDirectory("src/Brqosh.Desktop");
        if (root is null) return null;

        var path = Path.Combine(root, "Views", "Pages", $"{dialog}.xaml");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>نصُّ شاشةٍ بلا تعليقات — <b>فذِكرُ الاسم في تعليقٍ ليس استدعاءً</b>.</summary>
    private static string? DialogSource(string dialog)
    {
        var root = FindRepoDirectory("src/Brqosh.Desktop");
        if (root is null) return null;

        var path = Path.Combine(root, "Views", "Pages", $"{dialog}.xaml.cs");
        if (!File.Exists(path)) return null;

        var text = Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(text, @"//.*?$", " ", RegexOptions.Multiline);
    }

    private static string? FindRepoDirectory(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }
}
