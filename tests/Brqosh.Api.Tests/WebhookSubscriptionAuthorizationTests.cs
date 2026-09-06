using System.Net;
using System.Net.Http.Json;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Webhooks;

namespace Brqosh.Api.Tests;

/// <summary>
/// (م-٣ · ن-٢٧) <b>مسارا اشتراكات الـwebhook كانا بلا حارسِ صلاحيّةٍ ولا مستأجر.</b>
///
/// <para><c>MapGet</c> كان <c>() =&gt; Results.Ok(WebhookSubscriptionRegistry.List())</c> — لا وسيطَ
/// ولا سياق، فيُعيد <b>قائمةَ العمليّة كلَّها</b> لأيّ حاملِ مفتاح: <b>عناوينَ وجهةٍ سجّلها
/// مستأجرون آخرون</b>، وهي عناوينُ أنظمتِهم الداخليّة. و<c>MapPost</c> معاملاه
/// <c>(WebhookSubscribeRequest, IWebHostEnvironment)</c> — فيُزرع هدفُ تسليمٍ خارجيٌّ بلا صلاحيّة.</para>
///
/// <para><b>وصياغةُ البند صُحّحت قبل الإصلاح.</b> «حاملُ مفتاحٍ بدور <c>Viewer</c> يشترك» ليس
/// تصعيداً: <c>NarrowByRequest</c> تُضيّق ولا تُوسّع، فمُرسِلُ الترويسة يملك المفتاحَ الأقوى
/// ويُقيّد نفسَه <b>طوعاً</b>. <b>والعطلُ الدائمُ عكسُه</b>: مضيفٌ يُضبَط
/// <c>BRQOSH_API_ROLE=Viewer</c> — أو يُترك في الإنتاج فيسقط إلى <c>Viewer</c> انغلاقاً —
/// <b>يزرع الهدفَ رغم ذلك</b>، لأنّ المسارين لا يسألان <c>SecurityContext</c> أصلاً.</para>
///
/// <para><b>ولذلك يُضبط الدورُ هنا خادميّاً بمتغيّر البيئة، لا بترويسة عميل.</b> فترويسةٌ
/// تُضيّق تختبر النصفَ غيرَ الضارّ؛ والمقياسُ الحقيقيُّ مضيفٌ صلاحيّتُه منخفضةٌ من أصلها.
/// <b>ولا يُتّكل على الدور الافتراضيّ</b>: <c>ResolveApiRole</c> يُعيد <c>Administrator</c>
/// خارج الإنتاج و<c>Viewer</c> داخله، و<c>BRQOSH_ENV</c> قد يكون مضبوطاً «production» على
/// مستوى الجهاز (يضبطه المُنصِّب) — <b>فاختبارٌ يتّكل على الافتراض يفشل على أجهزةٍ نُصِّب
/// عليها المنتَج</b>. ويُعاد المتغيّرُ إلى ما كان في <c>Dispose</c>.</para>
///
/// <para>والتوازي مُعطَّلٌ في هذه الحزمة (<c>AssemblyInfo.cs</c>)، فضبطُ متغيّرِ بيئةٍ
/// على مستوى العمليّة آمنٌ هنا ولا يتسرّب إلى اختبارٍ متزامن.</para>
/// </summary>
public class WebhookSubscriptionAuthorizationTests : IClassFixture<ApiTestFixture>, IDisposable
{
    private const string Route = "/api/v1/webhooks/subscriptions";

    private readonly HttpClient _client;
    private readonly string? _roleBefore;

    public WebhookSubscriptionAuthorizationTests(ApiTestFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
        _roleBefore = Environment.GetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv);
        WebhookSubscriptionRegistry.ResetForTests();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv, _roleBefore);
        WebhookSubscriptionRegistry.ResetForTests();
        GC.SuppressFinalize(this);
    }

    private static void ConfigureHostRole(UserRole role) =>
        Environment.SetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv, role.ToString());

    private static HttpRequestMessage Subscribe(string url, Guid? tenant = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = JsonContent.Create(new { EventType = "DocumentPosted", TargetUrl = url }),
        };
        if (tenant is Guid t) request.Headers.Add("X-Tenant-Id", t.ToString());
        return request;
    }

    private static HttpRequestMessage Listing(Guid? tenant = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Route);
        if (tenant is Guid t) request.Headers.Add("X-Tenant-Id", t.ToString());
        return request;
    }

    // ── حارسُ الصلاحيّة ───────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ:</b> مضيفٌ صلاحيّتُه <c>Viewer</c> لا يزرع هدفَ تسليمٍ خارجيّاً.
    ///
    /// <para>وهذه الحالةُ هي الإنتاجُ الافتراضيّ: <c>BRQOSH_API_ROLE</c> غيرُ مضبوطٍ في الإنتاج
    /// يسقط إلى <c>Viewer</c> انغلاقاً — <b>وكان يزرع رغم ذلك</b>.</para>
    /// </summary>
    [Fact]
    public async Task AViewerConfiguredHost_CannotPlantAnOutboundTarget()
    {
        ConfigureHostRole(UserRole.Viewer);

        var response = await _client.SendAsync(Subscribe("https://example.com/hooks/brqosh"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(WebhookSubscriptionRegistry.ListAll());
    }

    /// <summary>ولا يقرأ أهدافَ غيرِه — وهو الكشفُ البالغُ اليوم، إذ لا يلزمه تسليمٌ ليقع.</summary>
    [Fact]
    public async Task AViewerConfiguredHost_CannotListOutboundTargets()
    {
        ConfigureHostRole(UserRole.Viewer);

        var response = await _client.SendAsync(Listing());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وهو ألزمُ من المنع:</b> المضيفُ المُخوَّل ما زال يعمل.
    ///
    /// <para>فحارسٌ يرفض الجميعَ يُغلق الثغرةَ ويُعطّل التكامل، ولا يُميّز بينهما إلّا هذا.</para>
    /// </summary>
    [Fact]
    public async Task AnAdministratorConfiguredHost_StillRegistersAndLists()
    {
        ConfigureHostRole(UserRole.Administrator);

        var created = await _client.SendAsync(Subscribe("https://example.com/hooks/brqosh"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var listed = await _client.SendAsync(Listing());
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);

        var subs = await listed.Content.ReadFromJsonAsync<List<WebhookSubscription>>();
        Assert.NotNull(subs);
        Assert.Single(subs!);
        Assert.Equal("DocumentPosted", subs![0].EventType);
    }

    // ── عزلُ المستأجر ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>هدفُ مستأجرٍ لا يظهر لمستأجرٍ آخر.</b>
    ///
    /// <para>والنوعُ نفسُه كان بلا <c>TenantId</c>، <b>فالعزلُ مستحيلٌ بنيويّاً لا منسيّاً</b> —
    /// ولا يُرشَّح ما لا يُسجَّل. والمكشوفُ هنا ليس بياناتٍ محاسبيّةً بل <b>عناوينَ أنظمةٍ
    /// داخليّةٍ لمنشآتٍ أخرى</b>، وهي خريطةُ هجومٍ لا سجلُّ حركة.</para>
    /// </summary>
    [Fact]
    public async Task ATargetOfOneTenant_IsInvisibleToAnother()
    {
        ConfigureHostRole(UserRole.Administrator);

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var created = await _client.SendAsync(
            Subscribe("https://tenant-one.example.com/hooks", first));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var otherView = await _client.SendAsync(Listing(second));
        Assert.Equal(HttpStatusCode.OK, otherView.StatusCode);
        var otherSubs = await otherView.Content.ReadFromJsonAsync<List<WebhookSubscription>>();
        Assert.NotNull(otherSubs);
        Assert.Empty(otherSubs!);

        // ‏شاهدُ ضبطٍ موجب: صاحبُه يراه — فالعزلُ لا يُخفيه عن الجميع.
        var ownerView = await _client.SendAsync(Listing(first));
        var ownerSubs = await ownerView.Content.ReadFromJsonAsync<List<WebhookSubscription>>();
        Assert.NotNull(ownerSubs);
        Assert.Single(ownerSubs!);
        Assert.Equal("https://tenant-one.example.com/hooks", ownerSubs![0].TargetUrl);
    }

    /// <summary>
    /// <b>ومن لا هويّةَ له لا يرى أهدافَ من له هويّة.</b> وهو الاتجاهُ الذي كان يُسرّب:
    /// طلبٌ بلا <c>X-Tenant-Id</c> كان يُعيد كلَّ شيء.
    /// </summary>
    [Fact]
    public async Task ARequestWithoutATenant_SeesNoTenantScopedTarget()
    {
        ConfigureHostRole(UserRole.Administrator);

        var tenant = Guid.NewGuid();
        await _client.SendAsync(Subscribe("https://tenant-one.example.com/hooks", tenant));

        var anonymous = await _client.SendAsync(Listing());
        var subs = await anonymous.Content.ReadFromJsonAsync<List<WebhookSubscription>>();

        Assert.NotNull(subs);
        Assert.Empty(subs!);
    }
}
