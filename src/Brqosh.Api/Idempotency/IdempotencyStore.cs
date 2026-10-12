using System.Collections.Concurrent;
using Brqosh.Domain.Security;

namespace Brqosh.Api.Idempotency;

public sealed record IdempotentResponse(int StatusCode, string ContentType, string Body);

/// <summary>
/// ذاكرة مؤقتة لمفاتيح Idempotency-Key — M9 أدنى · Redis لاحقاً في SaaS.
///
/// (CEEF-210) المفتاح كان نصّ الترويسة وحده: مستأجر يستخدم نفس القيمة التي استخدمها
/// آخر يستلم **جسد استجابته المخزَّن كاملاً** — تسريب بيانات عابر للمستأجرين، ويتخطى
/// الطلبُ المكرر حارس المستأجر لأنه يُردّ قبل بلوغ النقطة. صار المفتاح مركّباً من
/// (المستأجر + المسار + الترويسة). وأُضيف سقف وانتهاء صلاحية: الكاش كان ينمو بلا حدّ
/// فيستنزف الذاكرة من عميل شرعيّ واحد يولّد مفاتيح فريدة.
/// </summary>
public static class IdempotencyStore
{
    /// <summary>مدة بقاء المفتاح — تكفي لإعادة محاولة عميل، ولا تُبقي الأجساد للأبد.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    /// <summary>سقف المداخل — يمنع استنزاف الذاكرة عبر مفاتيح فريدة متتابعة.</summary>
    public const int MaxEntries = 5_000;

    private sealed record Entry(IdempotentResponse Response, DateTime ExpiresAtUtc);

    private static readonly ConcurrentDictionary<string, Entry> Cache = new();

    /// <summary>
    /// مفتاح مركّب: لا يتقاطع مستأجران ولا مساران على نفس القيمة.
    ///
    /// <para>‏(تقويةُ الـAPI · S-6) <b>ولا مستدعيان</b>: الدورُ والاسمُ جزءٌ منه. كان مستدعٍ آخرُ في المستأجر نفسِه — اسمٌ آخر
    /// أو دورٌ ضُيّق بـ<c>X-Api-Role</c> — يُرسل القيمةَ نفسَها فيستلم جسدَ ردّ الأوّل <b>قبل أن يبلغ فحصَ صلاحيّته</b>.
    /// <b>وكلُّ جزءٍ مسبوقٌ بطوله</b>: فاصلٌ داخل اسمٍ لا يصوغ مفتاحَ مستدعٍ آخر.</para>
    /// </summary>
    public static string ComposeKey(Guid? tenantId, UserRole? role, string? username, string path, string headerKey) =>
        string.Join("|", new[] { tenantId?.ToString() ?? "-", role?.ToString() ?? "-", username ?? "-", path, headerKey }
            .Select(part => $"{part.Length}:{part}"));

    public static bool TryGet(string key, out IdempotentResponse response)
    {
        response = null!;
        if (!Cache.TryGetValue(key, out var entry))
            return false;

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            Cache.TryRemove(key, out _);
            return false;
        }

        response = entry.Response;
        return true;
    }

    public static void Save(string key, IdempotentResponse response)
    {
        EvictIfNeeded();
        Cache[key] = new Entry(response, DateTime.UtcNow.Add(Ttl));
    }

    private static void EvictIfNeeded()
    {
        if (Cache.Count < MaxEntries)
            return;

        var now = DateTime.UtcNow;
        foreach (var (key, entry) in Cache)
        {
            if (entry.ExpiresAtUtc <= now)
                Cache.TryRemove(key, out _);
        }

        // ما زال ممتلئاً بعد إزالة المنتهية: أزل الأقدم انتهاءً لإبقاء السقف فعّالاً.
        if (Cache.Count >= MaxEntries)
        {
            foreach (var key in Cache.OrderBy(p => p.Value.ExpiresAtUtc)
                         .Take(Cache.Count - MaxEntries + 1)
                         .Select(p => p.Key)
                         .ToList())
            {
                Cache.TryRemove(key, out _);
            }
        }
    }

    public static void ResetForTests() => Cache.Clear();
}
