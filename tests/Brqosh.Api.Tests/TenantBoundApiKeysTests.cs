using System.Net;
using Brqosh.Api.Middleware;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Brqosh.Api.Tests;

/// <summary>
/// ‏(تقويةُ الـAPI · S-4) <b>المستأجرُ يُؤخذ من المفتاح لا من ترويسةٍ يكتبها المستدعي.</b>
///
/// <para>‏<c>X-Tenant-Id</c> كان مصدرَ المستأجر الوحيد: مفتاحٌ مشتركٌ واحد، ومن يحمله يختار أيَّ مستأجرٍ بكتابة رقمه —
/// <b>فالعزلُ المفروضُ (<c>EnforceTenantIsolation</c>) يفرض وجودَ الترويسة لا صدقَها.</b> فصار لكلّ مستأجرٍ مفتاحٌ اختياريٌّ
/// مربوطٌ به (<c>BRQOSH_TENANT_API_KEYS</c>): المفتاحُ يُعيّن المستأجرَ، وترويسةٌ تخالفه تُرفض بـ403. والمفتاحُ المشتركُ
/// يبقى كما كان — ويُحذَّر منه عند الإقلاع حين يكون العزلُ مفروضاً.</para>
/// </summary>
public class TenantBoundApiKeysTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private const string KeyA = "tenant-a-key-0123456789abcdef";
    private const string KeyB = "tenant-b-key-0123456789abcdef";
    private const string SharedKey = "shared-key-0123456789abcdef";

    // ── القرارُ بلا خادم ─────────────────────────────────────────────────────

    [Fact]
    public void TheSetting_MapsEachKeyToItsTenant()
    {
        var keys = TenantApiKeys.Parse($"{TenantA}:{KeyA}; {TenantB}:{KeyB}");

        Assert.Equal(TenantA, keys.TenantFor(KeyA));
        Assert.Equal(TenantB, keys.TenantFor(KeyB));
        Assert.Null(keys.TenantFor("not-a-key"));
        Assert.Null(keys.TenantFor(null));
        Assert.True(TenantApiKeys.Parse(null).IsEmpty);
        Assert.True(TenantApiKeys.Parse("  ").IsEmpty);
    }

    /// <summary>‏<b>وإعدادٌ معيبٌ يُسقط الإقلاع</b> — لا يُتجاهل صامتاً فيعمل الخادمُ بمفاتيحَ أقلَّ ممّا ظنّ المُشغِّل.</summary>
    [Theory]
    [InlineData("not-a-guid:tenant-a-key-0123456789abcdef")]
    [InlineData("aaaaaaaa-0000-0000-0000-000000000001")]
    [InlineData("aaaaaaaa-0000-0000-0000-000000000001:")]
    [InlineData("aaaaaaaa-0000-0000-0000-000000000001:short")]
    [InlineData("aaaaaaaa-0000-0000-0000-000000000001:tenant-a-key-0123456789abcdef;aaaaaaaa-0000-0000-0000-000000000001:tenant-b-key-0123456789abcdef")]
    [InlineData("aaaaaaaa-0000-0000-0000-000000000001:same-key-0123456789abcdef;bbbbbbbb-0000-0000-0000-000000000002:same-key-0123456789abcdef")]
    public void AMalformedSetting_FailsTheStart(string setting) =>
        Assert.Throws<InvalidOperationException>(() => TenantApiKeys.Parse(setting));

    /// <summary>‏<b>ومفتاحٌ مربوطٌ يساوي المشتركَ ملتبس</b> — أيُّهما يحكم؟ — فيُرفض عند الإقلاع.</summary>
    [Fact]
    public void ATenantKeyEqualToTheSharedKey_FailsTheStart() =>
        Assert.Throws<InvalidOperationException>(() =>
            TenantApiKeys.Parse($"{TenantA}:{SharedKey}").EnsureDistinctFrom(SharedKey));

    /// <summary>‏<b>والتحذيرُ</b>: عزلٌ مفروضٌ ولا مفتاحَ مربوط ⟵ المستأجرُ من الترويسة وحدَها.</summary>
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void AStartupWarning_NamesAHeaderOnlyTenant(bool enforce, bool hasTenantKeys, bool warns) =>
        Assert.Equal(warns, TenantApiKeys.HeaderOnlyWarning(enforce, hasTenantKeys) is not null);

    /// <summary>‏<b>وحدُّ المعدّل يعدّ المفتاحَ المربوطَ مفتاحاً</b> — له دلوُه، لا دلوُ العنوان المشترك مع غيره.</summary>
    [Fact]
    public void ATenantKey_GetsItsOwnRateLimitBucket()
    {
        var keys = TenantApiKeys.Parse($"{TenantA}:{KeyA}");
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Api-Key"] = KeyA;
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9");

        Assert.StartsWith("key:", ApiRateLimitMiddleware.ResolveBucketKey(ctx, SharedKey, keys), StringComparison.Ordinal);
        Assert.StartsWith("ip:", ApiRateLimitMiddleware.ResolveBucketKey(ctx, SharedKey), StringComparison.Ordinal);
    }

    // ── طرفاً إلى طرف ───────────────────────────────────────────────────────

    /// <summary>‏<b>القياسُ الحاكم:</b> مفتاحُ A وترويسةُ B ⟵ 403 — لا يدخل مستأجرَ غيره بكتابة رقمه.</summary>
    [Fact]
    public async Task ATenantKey_WithAnotherTenantsHeader_IsForbidden()
    {
        var response = await SendAsync(KeyA, TenantB);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("tenant_mismatch", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>‏<b>والمفتاحُ وحدَه يُعيّن المستأجر</b> — بلا ترويسةٍ يمضي حيث كان العزلُ يرفض الغياب.</summary>
    [Fact]
    public async Task ATenantKey_WithoutAHeader_IsItsOwnTenant()
    {
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(KeyA, tenantHeader: null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(KeyA, TenantA)).StatusCode);
    }

    /// <summary>‏<b>والمفتاحُ المشتركُ كما كان</b> (شاهدُ ضبط)، <b>ومفتاحٌ مجهولٌ 401</b>.</summary>
    [Fact]
    public async Task TheSharedKey_KeepsItsBehaviour_AndAnUnknownKeyIsUnauthorized()
    {
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(SharedKey, TenantB)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(SharedKey, tenantHeader: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync("unknown-key-0123456789abcdef", TenantA)).StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(string key, Guid? tenantHeader)
    {
        using var factory = new IsolatedApiFactory(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ApiSecuritySettings.SaaSModeEnv] = "true",
                [ApiSecuritySettings.ApiKeyEnv] = SharedKey,
                [TenantApiKeys.Env] = $"{TenantA}:{KeyA};{TenantB}:{KeyB}",
            })));
        try
        {
            var client = factory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/companies");
            request.Headers.Add("X-Api-Key", key);
            if (tenantHeader is { } tenant)
                request.Headers.Add("X-Tenant-Id", tenant.ToString());
            return await client.SendAsync(request);
        }
        finally
        {
            TenantContext.EnforceTenantIsolation = false;
        }
    }
}
