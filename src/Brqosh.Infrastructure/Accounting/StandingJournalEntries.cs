using Brqosh.Domain.Accounting;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// ‏<b>«قائمٌ» — مرحّلٌ لم يُعكَس، وليس هو نفسُه عكساً لغيره.</b> مالكٌ واحدٌ لسؤالٍ تكرّر.
///
/// <para>‏<b>وقيدُ العكس يرث <c>SourceDocument</c> من أصله</b> (<c>CreateReversal</c>)، <b>فيبقى
/// <c>Posted</c> تحت المفتاح نفسِه بعد الإلغاء</b>. ولا وسمَ يقول «أنا عكس» — <b>بل الأصلُ هو من
/// يُشير إليه</b> بـ<c>ReversalEntryId</c>. فالتمييزُ أنّ لا قيدَ يُسمّيني عكساً له.</para>
///
/// <para>‏<b>وكُتب السؤالُ بيده ثلاثَ مرّاتٍ قبل أن يكون له مالك</b> — مرّتين في
/// <c>MonetaryRevaluationService</c> (ح-٣٨) ومرّةً في <c>VatReturnQuery</c> (ض-٣) — <b>وكانت
/// الرابعةُ ستُكتب في الإقفال السنويّ</b> (ح-٤٧). <b>وعكسُه — <c>Status != Draft</c> — هو عينُ
/// العطل في الموضعين</b>: المعكوسُ ليس مسودّةً، فيبقى الحارسُ يرفض بعد العكس تماماً.</para>
///
/// <para>‏<b>وليس هذا نقضاً لـح-٢٦</b>: ذاك في الأرصدة (<c>WhereReportable</c>) حيث يبقى
/// المعكوسُ وعكسُه معاً فيتقاصّان — <b>وهذا سؤالُ وجودٍ لا سؤالُ مبلغ</b>.</para>
/// </summary>
public static class StandingJournalEntries
{
    public static IQueryable<JournalEntry> WhereStanding(
        this IQueryable<JournalEntry> query, IQueryable<JournalEntry> allEntries) =>
        query.Where(e => e.Status == JournalEntryStatus.Posted
                         && !allEntries.Any(o => o.ReversalEntryId == e.Id));
}
