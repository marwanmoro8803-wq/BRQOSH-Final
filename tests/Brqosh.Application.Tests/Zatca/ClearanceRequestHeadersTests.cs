using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٩) <b>ترويسةُ <c>Clearance-Status</c> كانت غائبةً عن نداء التخليص.</b>
///
/// <para>نداءُ <c>/invoices/clearance/single</c> يشترطها، <b>وغيابُها يردّ الطلبَ وإن صحّ
/// المسارُ والحمولةُ والتوقيعُ والختم</b>. <b>وهو بعينه العطلُ الذي وقع في CEEF-206</b> حين
/// غابت <c>Accept-Version</c>: كلُّ ما في المستند صحيح، والطلبُ يُردّ على البابِ.</para>
///
/// <para><b>والفاتورةُ القياسيّةُ لا تُباع بغيرها:</b> منشأةٌ ملزَمةٌ بالتخليص لا تُصدر
/// فاتورةً واحدةً مقبولةً حتى تُرسَل.</para>
///
/// <para><b>ولمَ يُقاس هنا لا في تابع الإرسال:</b> <c>HttpClient</c> ساكنٌ بلا درزٍ يُحقن،
/// <b>فترويسةٌ داخل الإرسال لا تُقاس إلّا بنداءٍ حقيقيٍّ للهيئة</b> — وهو ما لا يقع في
/// اختبار. فاستُخرج بناءُ الطلب، على سابقة <c>ReadAuthorityVerdict</c> في ز-٢.</para>
/// </summary>
public class ClearanceRequestHeadersTests
{
    private const string Token = "Basic dGVzdDp0ZXN0";

    private static HttpRequestMessage Build(ZatcaTransmissionMode mode) =>
        ZatcaSubmissionService.BuildSubmissionRequest(
            mode, "https://gw.example.test/api/", "{}", Token);

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary><b>القياسُ الحاكم:</b> نداءُ التخليص يحمل الترويسةَ بقيمتها.</summary>
    [Fact]
    public void AClearanceRequest_CarriesTheClearanceStatusHeader()
    {
        using var request = Build(ZatcaTransmissionMode.Clearance);

        Assert.Equal("1", Header(request, "Clearance-Status"));
    }

    /// <summary>
    /// <b>شاهدُ ضبط:</b> نداءُ الإبلاغ بلا هذه الترويسة — <b>فالمبسّطةُ تُبلَّغ ولا
    /// تُخلَّص</b>، وترويسةٌ لا يعرفها المسارُ زيادةٌ لا تُفيد. ولولاه لجاز أن تُضاف للمسارين
    /// معاً ويمرّ القياسُ الأوّل.
    /// </summary>
    [Fact]
    public void AReportingRequest_CarriesNoClearanceStatusHeader()
    {
        using var request = Build(ZatcaTransmissionMode.Reporting);

        Assert.Null(Header(request, "Clearance-Status"));
    }

    /// <summary>
    /// <b>والترويسةُ تتبع المسار</b> — فالشرطُ الذي يختار العنوانَ هو الذي يضع الترويسة،
    /// ولا يفترقان بعد تعديل.
    /// </summary>
    [Theory]
    [InlineData(ZatcaTransmissionMode.Clearance, "/invoices/clearance/single", true)]
    [InlineData(ZatcaTransmissionMode.Reporting, "/invoices/reporting/single", false)]
    public void TheHeaderFollowsTheEndpoint(
        ZatcaTransmissionMode mode, string endpoint, bool expectsHeader)
    {
        using var request = Build(mode);

        Assert.EndsWith(endpoint, request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(expectsHeader, Header(request, "Clearance-Status") is not null);
    }

    // ── وما كان قائماً يبقى ─────────────────────────────────────────────────

    /// <summary>
    /// <b>وحارسُ CEEF-206 باقٍ:</b> <c>Accept-Version</c> على المسارين — فالإضافةُ لم تُزح
    /// ترويسةً كانت تُرسَل، ولا الاستخراجُ أسقط شيئاً.
    /// </summary>
    [Theory]
    [InlineData(ZatcaTransmissionMode.Clearance)]
    [InlineData(ZatcaTransmissionMode.Reporting)]
    public void AcceptVersion_IsStillSentOnBothPaths(ZatcaTransmissionMode mode)
    {
        using var request = Build(mode);

        Assert.Equal("V2", Header(request, "Accept-Version"));
        Assert.Equal("application/json", request.Content?.Headers.ContentType?.MediaType);
        Assert.Contains(request.Headers.Accept, a => a.MediaType == "application/json");
    }

    /// <summary>والاعتمادُ يُمرَّر كما هو — والاستخراجُ نقلَه ولم يُعِد تركيبَه.</summary>
    [Fact]
    public void TheAuthorizationHeader_IsCarriedThrough()
    {
        using var request = Build(ZatcaTransmissionMode.Clearance);

        Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
        Assert.Equal("dGVzdDp0ZXN0", request.Headers.Authorization?.Parameter);
    }

    /// <summary>
    /// <b>وشرطةُ العنوان لا تتضاعف</b> — عنوانٌ ينتهي بها وآخرُ لا، والناتجُ واحد.
    /// </summary>
    [Theory]
    [InlineData("https://gw.example.test/api/")]
    [InlineData("https://gw.example.test/api")]
    public void TheBaseUrlSlash_IsNormalised(string baseUrl)
    {
        using var request = ZatcaSubmissionService.BuildSubmissionRequest(
            ZatcaTransmissionMode.Clearance, baseUrl, "{}", Token);

        Assert.Equal("/api/invoices/clearance/single", request.RequestUri!.AbsolutePath);
    }
}
