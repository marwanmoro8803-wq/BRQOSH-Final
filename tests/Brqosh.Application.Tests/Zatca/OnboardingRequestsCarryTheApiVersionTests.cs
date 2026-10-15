using System.Net;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٣٦ · ‏`tax-zatca#10`) <b>طلبا شهادة الهيئة يحملان <c>Accept-Version: V2</c>.</b>
///
/// <para>‏الهيئةُ ترفض كلَّ طلبٍ بلا هذه الترويسة (CEEF-206 في الإرسال). وكان طلبا الامتثال والإنتاج بلاها،
/// ‏<b>فالتسجيلُ الحيُّ يسقط قبل أوّل فاتورة</b>. المعالجُ يلتقط الطلبَ ثمّ يردّ ٤٠٠ — فلا شيءَ يُكتب على القرص.</para>
/// </summary>
[Collection("SequentialEnv")]
public class OnboardingRequestsCarryTheApiVersionTests : IDisposable
{
    private readonly Capture _capture = new();

    public OnboardingRequestsCarryTheApiVersionTests() =>
        ZatcaCsidOnboardingService.HandlerOverride = _capture;

    public void Dispose()
    {
        ZatcaCsidOnboardingService.HandlerOverride = null;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task TheComplianceRequest_CarriesTheApiVersion()
    {
        await Assert.ThrowsAsync<AccountingException>(() => new ZatcaCsidOnboardingService()
            .RequestComplianceCsidAsync("https://zatca.test/e-invoicing/developer-portal", "123456", "Y3Ny", 1));

        Assert.Equal("/e-invoicing/developer-portal/compliance", _capture.Path);
        Assert.Equal("V2", _capture.ApiVersion);
        Assert.Equal("123456", _capture.Otp);
    }

    [Fact]
    public async Task TheProductionRequest_CarriesTheApiVersion()
    {
        await Assert.ThrowsAsync<AccountingException>(() => new ZatcaCsidOnboardingService()
            .RequestProductionCsidAsync("https://zatca.test/e-invoicing/developer-portal", "1234567890", "dG9rZW4=", "secret", 1));

        Assert.Equal("/e-invoicing/developer-portal/production/csids", _capture.Path);
        Assert.Equal("V2", _capture.ApiVersion);
        Assert.Equal("Basic", _capture.AuthScheme);
    }

    private sealed class Capture : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? ApiVersion { get; private set; }
        public string? Otp { get; private set; }
        public string? AuthScheme { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri?.AbsolutePath;
            ApiVersion = request.Headers.TryGetValues("Accept-Version", out var v) ? string.Join(",", v) : null;
            Otp = request.Headers.TryGetValues("OTP", out var o) ? string.Join(",", o) : null;
            AuthScheme = request.Headers.Authorization?.Scheme;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"message\":\"captured\"}"),
            });
        }
    }
}
