using Brqosh.Api.Idempotency;

namespace Brqosh.Api.Tests;

/// <summary>
/// ع-A4 (CEEF-210) — عزل كاش الـIdempotency.
///
/// كان المفتاح نصّ الترويسة وحده: مستأجر يستخدم نفس القيمة التي استخدمها آخر يستلم
/// **جسد استجابته المخزَّن كاملاً** — تسريب بيانات عابر للمستأجرين. ويتخطى الطلبُ
/// المكرر حارس المستأجر لأنه يُردّ قبل بلوغ النقطة.
/// </summary>
public class IdempotencyIsolationTests
{
    public IdempotencyIsolationTests() => IdempotencyStore.ResetForTests();

    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static IdempotentResponse Body(string text) => new(200, "application/json", text);

    /// <summary>نفس المفتاح من مستأجرين مختلفين لا يتقاطع.</summary>
    [Fact]
    public void SameHeaderKey_DifferentTenants_DoNotShareCache()
    {
        var keyA = IdempotencyStore.ComposeKey(TenantA, null, null, "/api/v1/exchange-rates", "same-key");
        var keyB = IdempotencyStore.ComposeKey(TenantB, null, null, "/api/v1/exchange-rates", "same-key");

        IdempotencyStore.Save(keyA, Body("سرّ المستأجر أ"));

        Assert.False(IdempotencyStore.TryGet(keyB, out _));
        Assert.True(IdempotencyStore.TryGet(keyA, out var mine));
        Assert.Equal("سرّ المستأجر أ", mine.Body);
    }

    /// <summary>ونفس المفتاح على مسارين مختلفين لا يتقاطع كذلك.</summary>
    [Fact]
    public void SameHeaderKey_DifferentPaths_DoNotShareCache()
    {
        var rates = IdempotencyStore.ComposeKey(TenantA, null, null, "/api/v1/exchange-rates", "k");
        var maps = IdempotencyStore.ComposeKey(TenantA, null, null, "/api/v1/intercompany-account-maps", "k");

        IdempotencyStore.Save(rates, Body("أسعار"));

        Assert.False(IdempotencyStore.TryGet(maps, out _));
    }

    /// <summary>إعادة الطلب نفسه من نفس المستأجر تستفيد من الكاش — الوظيفة لم تُكسر.</summary>
    [Fact]
    public void SameTenantSamePath_ReusesCachedResponse()
    {
        var key = IdempotencyStore.ComposeKey(TenantA, null, null, "/api/v1/exchange-rates", "retry-1");
        IdempotencyStore.Save(key, Body("مخزَّن"));

        Assert.True(IdempotencyStore.TryGet(key, out var cached));
        Assert.Equal("مخزَّن", cached.Body);
    }

    /// <summary>غياب هوية المستأجر لا يخلط الطلبات بمستأجر معروف.</summary>
    [Fact]
    public void NullTenant_DoesNotCollideWithKnownTenant()
    {
        var anonymous = IdempotencyStore.ComposeKey(null, null, null, "/api/v1/exchange-rates", "k");
        var known = IdempotencyStore.ComposeKey(TenantA, null, null, "/api/v1/exchange-rates", "k");

        IdempotencyStore.Save(anonymous, Body("مجهول"));

        Assert.False(IdempotencyStore.TryGet(known, out _));
    }
}
