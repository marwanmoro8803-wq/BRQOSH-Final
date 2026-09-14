using System.Net;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Api.Tests;

/// <summary>
/// (م-٤ · ن-٤٥) <b>عزلُ المستأجرين كان مُطفأً في كلّ تنصيبٍ قائم — ويفشل مفتوحاً.</b>
///
/// <para><c>TenantContext.EnforceTenantIsolation</c> خاصّيّةٌ بلا مُهيّئ فافتراضُها
/// <c>false</c>، وتُضبط من <c>BRQOSH_SAAS_MODE</c> وحدَه. <b>والمتغيّرُ لا يُضبط في أيّ
/// وثيقةٍ ولا سكربتٍ ولا مُنصِّبٍ ولا CI</b> — مقيسٌ ببحثٍ على المستودع أعاد موضعين:
/// تعليقاً وإعلانَ الثابت. <b>وشاهدُ ضبطٍ موجب:</b> شقيقُه <c>BRQOSH_API_KEY</c> يظهر في
/// خمسة ملفّاتِ نشرٍ وتشغيل. <b>فهو مُطفأٌ دائماً، لا «يُنسى أحياناً».</b></para>
///
/// <para><b>وأثرُه أنّ الحارسَ يفشل مفتوحاً:</b> <c>TenantGuard</c> يُعيد <c>true</c> لأيّ
/// شركةٍ متى غاب المُعرِّف. <b>وسردُ الشركات بلا ترويسةٍ يُعيد صفوفَ كلّ المستأجرين —
/// ومعها <c>TenantId</c> نفسُه</b>، وهو المفتاحُ الذي يفحصه الحارسُ بعدها. فالتسريبُ
/// يُكمل نفسَه: طلبٌ واحدٌ بلا ترويسةٍ يُسلّم المفاتيحَ لكلّ ما بعده.</para>
///
/// <para><b>ونطاقُه مُقاسٌ لا مُضخَّم:</b> الواجهةُ <b>ليست في المُنصِّب</b> — قِيس أنّ
/// <c>installer/publish.ps1:30</c> ينشر <c>Brqosh.Desktop.csproj</c> وحدَه، وأنّ كودَ
/// <b>عزل المستأجرين</b> صفرٌ في سطح المكتب: <c>TenantContext</c> و<c>TenantGuard</c>
/// و<c>EnforceTenantIsolation</c> و<c>TenantId</c> — <b>صفرُ ملفّاتٍ لكلٍّ منها</b> تحت
/// <c>src/Brqosh.Desktop</c>. <b>وشاهدُ الضبط الموجب:</b> التقنيةُ نفسُها تجد
/// <c>TenantContext</c> و<c>TenantGuard</c> في <c>src/Brqosh.Api</c> — فالصفرُ عن بحثٍ
/// يرى، لا عن بحثٍ أعمى. فهذا انكشافُ استضافةٍ ذاتيّةٍ للواجهة، لا انكشافُ التنصيب المشحون.</para>
///
/// <para><b>(م-٥ · ن-٢١) وشاهدُ الضبط هنا كان كاذباً — ولم يكن بائتاً بل وُلد كاذباً.</b>
/// كان يقول «<c>SecurityContext</c> يصل سطحَ المكتب في <b>ثلاثة</b> ملفّات»، <b>والمقيسُ
/// ثلاثةٌ وأربعون</b> — <b>وثلاثةٌ وأربعون أيضاً عند الالتزام الذي كتب الجملة</b>
/// (<c>f26994a</c>). والخللُ في <b>الرمز المُستشهَد به لا في العدد</b>:
/// <c>SecurityContext</c> هويّةٌ وصلاحيّات، و<b>العزلُ المستأجِريُّ <c>TenantContext</c></b> —
/// فالضابطُ كان يقيس شيئاً آخر. <b>والحكمُ الذي يسنده صحيحٌ مع ذلك</b>، وقد أُعيد إسنادُه
/// أعلاه إلى ما يقيسه فعلاً.</para>
///
/// <para><b>والدرسُ يُكتب حيث وقع:</b> منهجُ هذه الحملة يقوم على «الادّعاءُ السلبيُّ يلزمه
/// شاهدُ ضبطٍ موجب». <b>وشاهدُ ضبطٍ لا يُعاد إنتاجُه أسوأُ من لا شاهد</b>: بلا شاهدٍ يبقى
/// القارئُ متيقّظاً، ومعه يطمئنّ إلى ما لم يُقَس.</para>
/// </summary>
public class TenantIsolationFailsClosedTests : IClassFixture<ApiTestFixture>, IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _enforceBefore = TenantContext.EnforceTenantIsolation;

    public TenantIsolationFailsClosedTests(ApiTestFixture fixture) =>
        _client = fixture.Factory.CreateClient();

    public void Dispose()
    {
        TenantContext.EnforceTenantIsolation = _enforceBefore;
        GC.SuppressFinalize(this);
    }

    // ── جدولُ القرار ──────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ:</b> الغيابُ يفرض العزلَ في الإنتاج ويُعطّله خارجه.
    ///
    /// <para>وهذا هو البند كلُّه: القرارُ كان ثنائيّاً — <b>مضبوطٌ أو مفتوح</b> — وصار
    /// ثلاثيّاً، فما لا يُصرَّح به <b>يفشل مغلقاً حيث يضرّ</b>.</para>
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void AnUnsetFlag_EnforcesInProductionOnly(bool isProduction, bool expected)
    {
        Assert.Equal(expected, ApiSecuritySettings.ResolveTenantIsolation(isProduction, null));
        Assert.Equal(expected, ApiSecuritySettings.ResolveTenantIsolation(isProduction, "   "));
    }

    /// <summary>
    /// <b>وقيمةٌ صريحةٌ تُطاع في الاتجاهين</b> — فمضيفٌ أحاديُّ المنشأة يُعطّل العزلَ
    /// <b>بقرارٍ مكتوب</b>، ومضيفُ تطويرٍ يُشغّله ليختبره.
    /// </summary>
    [Theory]
    [InlineData("1", false, true)]
    [InlineData("true", false, true)]
    [InlineData("TRUE", false, true)]
    [InlineData("0", true, false)]
    [InlineData("false", true, false)]
    public void AnExplicitFlag_IsObeyedInBothDirections(string configured, bool isProduction, bool expected) =>
        Assert.Equal(expected, ApiSecuritySettings.ResolveTenantIsolation(isProduction, configured));

    /// <summary>
    /// <b>وقيمةٌ لا تُفهم تُعامَل كالغياب — لا كتعطيل.</b>
    ///
    /// <para>وهذا هو الفرقُ الذي يُنقذ: <c>BRQOSH_SAAS_MODE=yes</c> خطأٌ مطبعيٌّ معقول،
    /// <b>ولو قُرئ تعطيلاً لفتح القاعدةَ كلَّها صامتاً على مضيفِ إنتاج</b>.</para>
    /// </summary>
    [Theory]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData("enabled")]
    public void AnUnparseableFlag_StillEnforcesInProduction(string configured)
    {
        Assert.True(ApiSecuritySettings.ResolveTenantIsolation(isProduction: true, configured));
        Assert.False(ApiSecuritySettings.ResolveTenantIsolation(isProduction: false, configured));
    }

    // ── المسارُ الحيّ ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>ومتى فُرض العزل، يُردّ السردُ بلا ترويسةٍ برمزٍ يقوله.</b>
    ///
    /// <para>وكان الحارسُ يُعيد <c>403</c> <b>بلا جسمٍ إطلاقاً</b> — فلا يعرف المُشغِّلُ
    /// أهو منعُ مستأجرٍ أم منعُ دور. والرمزُ هو ما يجعل الرسالةَ قابلةً للبحث.</para>
    /// </summary>
    [Fact]
    public async Task WhenEnforced_AHeaderlessListing_IsRefusedWithANamedReason()
    {
        TenantContext.EnforceTenantIsolation = true;

        var response = await _client.GetAsync("/api/v1/companies");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("tenant_header_required", await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وهو ألزمُ من المنع:</b> الترويسةُ الصحيحةُ تمرّ.
    ///
    /// <para>فحارسٌ يرفض الجميعَ يُغلق الثغرةَ ويُعطّل التكامل، ولا يُميّز بينهما إلّا هذا.</para>
    /// </summary>
    [Fact]
    public async Task WhenEnforced_AListingWithATenantHeader_IsAllowed()
    {
        TenantContext.EnforceTenantIsolation = true;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/companies");
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// <b>وشاهدٌ ثانٍ:</b> المضيفُ الأحاديُّ الذي عطّل العزلَ صراحةً يعمل كما كان.
    /// فالإصلاحُ لا يكسر استضافةً قائمةً اختارت وضعَها.
    /// </summary>
    [Fact]
    public async Task WhenNotEnforced_AHeaderlessListing_StillWorks()
    {
        TenantContext.EnforceTenantIsolation = false;

        var response = await _client.GetAsync("/api/v1/companies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── الوصل ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>إثباتُ أكوادٍ ثابت:</b> الإقلاعُ يشتقّ القرارَ بالمُحلِّل، لا بالعلَم الخامّ.
    ///
    /// <para>فالتوكيداتُ أعلاه تقيس دالّةً نقيّة، <b>ولا تُثبت أنّ الإقلاعَ يستدعيها</b> —
    /// وذلك بعينه ما جعل هذا البند يمرّ: حارسٌ مكتوبٌ صحيحاً، <b>وعلَمٌ لا يُضبط</b>.</para>
    /// </summary>
    [Fact]
    public void Startup_DerivesTheDecisionFromTheResolver()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "Brqosh.Api", "Program.cs"));

        var code = string.Join('\n', source
            .Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        Assert.Contains("ApiSecuritySettings.ResolveTenantIsolation(", code, StringComparison.Ordinal);
        Assert.DoesNotContain("ApiSecuritySettings.IsSaaSMode()", code, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }
}
