using System.Net;
using System.Net.Http.Json;
using Brqosh.Application.Platform;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Companies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Brqosh.Api.Tests;

/// <summary>
/// (ح-٦٤ · evidence-integrity#6) <b>عزلُ المستأجر على <c>/api/v1/customers</c> — بشركةٍ حقيقيّةٍ لا بقائمةٍ فارغة.</b>
///
/// <para><b>ما كان:</b> الاختباران يسألان <c>/api/v1/companies</c> ثمّ <c>return;</c> إن لم يجدا <c>"id"</c> —
/// <b>والمصنعُ يبدأ بقاعدة SQLite فارغة</b>، فلم يبلغ أيٌّ منهما سطرَ تأكيدٍ قطّ: يمرّان في أجزاءٍ من المئة من الثانية
/// لأنّهما لا يفعلان شيئاً. و<c>OPEN_ITEMS</c> يعدّهما من «سبع حزم حراسة» خلف «tenant default-deny ✅ محروس».
/// وهذا بعينه ما يُدينه <c>PostgresFactAttribute</c>: <b>الرجوعُ الصامتُ يُقرأ نجاحاً</b>.</para>
///
/// <para><b>الآن:</b> المستأجرُ وشركتُه يُنشآن عبر <c>/api/v1/tenants/provision</c> — الطريقُ الذي يُنشئ به المشغّلُ
/// مستأجراً فعلاً، لا كتابةٌ مباشرةٌ في القاعدة من خلف الـAPI — <b>ويُشترط وجودُهما قبل أيّ سؤال</b>. فإن تعطّل الإنشاءُ
/// سقط الاختبارُ باسمه بدل أن يمرّ صامتاً.</para>
/// </summary>
public class TenantMiddlewareTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;

    public TenantMiddlewareTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Customers_WithAStrangersTenant_Returns403()
    {
        var client = _fixture.Factory.CreateClient();
        var (_, companyId) = await ProvisionAsync(client, "Tenant Wrong Co", "300000000000301");

        var response = await GetCustomersAsync(client, companyId, tenant: Guid.NewGuid());

        await AssertTenantForbiddenAsync(response);
    }

    /// <summary>الحالةُ الواقعيّة: مستأجرٌ حقيقيٌّ يطلب شركةَ مستأجرٍ حقيقيٍّ آخر.</summary>
    [Fact]
    public async Task Customers_OfAnotherTenantsCompany_Returns403()
    {
        var client = _fixture.Factory.CreateClient();
        var (tenantA, _) = await ProvisionAsync(client, "Tenant A Co", "300000000000302");
        var (_, companyB) = await ProvisionAsync(client, "Tenant B Co", "300000000000303");

        var response = await GetCustomersAsync(client, companyB, tenant: tenantA);

        await AssertTenantForbiddenAsync(response);
    }

    /// <summary><b>الضابطُ الموجب</b> — المستأجرُ نفسُه يمرّ؛ وإلّا فالـ403 أعلاه لا تشهد بعزلٍ بل بمسارٍ مغلقٍ للجميع.</summary>
    [Fact]
    public async Task Customers_WithTheCompanysOwnTenant_ReturnsOk()
    {
        var client = _fixture.Factory.CreateClient();
        var (tenant, companyId) = await ProvisionAsync(client, "Tenant Own Co", "300000000000304");

        var response = await GetCustomersAsync(client, companyId, tenant);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// <b>وضعُ SaaS بلا ترويسة</b> — «default-deny» الذي يعدّه <c>OPEN_ITEMS</c> محروساً: حاملُ المفتاح المشترك
    /// لا يقرأ عملاءَ شركةٍ بحذف <c>X-Tenant-Id</c>. والضابطُ في الاختبار نفسِه: الترويسةُ الصحيحةُ تمرّ.
    /// </summary>
    [Fact]
    public async Task Customers_InSaaSMode_WithoutTenantHeader_Returns403_AndWithItReturnsOk()
    {
        using var factory = new IsolatedApiFactory(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [Brqosh.Infrastructure.Security.ApiSecuritySettings.SaaSModeEnv] = "true",
            })));
        try
        {
            var client = factory.CreateClient();
            var (tenant, companyId) = await ProvisionAsync(client, "Tenant SaaS Co", "300000000000305");

            await AssertTenantForbiddenAsync(await GetCustomersAsync(client, companyId, tenant: null));
            Assert.Equal(HttpStatusCode.OK, (await GetCustomersAsync(client, companyId, tenant)).StatusCode);
        }
        finally
        {
            TenantContext.EnforceTenantIsolation = false;
        }
    }

    [Fact]
    public async Task Health_RemainsOpen_WithTenantHeader()
    {
        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── الأدوات ─────────────────────────────────────────────────────────────────

    private static async Task<(Guid TenantId, long CompanyId)> ProvisionAsync(HttpClient client, string name, string vat)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tenants/provision",
            new TenantProvisionRequest(name, vat, "TRIAL", BusinessProfile.General));
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"إنشاءُ المستأجر فشل ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<ProvisionResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.TenantId);
        Assert.True(body.CompanyId > 0, "الإنشاءُ لم يُعِد شركة — فلا شيءَ يُعزَل ولا اختبار.");
        return (body.TenantId, body.CompanyId);
    }

    private static Task<HttpResponseMessage> GetCustomersAsync(HttpClient client, long companyId, Guid? tenant)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/customers?companyId={companyId}");
        if (tenant is Guid t)
            request.Headers.Add("X-Tenant-Id", t.ToString());
        return client.SendAsync(request);
    }

    /// <summary>403 <b>بسبب العزل</b> — لا أيُّ 403: مفتاحٌ ناقصٌ أو دورٌ ضيّقٌ يُعطيان الرمزَ نفسَه لسببٍ آخر.</summary>
    private static async Task AssertTenantForbiddenAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"متوقَّعٌ 403، وجاء {(int)response.StatusCode}: {body}");
        Assert.Contains("tenant_", body, StringComparison.Ordinal);
    }

    private sealed record ProvisionResponse(Guid TenantId, long CompanyId, string PlanCode);
}
