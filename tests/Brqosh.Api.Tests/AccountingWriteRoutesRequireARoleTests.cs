using System.Net;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Api.Tests;

/// <summary>
/// (م-٤ · ن-١٦) <b>خمسةُ مساراتِ كتابةٍ كانت بلا حارسِ دورٍ إطلاقاً.</b>
///
/// <para>عرفُ المستودع أن تفحص خدماتُ البنية الصلاحيّةَ بنفسها — ويُطبَّق في
/// <c>FxClosingTranslationService.TranslatePeriodAsync</c> فعلاً. <b>لكنّ أربعاً من خمسِ خدماتٍ على مسارات
/// الكتابة لا تذكر <c>SecurityContext</c> إطلاقاً</b>، واثنتان تكتبان عبر <c>DbContext</c>
/// مباشرةً في <c>Program.cs</c> بلا خدمةٍ أصلاً.</para>
///
/// <para><b>وتصحيحٌ لنطاق البند:</b> قيل ثلاثةُ مسارات، <b>والمقيسُ خمسة</b> — والمنسيّان
/// <c>POST /companies/{id}/exchange-rates/sync</c> و<c>POST /report-snapshots/trial-balance</c>،
/// وكلاهما يكتب ويُبلَغ.</para>
///
/// <para><b>وما يُكتب ليس هامشاً:</b> سعرُ صرفٍ يدخل كلَّ إعادةِ تقييمٍ وكلَّ قائمةٍ
/// موحَّدة، <b>وخريطةُ استبعادٍ بينيّةٍ تُحذف فتتغيّر كلُّ نتيجةٍ مجمَّعةٍ بعدها</b> —
/// بمفتاحٍ دورُه <c>Viewer</c>.</para>
///
/// <para><b>ويُضبط الدورُ خادميّاً بمتغيّر البيئة لا بترويسة عميل</b> — فترويسةُ
/// <c>X-Api-Role</c> تُضيّق ولا تُوسّع، فمُرسِلُها يملك المفتاحَ الأقوى ويُقيّد نفسَه طوعاً.
/// <b>والحالةُ الحقيقيّةُ مضيفٌ صلاحيّتُه منخفضةٌ من أصلها</b> — وهي الافتراضُ في الإنتاج
/// حين لا يُضبط <c>BRQOSH_API_ROLE</c>. ويُعاد المتغيّرُ إلى ما كان في <c>Dispose</c>.</para>
/// </summary>
public class AccountingWriteRoutesRequireARoleTests : IClassFixture<ApiTestFixture>, IDisposable
{
    private readonly HttpClient _client;
    private readonly string? _roleBefore;

    public AccountingWriteRoutesRequireARoleTests(ApiTestFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
        _roleBefore = Environment.GetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv, _roleBefore);
        GC.SuppressFinalize(this);
    }

    private static void ConfigureHostRole(UserRole role) =>
        Environment.SetEnvironmentVariable(ApiSecuritySettings.ApiRoleEnv, role.ToString());

    /// <summary>المساراتُ الخمسةُ التي تكتب ولا تُحرَس — بأقلِّ ما يبلغها.</summary>
    public static TheoryData<string, string> WriteRoutes => new()
    {
        { "POST", "/api/v1/company-groups/1/intercompany-account-maps?accountCode=1110" },
        { "DELETE", "/api/v1/company-groups/1/intercompany-account-maps/1110" },
        { "POST", "/api/v1/exchange-rates?companyId=1&fromCurrency=USD&toCurrency=SAR&effectiveDate=2026-01-01&rate=3.75" },
        { "POST", "/api/v1/companies/1/exchange-rates/sync" },
        { "POST", "/api/v1/report-snapshots/trial-balance?companyId=1&from=2026-01-01&to=2026-12-31" },
    };

    /// <summary>
    /// <b>القياسُ المركزيّ:</b> مضيفٌ صلاحيّتُه <c>Viewer</c> لا يكتب على أيٍّ منها.
    ///
    /// <para><b>والرمزُ في الجسم جزءٌ من القياس</b>: <c>403</c> وحدَها قد تأتي من حارس
    /// المستأجر، <b>فلا تُميّز حارسَ الدور</b>. و<c>accounting_write_forbidden</c> لا
    /// يصدر إلّا عنه.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(WriteRoutes))]
    public async Task AViewerConfiguredHost_CannotWriteThroughAnyOfThem(string method, string route)
    {
        ConfigureHostRole(UserRole.Viewer);

        var response = await _client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), route));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("accounting_write_forbidden", await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وهو ألزمُ من المنع:</b> المضيفُ المُخوَّل لا يُردّ بالحارس.
    ///
    /// <para>ولا يُشترط نجاحُ الطلب — فالقاعدةُ في هذا المِرفاس فارغة، فقد يردّ
    /// <c>400</c> أو <c>404</c>. <b>المشترَطُ ألّا يكون الردُّ حارسَ الدور</b>، وإلّا كان
    /// الإصلاحُ قفلاً على الجميع لا حارساً.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(WriteRoutes))]
    public async Task AnAccountantConfiguredHost_IsNotStoppedByTheRoleGate(string method, string route)
    {
        ConfigureHostRole(UserRole.Accountant);

        var response = await _client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), route));

        Assert.DoesNotContain("accounting_write_forbidden", await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والقراءةُ لا تُحرس بهذا</b> — فحارسُ كتابةٍ يمنع القراءةَ يُعطّل التكامل كلَّه،
    /// <b>ويجعل القياسَ أعلاه أخضرَ لسببٍ خاطئ</b>.
    /// </summary>
    [Fact]
    public async Task AViewerConfiguredHost_CanStillRead()
    {
        ConfigureHostRole(UserRole.Viewer);

        var response = await _client.GetAsync("/api/v1/billing-plans");

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
