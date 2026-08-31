using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Security;

public class AuditService(BrqoshDbContext db)
{
    public async Task LogAsync(long companyId, string username, string action,
        string entityType, string entityKey, string details = "", string? reason = null,
        CancellationToken ct = default)
    {
        var entry = AuditLogEntry.Record(
            companyId, username, action, entityType, entityKey, details, reason);

        // (O-19، CEEF-212) اربط الإدخال بسلسلة الشركة قبل الحفظ.
        //
        // (CEEF-229) ‏**والمعاملةُ شرطُ حياة القفل، لا تحسيناً.**
        //
        // ‏القفلُ أدناه من نوع `xact`: خارج معاملةٍ صريحة يُحرَّر فورَ انتهاء أمره، فلا
        // ‏يحمي القراءةَ التي تليه. فكاتبان متزامنان يقرآن «الأخير» نفسَه ويبنيان عليه
        // ‏الترتيبَ نفسَه — <b>ويُسقط الفهرسُ الفريد أحدَهما بـ23505</b>: حدثٌ محاسبيّ وقع
        // ‏ولا أثرَ له، أو معاملةُ ترحيلٍ كاملة تُرتَدّ على خطأِ قاعدةٍ خام.
        //
        // ‏**ونادرٌ لا يعني ممنوعاً**: أربعةٌ وعشرون كاتباً متزامناً مرّوا ثلاثَ مرّات بلا
        // ‏تصادم لأنّ النافذة ميكروثوانٍ؛ فلمّا وُسّعت 120ms سقط واحدٌ منهم. وتلك النافذةُ
        // ‏تتّسع من نفسها على قاعدةٍ بعيدةٍ أو محمَّلة.
        //
        // ‏و`ExecuteInTransactionAsync` تنضمّ إلى معاملةٍ قائمة إن وُجدت وتفتح واحدةً إن لم
        // ‏تُوجد — فيحيا القفلُ في الحالين. **ولم يُفرَض المنعُ كما في `DocumentSequenceLock`**
        // ‏لأنّ ستّين مستدعياً هنا، ومنهم من يُنادي من شاشةٍ بلا معاملةٍ بحقّ.
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            await AcquireChainLockAsync(companyId, token);

            var last = await db.AuditLogEntries
                .Where(a => a.CompanyId == companyId)
                .OrderByDescending(a => a.Sequence)
                .Select(a => new { a.Sequence, a.EntryHash })
                .FirstOrDefaultAsync(token);

            // (CEEF-229) ‏ترتيبُ ما قبل السلسلة سالب، و«الأعلى + ١» عليه يُنتج صفراً —
            // ‏ترتيباً محجوزاً لغير المربوط. فالسلسلةُ تبدأ من ١ مهما كان ما قبلها.
            entry.ChainTo(
                Math.Max(last?.Sequence ?? 0, 0) + 1,
                last?.EntryHash ?? AuditLogEntry.GenesisHash);

            db.AuditLogEntries.Add(entry);
            await db.SaveChangesAsync(token);
        }, ct);
    }

    private async Task AcquireChainLockAsync(long companyId, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql())
            return;

        // (CEEF-229) ‏الشرطُ مُعلَنٌ هنا لا مفترَضاً في المستدعي.
        //
        // ‏`pg_advisory_xact_lock` يُحرَّر بانتهاء المعاملة؛ وخارجَها يُحرَّر بانتهاء
        // ‏أمره — فيبقى الاسمُ «قفلاً» والفعلُ لا شيء. ولو نُزع الغلافُ المعامليّ من
        // ‏`LogAsync` يوماً لعاد السباقُ **صامتاً**، فيُصرَّح به هنا ليسقط صاخباً.
        //
        // ‏وهو نظيرُ ما فُرض في `DocumentSequenceLock` بـم1-١٤، وثالثُ موضعٍ في هذه
        // ‏الحملة يقع فيه العطلُ نفسُه — <b>قفلٌ حيٌّ هنا ميّتٌ هناك</b>.
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "قفلُ سلسلة التدقيق يجري خارج معاملة — وهو من نوع xact فيُحرَّر فوراً "
                + "فلا يحمي قراءةَ آخر إدخالٍ ولا كتابةَ ترتيبه. "
                + "استُدعي AuditService.LogAsync بلا غلافٍ معامليّ.");

        // مفتاح ثانٍ ثابت يميّز قفل سلسلة التدقيق عن قفل ترقيم القيود لنفس الشركة.
        // التوقيع (int4, int4) وتمرير المعاملات كمصفوفة صريحة — درسا CEEF-203.
        await db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}::int, {1}::int)",
            new object[] { companyId, 991 },
            ct);
    }
}
