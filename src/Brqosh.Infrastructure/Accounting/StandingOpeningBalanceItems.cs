using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// ‏ <b>الذممُ الافتتاحيّةُ القائمة — قرارٌ واحدٌ يقرؤه كلُّ طرفٍ في الدفتر المساعد.</b>
///
/// <para>‏<b>والقرارُ تاريخيٌّ كالأستاذ</b>: قيدٌ عُكس يبقى في الأستاذ ويُقابله عكسُه <b>بتاريخ العكس</b> — فالذمّةُ تُعدّ في كلّ
/// تاريخٍ قبله، وتسقط من تاريخه. وحذفُها من التاريخ كلِّه (بشرط «مرحَّلٌ» وحدَه) يُخالف الأستاذَ في كلّ مرجعٍ مؤرَّخٍ قبل
/// العكس — وهو ما يمنعه <c>ReversedEntriesStayInBalancesTests</c>. فالرصيدُ والكشفُ والأعمارُ تسأل هنا،
/// <b>ولا يُكرَّر الشرطُ في أيٍّ منها</b> فيُنسى في واحد.</para>
/// </summary>
public static class StandingOpeningBalanceItems
{
    /// <summary>
    /// ‏الذممُ القائمةُ في <paramref name="asOf"/> (أو الآن): دخلت الدفاترَ قبله، ولم يُعكَس قيدُها في تاريخٍ لا يتجاوزه.
    /// </summary>
    public static IQueryable<OpeningBalanceItem> Standing(
        BrqoshDbContext db, long companyId, OpeningBalanceParty party, long? partyId = null, DateOnly? asOf = null) =>
        db.OpeningBalanceItems.Where(i =>
            i.CompanyId == companyId
            && i.Party == party
            && (partyId == null || i.PartyId == partyId)
            && (asOf == null || i.PostingDate <= asOf)
            && !db.JournalEntries.Any(e =>
                e.Id == i.JournalEntryId
                && e.ReversalEntryId != null
                && db.JournalEntries.Any(r => r.Id == e.ReversalEntryId && (asOf == null || r.EntryDate <= asOf))));

    /// <summary>
    /// ‏لكشف الحساب: كلُّ ذمّةٍ دخلت الدفاتر <b>وتاريخُ عكس قيدها إن عُكس</b> — فتُكتب سطراً، ثمّ سطرَ عكسٍ بتاريخه، كما تُكتب
    /// الفاتورةُ الملغاة. فالكشفُ يُطابق الرصيدَ في كلّ سطرٍ لا في آخره وحدَه.
    /// </summary>
    public static async Task<IReadOnlyList<(OpeningBalanceItem Item, DateOnly? ReversedOn)>> ForStatementAsync(
        BrqoshDbContext db, long companyId, OpeningBalanceParty party, long partyId)
    {
        var items = await db.OpeningBalanceItems
            .Where(i => i.CompanyId == companyId && i.Party == party && i.PartyId == partyId)
            .ToListAsync();
        if (items.Count == 0)
            return [];

        var entryIds = items.Select(i => i.JournalEntryId).Distinct().ToList();
        var reversedOn = await db.JournalEntries
            .Where(e => entryIds.Contains(e.Id) && e.ReversalEntryId != null)
            .Join(db.JournalEntries, e => e.ReversalEntryId, r => r.Id, (e, r) => new { e.Id, r.EntryDate })
            .ToDictionaryAsync(x => x.Id, x => x.EntryDate);

        return items
            .Select(i => (i, reversedOn.TryGetValue(i.JournalEntryId, out var on) ? on : (DateOnly?)null))
            .ToList();
    }

    /// <summary>‏نوعُ السطر في كشف الحساب.</summary>
    public const string StatementType = "رصيد افتتاحي";

    /// <summary>‏نوعُ سطر العكس في كشف الحساب.</summary>
    public const string ReversalStatementType = "عكس رصيد افتتاحي";
}
