using System.Net;
using System.Net.Http.Json;
using Brqosh.Api.Idempotency;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Webhooks;

namespace Brqosh.Api.Tests;

/// <summary>
/// ‏(تقويةُ الـAPI · S-6) <b>ردُّ مفتاح التكرار المخزَّن يُعطى لصاحبه وحدَه.</b>
///
/// <para>‏كان مفتاحُ الذاكرة (المستأجر + المسار + الترويسة). فمستدعٍ آخرُ في المستأجر نفسِه — اسمٌ آخر، أو دورٌ ضُيّق
/// بـ<c>X-Api-Role</c> — يُرسل القيمةَ نفسَها <b>فيستلم جسدَ ردّ الأوّل كاملاً قبل أن يبلغ الطلبُ فحصَ صلاحيّته</b>:
/// إعادةُ التشغيل تقع قبل نقطة النهاية، فالدورُ المضيَّقُ يقرأ ما لا يحقّ له، والسجلُّ لا يرى الثاني أصلاً.</para>
/// </summary>
public class IdempotencyIsTheCallersOwnTests : IClassFixture<ApiTestFixture>
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly HttpClient _client;

    public IdempotencyIsTheCallersOwnTests(ApiTestFixture fixture) =>
        _client = fixture.Factory.CreateClient();

    /// <summary>‏<b>القياسُ الحاكم، طرفاً إلى طرف:</b> مستدعٍ آخرُ بالمفتاح نفسِه يُنفَّذ طلبُه — لا يُردّ عليه بنسخة الأوّل.</summary>
    [Fact]
    public async Task AnotherCaller_WithTheSameKey_IsServedItsOwnRequest()
    {
        IdempotencyStore.ResetForTests();
        WebhookSubscriptionRegistry.ResetForTests();

        var first = await _client.SendAsync(Subscribe("alice", "https://example.com/hooks/alice"));
        var second = await _client.SendAsync(Subscribe("bob", "https://example.com/hooks/bob"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.NotEqual(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Equal(2, WebhookSubscriptionRegistry.ListAll().Count);
    }

    /// <summary>‏<b>وصاحبُ المفتاح نفسُه يُعاد عليه ردُّه</b> (شاهدُ ضبط) — فالعزلُ لم يُبطل التكرارَ الآمن.</summary>
    [Fact]
    public async Task TheSameCaller_StillGetsItsCachedResponse()
    {
        IdempotencyStore.ResetForTests();
        WebhookSubscriptionRegistry.ResetForTests();

        var first = await _client.SendAsync(Subscribe("alice", "https://example.com/hooks/one"));
        var retry = await _client.SendAsync(Subscribe("alice", "https://example.com/hooks/two"));

        Assert.Equal(await first.Content.ReadAsStringAsync(), await retry.Content.ReadAsStringAsync());
        Assert.Single(WebhookSubscriptionRegistry.ListAll());
    }

    /// <summary>‏<b>والدورُ جزءٌ من المفتاح</b>: الاسمُ نفسُه بدورٍ مضيَّقٍ لا يقرأ ما خُزّن لدورٍ أعلى.</summary>
    [Fact]
    public void TheRole_AndTheUser_ArePartOfTheKey()
    {
        const string path = "/api/v1/exchange-rates";
        var admin = IdempotencyStore.ComposeKey(Tenant, UserRole.Administrator, "api:alice", path, "k");

        Assert.NotEqual(admin, IdempotencyStore.ComposeKey(Tenant, UserRole.Viewer, "api:alice", path, "k"));
        Assert.NotEqual(admin, IdempotencyStore.ComposeKey(Tenant, UserRole.Administrator, "api:bob", path, "k"));
        Assert.NotEqual(admin, IdempotencyStore.ComposeKey(Tenant, null, null, path, "k"));
        Assert.Equal(admin, IdempotencyStore.ComposeKey(Tenant, UserRole.Administrator, "api:alice", path, "k"));
    }

    /// <summary>‏<b>ولا يُصاغ مفتاحان متطابقان من أجزاءٍ مختلفة</b> — اسمٌ فيه الفاصلُ لا يتنكّر بمسارٍ آخر.</summary>
    [Fact]
    public void ASeparatorInsideAPart_CannotForgeAnotherCallersKey()
    {
        var forged = IdempotencyStore.ComposeKey(Tenant, UserRole.Administrator, "api:a|/api/v1/x", "/y", "k");
        var honest = IdempotencyStore.ComposeKey(Tenant, UserRole.Administrator, "api:a", "/api/v1/x|/y", "k");

        Assert.NotEqual(forged, honest);
    }

    private static HttpRequestMessage Subscribe(string user, string target)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/subscriptions")
        {
            Content = JsonContent.Create(new { EventType = "DocumentPosted", TargetUrl = target }),
        };
        request.Headers.Add("Idempotency-Key", "shared-key-001");
        request.Headers.Add("X-Api-User", user);
        return request;
    }
}
