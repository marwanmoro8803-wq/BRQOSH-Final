using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// سياسةُ الحفظ — <b>يوماً لكلّ صنف، و<c>0</c> يعني «لا تُقلَّم»</b>.
/// </summary>
/// <param name="DispatchedEventDays">
/// أحداثُ الطابور <b>المُرسَلة</b>. <b>والمعلَّقةُ لا تُمَسّ أبداً</b> — حذفُها يُفقد
/// حدثَ عملٍ لم يصل إلى أحد.
/// </param>
/// <param name="ReportSnapshotDays">
/// لقطاتُ التقارير — <b>وهي مُعاد توليدُها من الأستاذ</b>، فحذفُها لا يُفقد شيئاً.
/// </param>
/// <param name="LoginAuditDays">
/// سجلُّ محاولات الدخول. <b>والافتراضيُّ خمسُ سنوات</b> — فهو دليلٌ أمنيٌّ تُطالِب به
/// جهاتٌ رقابية، <b>وتقليمُه بشهورٍ يُفقد أثرَ اختراقٍ يُكتشف متأخّراً</b>.
/// </param>
public record RetentionPolicy(
    int DispatchedEventDays = 90,
    int ReportSnapshotDays = 365,
    int LoginAuditDays = 1825);

/// <summary>ما حُذف — <b>صنفاً صنفاً لا مجموعاً</b>.</summary>
public record RetentionResult(
    int DispatchedEvents,
    int ReportSnapshots,
    int LoginAudits)
{
    public int Total => DispatchedEvents + ReportSnapshots + LoginAudits;
}

/// <summary>
/// (CEEF-231 · ط5-١١) <b>تقليمُ ما يجوز تقليمُه — ولا يُمَسّ ما لا يجوز.</b>
///
/// <para><b>ما كان:</b> لا خدمةَ أرشفةٍ ولا تقليمٍ في المستودع كلِّه. <b>والموجودُ نسخٌ
/// احتياطيٌّ وتدويرُ ملفّاته</b> — وهو تدويرُ نسخٍ على القرص لا حفظُ بيانات. فجداولُ
/// الطابور واللقطات وسجلِّ الدخول <b>تنمو بلا حدٍّ ما دام النظامُ يعمل</b>، وقاعدةُ
/// منشأةٍ بعد ثلاث سنواتٍ تحمل ملايينَ صفٍّ لا يقرؤها أحد.</para>
///
/// <para><b>والقرارُ الحاكم — سجلُّ التدقيق لا يُمَسّ.</b> فهو <b>دفترٌ غيرُ قابلٍ
/// للتعديل ولا الحذف</b> يفرضه <c>AuditLogImmutabilityInterceptor</c>، <b>وسلسلةُ
/// تجزئةٍ تربط كلَّ سطرٍ بسابقه</b> منذ <c>O-19</c>. فحذفُ سطرٍ منه <b>يكسر السلسلةَ
/// عند كلّ ما بعده</b> ويُبطل كلَّ ما بُني لإثبات عدم العبث. <b>ولا يُقلَّم بأمرٍ ولا
/// بسياسةٍ ولا بطلب</b> — والحدُّ هذا مكتوبٌ لا مُستنتَج.</para>
///
/// <para><b>والمعلَّقُ في الطابور لا يُمَسّ كذلك</b>: حدثٌ لم يُرسَل بعد <b>عملٌ لم يصل
/// إلى أحد</b> — وحذفُه بحجّة قِدَمه يُفقده إلى الأبد. <b>والقِدَمُ فيه علامةُ عطلٍ لا
/// علامةُ انتهاء</b>: طابورٌ فيه حدثٌ عمرُه سنةٌ يعني أنّ الإرسالَ متوقّفٌ منذ سنة.</para>
///
/// <para><b>وهويّةُ ح-٩:</b> بعد أيّ تقليم، <b>عددُ سطور سجلّ التدقيق كما كان</b>،
/// <b>وعددُ الأحداث المعلَّقة كما كان</b>.</para>
/// </summary>
public class DataRetentionService(BrqoshDbContext db)
{
    /// <summary>
    /// يُقدّر ما سيُحذف — <b>ولا يحذف</b>.
    /// </summary>
    /// <remarks>
    /// <b>وحذفٌ بلا معاينةٍ قرارٌ لا رجعةَ فيه يُتّخذ بنقرة</b>. والمعاينةُ تُقرأ
    /// بالأرقام نفسِها التي يُنتجها التنفيذ، <b>فلا تُقدَّر بمنطقٍ ثانٍ يفترق عنه</b>.
    /// </remarks>
    public Task<RetentionResult> PreviewAsync(long companyId, RetentionPolicy policy,
        DateOnly asOf, CancellationToken ct = default) =>
        RunAsync(companyId, policy, asOf, execute: false, ct);

    /// <summary>يُنفّذ التقليم ويُعيد ما حُذف.</summary>
    public Task<RetentionResult> PruneAsync(long companyId, RetentionPolicy policy,
        DateOnly asOf, CancellationToken ct = default) =>
        RunAsync(companyId, policy, asOf, execute: true, ct);

    private async Task<RetentionResult> RunAsync(long companyId, RetentionPolicy policy,
        DateOnly asOf, bool execute, CancellationToken ct)
    {
        if (policy.DispatchedEventDays < 0 || policy.ReportSnapshotDays < 0 || policy.LoginAuditDays < 0)
            throw new AccountingException("مدّةُ الحفظ لا تكون سالبة.");

        var events = await PruneDispatchedEventsAsync(companyId, policy, asOf, execute, ct);
        var snapshots = await PruneSnapshotsAsync(companyId, policy, asOf, execute, ct);
        var logins = await PruneLoginAuditsAsync(policy, asOf, execute, ct);

        if (execute) await db.SaveChangesAsync(ct);

        return new RetentionResult(events, snapshots, logins);
    }

    private async Task<int> PruneDispatchedEventsAsync(long companyId, RetentionPolicy policy,
        DateOnly asOf, bool execute, CancellationToken ct)
    {
        if (policy.DispatchedEventDays == 0) return 0;

        var cutoff = asOf.AddDays(-policy.DispatchedEventDays).ToDateTime(TimeOnly.MinValue);

        // ‏**والمُرسَلُ وحدَه** — والمعلَّقُ عملٌ لم يصل إلى أحد، **وقِدَمُه علامةُ عطلٍ
        // ‏لا علامةُ انتهاء**.
        var stale = await db.StoredDomainEvents
            .Where(e => e.CompanyId == companyId
                        && e.DispatchedAtUtc != null
                        && e.OccurredAtUtc < cutoff)
            .ToListAsync(ct);

        if (execute && stale.Count > 0)
            db.StoredDomainEvents.RemoveRange(stale);

        return stale.Count;
    }

    private async Task<int> PruneSnapshotsAsync(long companyId, RetentionPolicy policy,
        DateOnly asOf, bool execute, CancellationToken ct)
    {
        if (policy.ReportSnapshotDays == 0) return 0;

        var cutoff = asOf.AddDays(-policy.ReportSnapshotDays).ToDateTime(TimeOnly.MinValue);

        var stale = await db.ReportSnapshots
            .Where(s => s.CompanyId == companyId && s.CapturedAtUtc < cutoff)
            .ToListAsync(ct);

        if (execute && stale.Count > 0)
            db.ReportSnapshots.RemoveRange(stale);

        return stale.Count;
    }

    /// <summary>
    /// يُقلّم سجلَّ محاولات الدخول — <b>وهو على مستوى النظام لا المنشأة</b>.
    /// </summary>
    /// <remarks>
    /// <b>ومحاولةُ دخولٍ فاشلةٌ لا تخصّ منشأةً بعينها</b>: المهاجمُ لا يُعلن أيَّ منشأةٍ
    /// يقصد. فتقليمُه بمعرّف منشأةٍ يترك أخطرَ السطور — <b>محاولاتِ مَن لا حساب له</b> —
    /// بلا تقليمٍ إلى الأبد.
    /// </remarks>
    private async Task<int> PruneLoginAuditsAsync(RetentionPolicy policy, DateOnly asOf,
        bool execute, CancellationToken ct)
    {
        if (policy.LoginAuditDays == 0) return 0;

        var cutoff = asOf.AddDays(-policy.LoginAuditDays).ToDateTime(TimeOnly.MinValue);

        var stale = await db.LoginAuditEntries
            .Where(l => l.CreatedAtUtc < cutoff)
            .ToListAsync(ct);

        if (execute && stale.Count > 0)
            db.LoginAuditEntries.RemoveRange(stale);

        return stale.Count;
    }
}
