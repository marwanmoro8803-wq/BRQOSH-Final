using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>
/// (أسئلةُ المالك · طباعةٌ بالإنجليزيّة) <b>لغةُ المطبوع واتجاهُه — طبقةٌ مركزيّةٌ واحدة.</b>
///
/// <para><b>ما كان:</b> كلُّ مولِّدٍ يكتب تسمياتِه العربيّةَ حرفيّاً ويفترض الاتجاهَ يمينٌ-يسار، فتبديلُ لغة الواجهة لا يمسّ المطبوعات.</para>
///
/// <para><b>الآن:</b> المولِّدُ يفتح نطاقاً <see cref="Begin"/> بلغة <see cref="PrintBranding.English"/>، وكلُّ تسميةٍ تمرّ بـ<see cref="T"/>
/// (قاموسُ عربيّ ← إنجليزيّ في <see cref="PrintLangDictionary"/>)، وما ليس فيه يبقى كما هو — <b>وهذا مقصودٌ لبيانات المستخدم</b>
/// (أسماءُ الحسابات والعملاء) فلا تُترجَم. والاتجاهُ يُقرأ من النطاق نفسِه: <see cref="Flow"/> و<see cref="AlignStart"/>.</para>
///
/// <para><b>والفواتيرُ الضريبيّةُ (المبيعات والمستخلص) تبقى عربيّةً دائماً</b> — الهيئةُ تُلزم بالعربيّة في الفاتورة الضريبيّة، ومولِّداها يفتحان
/// نطاقاً عربيّاً صريحاً.</para>
/// </summary>
public static class PrintLang
{
    private static readonly AsyncLocal<bool> EnglishSlot = new();

    /// <summary>لغةُ المطبوعات الافتراضيّة — تضبطها الواجهةُ من لغتها عند التبديل. المولِّداتُ لا تقرؤها؛ هي لـ<see cref="PrintBranding.FromCompany"/> وحدَه.</summary>
    public static bool Preferred { get; set; }

    public static bool English => EnglishSlot.Value;

    /// <summary>يفتح نطاقاً بلغةٍ معيّنة ويُعيد السابقَ عند التخلّص.</summary>
    public static IDisposable Begin(bool english)
    {
        var previous = EnglishSlot.Value;
        EnglishSlot.Value = english;
        return new Scope(previous);
    }

    private sealed class Scope(bool previous) : IDisposable
    {
        public void Dispose() => EnglishSlot.Value = previous;
    }

    /// <summary>تسميةٌ بلغة النطاق: العربيّةُ كما هي، والإنجليزيّةُ من القاموس (وإن غابت فالأصل).</summary>
    public static string T(string arabic)
    {
        if (!English)
            return arabic;
        if (PrintLangDictionary.Map.TryGetValue(arabic, out var english))
            return english;
        if (arabic.Any(c => c is >= '؀' and <= 'ۿ'))
            Misses[arabic] = 0;
        return arabic;
    }

    /// <summary>نصوصٌ عربيّةٌ مرّت بـ<see cref="T"/> في نطاقٍ إنجليزيٍّ ولا مقابلَ لها — تقرؤها الاختباراتُ فيسقط ما فات القاموس.</summary>
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> Misses = new(StringComparer.Ordinal);

    /// <summary>قالبٌ بوسائط <c>{0}</c> — القالبُ العربيّ هو المفتاح.</summary>
    public static string TF(string arabicTemplate, params object?[] args) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, T(arabicTemplate), args);

    /// <summary>حاويةٌ تُرتَّب فيها العناصرُ بترتيب القراءة: من اليمين للعربيّة، ومن اليسار للإنجليزيّة.</summary>
    public static IContainer Flow(this IContainer container) =>
        English ? container.ContentFromLeftToRight() : container.ContentFromRightToLeft();

    /// <summary>بدايةُ السطر: يمينٌ للعربيّة ويسارٌ للإنجليزيّة.</summary>
    public static IContainer AlignStart(this IContainer container) =>
        English ? container.AlignLeft() : container.AlignRight();

    /// <summary>نهايةُ السطر: يسارٌ للعربيّة ويمينٌ للإنجليزيّة.</summary>
    public static IContainer AlignEnd(this IContainer container) =>
        English ? container.AlignRight() : container.AlignLeft();
}
