using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Security;

/// <summary>نوع الكسر المكتشف في سلسلة التدقيق.</summary>
public enum AuditChainBreakKind
{
    /// <summary>محتوى الإدخال لا يطابق تجزئته — عُدِّل حقل بعد الحفظ.</summary>
    TamperedContent,

    /// <summary>تجزئة السابق لا تطابق الإدخال الذي قبله — أُقحم أو أُبدل صفّ.</summary>
    BrokenLink,

    /// <summary>
    /// (CEEF-229) صفٌّ غيرُ مربوطٍ ظهر **بعد** بدء السلسلة — أتى من خارج التطبيق.
    /// `AuditService` هو الكاتبُ الوحيد وهو يربط دائماً، فلا سبيلَ إلى صفٍّ كهذا من الشفرة.
    /// </summary>
    UnchainedEntry,

    /// <summary>فجوة في الترتيب — حُذف إدخال أو أكثر.</summary>
    MissingEntry,
}

public record AuditChainBreak(
    AuditChainBreakKind Kind,
    long Sequence,
    long EntryId,
    string Description);

public record AuditChainResult(long CompanyId, int EntriesChecked, IReadOnlyList<AuditChainBreak> Breaks)
{
    public bool IsIntact => Breaks.Count == 0;
}

/// <summary>
/// (O-19، CEEF-212) التحقق من سلامة سلسلة سجل التدقيق.
///
/// معترض EF يمنع التعديل والحذف داخل التطبيق، لكن الوصول المباشر للقاعدة يتجاوزه.
/// هذا المدقّق يمشي على السلسلة ويكشف ثلاثة أنواع من العبث: تعديل محتوى، إبدال/إقحام
/// صفّ، وحذف صفّ. لا يمنع العبث — يجعله **غير قابل للإخفاء**.
/// </summary>
public sealed class AuditChainVerifier(BrqoshDbContext db)
{
    public async Task<AuditChainResult> VerifyAsync(long companyId, CancellationToken ct = default)
    {
        var entries = await db.AuditLogEntries.AsNoTracking()
            .Where(a => a.CompanyId == companyId)
            .OrderBy(a => a.Sequence)
            .ToListAsync(ct);

        var breaks = new List<AuditChainBreak>();
        var expectedSequence = 1L;
        var expectedPrevious = AuditLogEntry.GenesisHash;

        // (CEEF-229) ‏حدُّ الحقبة: أوّلُ صفٍّ مربوطٍ كُتب لهذه المنشأة.
        // ‏كلُّ صفٍّ غيرِ مربوطٍ **مفتاحُه الأوّليّ أعلى منه** كُتب بعد بدء السلسلة —
        // ‏ولا سبيلَ إلى ذلك من الشفرة: `AuditService` هو الكاتبُ الوحيد وهو يربط دائماً.
        var firstChainedId = entries
            .Where(e => !string.IsNullOrEmpty(e.EntryHash))
            .Select(e => (long?)e.Id)
            .Min();

        foreach (var entry in entries)
        {
            // ‏(CEEF-229) صفوفُ ما قبل السلسلة: تجزئتُها فارغة — لا سلسلةَ لها تُفحص.
            //
            // ‏وكان الشرطُ هنا `Sequence == 0` وحدَه، **بإعفاءٍ بلا تاريخِ انقضاء**: فصفٌّ
            // ‏يُدسّ اليوم بترتيبٍ صفر يمرّ على كاشف العبث إلى الأبد. كشفته طفرةٌ **لم
            // ‏تُلدَغ** في م2-١١ — والحارسُ الذي لا تُلدَغ طفرتُه حارسٌ أعمى.
            //
            // ‏فصار الإعفاءُ مشروطاً بشرطَين يستحيل على المهاجم استيفاؤهما معاً:
            // ‏ترتيبٌ **سالب** (حقبةُ ما قبل السلسلة، يضعه الترقيمُ عند الترقية)،
            // ‏ومفتاحٌ أوّليّ **أقدمُ من أوّل صفٍّ مربوط**.
            if (string.IsNullOrEmpty(entry.EntryHash))
            {
                var isGenuinelyOlder = entry.Sequence < 0
                    && (firstChainedId is null || entry.Id < firstChainedId);

                if (!isGenuinelyOlder)
                    breaks.Add(new AuditChainBreak(
                        AuditChainBreakKind.UnchainedEntry, entry.Sequence, entry.Id,
                        "صفٌّ بلا ارتباطٍ ظهر بعد بدء السلسلة — أُدخل من خارج التطبيق."));

                continue;
            }

            if (entry.Sequence != expectedSequence)
            {
                breaks.Add(new AuditChainBreak(
                    AuditChainBreakKind.MissingEntry, entry.Sequence, entry.Id,
                    $"فجوة في الترتيب: متوقَّع {expectedSequence} ووُجد {entry.Sequence} — يُرجَّح حذف إدخال."));
                expectedSequence = entry.Sequence;   // تابع الفحص من الموجود
            }

            if (!string.Equals(entry.PreviousHash, expectedPrevious, StringComparison.OrdinalIgnoreCase))
            {
                breaks.Add(new AuditChainBreak(
                    AuditChainBreakKind.BrokenLink, entry.Sequence, entry.Id,
                    "تجزئة السابق لا تطابق الإدخال الذي قبله — يُرجَّح إبدال أو إقحام صفّ."));
            }

            if (!entry.ContentMatchesHash())
            {
                breaks.Add(new AuditChainBreak(
                    AuditChainBreakKind.TamperedContent, entry.Sequence, entry.Id,
                    "محتوى الإدخال لا يطابق تجزئته — عُدِّل حقل بعد الحفظ."));
            }

            expectedPrevious = entry.EntryHash;
            expectedSequence = entry.Sequence + 1;
        }

        return new AuditChainResult(companyId, entries.Count, breaks);
    }
}
