using Brqosh.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// توليد الرقم التسلسلي للقيد: JV-{السنة}-{تسلسل من 6 خانات} — تسلسل مستقل لكل شركة وسنة.
/// يستخدم قفل PostgreSQL transaction عند التوفر لتجنب التكرار (ACC-006).
/// </summary>
public class JournalEntryNumberGenerator(BrqoshDbContext db)
{
    public async Task<string> NextAsync(long companyId, DateOnly entryDate, CancellationToken ct = default)
    {
        // ‏الترقيمُ بالسنة المالية (FY بسنة بدايتها) — يبدأ من 1 في كل سنةٍ مالية؛ والافتراضيُّ يناير فلا تغيّر.
        var year = await Accounting.FiscalYearResolver.StartYearForAsync(db, companyId, entryDate, ct);
        await AcquireSequenceLockAsync(companyId, year, ct);

        var prefix = $"JV-{year}-";

        // **الأكبرُ تأخذه القاعدة — لا يُسحب الدفترُ كلُّه إلى الذاكرة.**
        //
        // كان هنا `ToListAsync()` ثمّ فرزٌ في الذاكرة: لإعطاء الرقم رقم ن تُنقل ن-١ سلسلة
        // من القاعدة إلى الذاكرة. وهذا مُثبَتٌ باعتراض الأمر الفعليّ: استعلامٌ بلا حدّ صفوف
        // (NumberAllocationScalesTests).
        //
        // **وحدُّ ما أُثبت:** الآليّة مؤكَّدة، أمّا الأثرُ الزمنيّ فلم يُقَس. وما دفعني إلى
        // هنا قراءةُ بوّابتَي الأداء (4.33ms مقابل 21.40ms) وفهمتُها نمواً تربيعياً — وكانت
        // خطأً: الاختباران في عمليةٍ واحدة، والمشدَّدُ سبق فامتصّ ثمنَ الإحماء. وقياسٌ يُهمل
        // الإحماء أعطى على ثلاث جولات 1.02× و1.91× و1.06× — ضجيجٌ يفوق الأثر، فحُذف.
        //
        // ويبقى الإصلاحُ صحيحاً بذاته: لا يُطلب من القاعدة ألفُ صفٍّ لأخذ واحد. والأثرُ
        // متوقَّعٌ على دفترٍ أكبر وعلى PostgreSQL عبر الشبكة حيث نقلُ الصفوف ثمنُه حقيقيّ.
        //
        // و`OrderByDescending` هنا يُترجَم إلى `ORDER BY … DESC LIMIT 1` فتأخذه القاعدة.
        // والترتيبُ النصّيّ يساوي العدديّ ما دام التسلسل مصفوفاً بستّ خانات — وهو ما كان
        // الفرزُ في الذاكرة يفترضه أصلاً، فلا تتغيّر الدلالة بل موضعُ التنفيذ.
        var fromDb = await db.JournalEntries
            .Where(e => e.CompanyId == companyId && e.EntryNumber != null && e.EntryNumber.StartsWith(prefix))
            .Select(e => e.EntryNumber)
            .OrderByDescending(n => n)
            .FirstOrDefaultAsync(ct);

        // ‏والمعلَّقُ في الذاكرة يبقى في الذاكرة — وهو قليلٌ بحكم كونه غيرَ محفوظٍ بعد.
        var pending = db.JournalEntries.Local
            .Where(e => e.CompanyId == companyId && e.EntryNumber != null && e.EntryNumber.StartsWith(prefix))
            .Select(e => e.EntryNumber!)
            .OrderByDescending(n => n)
            .FirstOrDefault();

        var lastNumber = new[] { fromDb, pending }
            .Where(n => n is not null)
            .OrderByDescending(n => n)
            .FirstOrDefault();

        var next = 1;
        if (lastNumber is not null && int.TryParse(lastNumber[prefix.Length..], out var lastSeq))
            next = lastSeq + 1;

        // ‏حدُّ الصفوف الستّ: بعد 999,999 يصير الرقمُ سبعَ خاناتٍ فيسبقه لفظياً ذو الستّ،
        // ‏فيعود التسلسلُ إلى الوراء ويتكرّر. والتوقّفُ الصريح أفضل من ترقيمٍ يلتفّ بصمت.
        if (next > 999_999)
            throw new AccountingException(
                $"تجاوز تسلسلُ قيود سنة {year} ستّ خانات — لا يمكن توليد رقمٍ جديد بأمان.");

        return $"{prefix}{next:D6}";
    }

    private async Task AcquireSequenceLockAsync(long companyId, int year, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql())
            return;

        // قفل على مستوى المعاملة — يمنع race عند ترحيل متزامن.
        //
        // حرج: يجب تمرير المعاملات كمصفوفة صريحة. التحميل الزائد
        // ExecuteSqlRawAsync(string, params object[]) كان يبتلع الـ CancellationToken
        // كمعامل SQL ثالث، فيرمي EF: "no store type mapping for CancellationToken" —
        // أي أن **كل ترحيل محاسبي على PostgreSQL كان يفشل**. لم تكشفه الاختبارات لأنها
        // تعمل على SQLite حيث يعود هذا التابع مبكراً (السطر أعلاه).
        // التوقيع الموجود في PostgreSQL هو pg_advisory_xact_lock(int4, int4) —
        // تمرير companyId كـ bigint كان يرمي 42883: function does not exist.
        // أي أن حماية ACC-006 لم تكن تعمل على PostgreSQL إطلاقاً. الصبّ صريح في SQL.
        await db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}::int, {1}::int)",
            new object[] { companyId, year },
            ct);
    }
}
