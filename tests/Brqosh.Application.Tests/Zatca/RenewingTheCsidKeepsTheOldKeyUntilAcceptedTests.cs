using System.Net;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٣٧ · ‏`tax-zatca#10`) <b>تجديدُ شهادة الإنتاج — والمفتاحُ القديمُ يبقى حتى تقبل الهيئةُ الجديد.</b>
///
/// <para>‏كان البديلُ الوحيدُ إعادةَ التسجيل من أوّله، وتوليدُ CSR يكتب فوق <c>private.key</c> فوراً: <b>فلو رُفض الطلبُ
/// ‏بقيت شهادةٌ قائمةٌ بلا مفتاحها</b> ووقف التوقيع. هنا يُولَّد مفتاحُ التجديد جانباً ولا يحلّ محلَّ القديم إلّا مع الشهادة المقبولة.</para>
/// </summary>
[Collection("SequentialEnv")]
public class RenewingTheCsidKeepsTheOldKeyUntilAcceptedTests : IDisposable
{
    private const long Company = 7;
    private const string Api = "https://zatca.test/e-invoicing/core";
    private const string CurrentToken = "Basic Y3VycmVudDpzZWNyZXQ=";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "brqosh-renew-" + Guid.NewGuid().ToString("N"));
    private readonly Capture _capture = new();
    private readonly ZatcaCsidOnboardingService _service = new();

    public RenewingTheCsidKeepsTheOldKeyUntilAcceptedTests()
    {
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, _root);
        ZatcaCsidOnboardingService.HandlerOverride = _capture;
    }

    public void Dispose()
    {
        ZatcaCsidOnboardingService.HandlerOverride = null;
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, null);
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string Dir => Path.Combine(_root, "zatca", Company.ToString(), "csid");
    private string ActiveKey => File.ReadAllText(Path.Combine(Dir, "private.key"));

    [Fact]
    public void TheRenewalCsr_LeavesTheActiveKeyUntouched()
    {
        _service.GenerateKeyPairAndCsr(Company, "شركة", "310123456700003");
        var before = ActiveKey;

        var renewal = _service.GenerateKeyPairAndCsr(Company, "شركة", "310123456700003", forRenewal: true);

        Assert.Equal(before, ActiveKey);
        Assert.NotEqual(before, File.ReadAllText(renewal.PrivateKeyPath));
    }

    [Fact]
    public async Task AnAcceptedRenewal_SwapsTheKeyWithItsCertificate()
    {
        _service.GenerateKeyPairAndCsr(Company, "شركة", "310123456700003");
        var renewal = _service.GenerateKeyPairAndCsr(Company, "شركة", "310123456700003", forRenewal: true);
        var renewingKey = File.ReadAllText(renewal.PrivateKeyPath);
        _capture.Reply = (HttpStatusCode.OK,
            "{\"requestID\":42,\"binarySecurityToken\":\"bmV3Y2VydA==\",\"secret\":\"s2\"}");

        var result = await _service.RenewProductionCsidAsync(Api, "654321", renewal.CsrBase64, CurrentToken, Company);

        Assert.Equal(HttpMethod.Patch, _capture.Method);
        Assert.Equal("/e-invoicing/core/production/csids", _capture.Path);
        Assert.Equal("V2", _capture.ApiVersion);
        Assert.Equal("654321", _capture.Otp);
        Assert.Equal(CurrentToken, _capture.Authorization);
        Assert.Equal("42", result.RequestId);
        Assert.Equal(renewingKey, ActiveKey);
        Assert.False(File.Exists(renewal.PrivateKeyPath));
    }

    [Fact]
    public async Task ARejectedRenewal_KeepsTheActiveKeyAndCertificate()
    {
        _service.GenerateKeyPairAndCsr(Company, "شركة", "310123456700003");
        var before = ActiveKey;
        var certPath = Path.Combine(Dir, "csid.crt.pem");
        File.WriteAllText(certPath, "current-cert");
        var renewal = _service.GenerateKeyPairAndCsr(Company, "شركة", "310123456700003", forRenewal: true);
        _capture.Reply = (HttpStatusCode.BadRequest, "{\"message\":\"Invalid OTP\"}");

        await Assert.ThrowsAsync<AccountingException>(() =>
            _service.RenewProductionCsidAsync(Api, "000000", renewal.CsrBase64, CurrentToken, Company));

        Assert.Equal(before, ActiveKey);
        Assert.Equal("current-cert", File.ReadAllText(certPath));
    }

    [Fact]
    public async Task ARenewalWithoutARenewalKey_IsRefusedBeforeTheNetwork()
    {
        _service.GenerateKeyPairAndCsr(Company, "شركة", "310123456700003");

        await Assert.ThrowsAsync<AccountingException>(() =>
            _service.RenewProductionCsidAsync(Api, "654321", "Y3Ny", CurrentToken, Company));

        Assert.Null(_capture.Method);
    }

    private sealed class Capture : HttpMessageHandler
    {
        public (HttpStatusCode Code, string Body) Reply { get; set; } = (HttpStatusCode.BadRequest, "{}");
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? ApiVersion { get; private set; }
        public string? Otp { get; private set; }
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath;
            ApiVersion = request.Headers.TryGetValues("Accept-Version", out var v) ? string.Join(",", v) : null;
            Otp = request.Headers.TryGetValues("OTP", out var o) ? string.Join(",", o) : null;
            Authorization = request.Headers.TryGetValues("Authorization", out var a) ? string.Join(",", a) : null;
            return Task.FromResult(new HttpResponseMessage(Reply.Code) { Content = new StringContent(Reply.Body) });
        }
    }
}
