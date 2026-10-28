using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Security;

/// <summary>
/// <b>قفلُ الدخول صار يعيش في القاعدة — وكان يُمحى بإعادة تشغيل التطبيق.</b>
///
/// <para><b>ما كان:</b> <see cref="LoginLockoutTracker"/> قاموسٌ ثابتٌ <b>في ذاكرة العملية</b>،
/// ويُفرَض في <c>LoginWindow</c> <b>لا في <c>UserAuthService.AuthenticateAsync</c></b>.
/// فمن أغلق التطبيقَ وفتحه بدأ من صفر، <b>ومن استدعى الخدمةَ من أيّ مسارٍ آخر لم يُقفَل
/// أصلاً</b>. وإخفاقاتُ رمز MFA لم تكن تُحسب بحال.</para>
///
/// <para><b>ولا جدولَ جديداً ولا هجرة:</b> كلُّ محاولةٍ تُسجَّل أصلاً في
/// <c>login_audit_entries</c> بنجاحها وسببِ إخفاقها. <b>فالقفلُ يُشتقّ من الأثر نفسِه</b> —
/// عدّادٌ منفصلٌ يفتح بابَ الكذب: صفٌّ يقول «مقفل» وسجلٌّ لا يشهد.</para>
///
/// <para><b>ويُحسب المتتالي لا المجموع:</b> نجاحٌ واحدٌ يمحو ما قبله. وبلا هذا يُقفَل
/// حسابٌ يعمل منذ سنةٍ لأنّ صاحبَه أخطأ خمسَ مرّاتٍ متفرّقاتٍ عبر شهور.</para>
/// </summary>
public sealed class PersistentLoginLockout(BrqoshDbContext db)
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>كم محاولةً يُنظَر فيها إلى الوراء — سقفٌ يمنع مسحَ سجلٍّ طويل.</summary>
    private const int Window = 32;

    public sealed record LockoutState(bool IsLocked, int ConsecutiveFailures, DateTime? UnlocksAtUtc)
    {
        public int MinutesRemaining => UnlocksAtUtc is { } until
            ? Math.Max(1, (int)Math.Ceiling((until - DateTime.UtcNow).TotalMinutes))
            : 0;
    }

    /// <summary>يقرأ الحالة من سجلّ المحاولات — لا يكتب شيئاً.</summary>
    public async Task<LockoutState> ReadAsync(
        long companyId, string username, DateTime? nowUtc = null, CancellationToken ct = default)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var normalized = username.Trim().ToLowerInvariant();

        var recent = await db.LoginAuditEntries
            .Where(e => e.CompanyId == companyId && e.Username == normalized)
            .OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id)
            .Take(Window)
            .Select(e => new { e.Success, e.CreatedAtUtc })
            .ToListAsync(ct);

        var failures = 0;
        DateTime? newestFailure = null;
        foreach (var entry in recent)
        {
            if (entry.Success) break;          // ‏نجاحٌ يمحو ما قبله
            newestFailure ??= entry.CreatedAtUtc;
            failures++;
        }

        if (failures < MaxFailedAttempts || newestFailure is null)
            return new LockoutState(false, failures, null);

        var unlocksAt = newestFailure.Value + LockoutDuration;

        // ‏وانقضاءُ المدّة يفتح الحساب من نفسِه — لا يحتاج تدخّلاً ولا صفّاً جديداً.
        return unlocksAt > now
            ? new LockoutState(true, failures, unlocksAt)
            : new LockoutState(false, failures, null);
    }

    /// <summary>يرمي برسالةٍ تُسمّي المدّةَ الباقية إن كان الحسابُ مقفلاً.</summary>
    public async Task ThrowIfLockedAsync(
        long companyId, string username, DateTime? nowUtc = null, CancellationToken ct = default)
    {
        var state = await ReadAsync(companyId, username, nowUtc, ct);
        if (state.IsLocked)
            throw new AccountingException(
                $"الحساب مقفل مؤقتاً بعد {state.ConsecutiveFailures} محاولاتٍ فاشلة. "
                + $"حاول بعد {state.MinutesRemaining} دقيقة.");
    }
}
