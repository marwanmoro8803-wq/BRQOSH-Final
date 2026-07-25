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

        foreach (var entry in entries)
        {
            // إدخالات ما قبل تفعيل السلسلة (Sequence=0) لا تُفحص — لا سلسلة لها أصلاً.
            if (entry.Sequence == 0)
                continue;

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
