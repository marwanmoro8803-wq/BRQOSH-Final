using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// ‏(ADR 0003 · Phase 7) <b>تسجيلُ خادم الختم من الإعداد وحده، من أوّله لآخره</b> — أمام هيئةٍ مُحاكاةٍ تتصرّف كالبيئة التجريبيّة
/// ‏للمطوّرين: الـOTP المنشور، وقالبُ TSTZATCA، وفحوصُ الامتثال لكلّ نوع مستند، ثمّ شهادةُ الإنتاج. وبيئةٌ تشترط OTP بلا OTP لا تُرسل شيئاً.
/// </summary>
[Collection("SequentialEnv")]
public class TheCentralSignerOnboardsEndToEndFromConfigurationTests : IDisposable
{
    private const long Company = 1;
    private const string Vat = "310123456700003";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "brqosh-onboard-" + Guid.NewGuid().ToString("N"));
    private readonly FakeZatca _zatca = new();
    private readonly BrqoshDbContext _db;

    public TheCentralSignerOnboardsEndToEndFromConfigurationTests()
    {
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, _root);
        ZatcaCsidOnboardingService.HandlerOverride = _zatca;
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Database.CloseConnection();
        _db.Dispose();
        ZatcaCsidOnboardingService.HandlerOverride = null;
        Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, null);
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static Dictionary<string, string?> Config(string environment, string invoiceType = "1100", string? otp = null) => new()
    {
        ["Zatca:Onboarding:Environment"] = environment,
        ["BRQOSH_ZATCA_VAT"] = Vat,
        ["Zatca:Onboarding:Cr"] = "1010010000",
        ["Zatca:Onboarding:OrganizationName"] = "Brqosh Test Co",
        ["BRQOSH_ZATCA_BRANCH"] = "Riyadh Branch",
        ["Zatca:Onboarding:Address"] = "Riyadh",
        ["BRQOSH_ZATCA_BUSINESS_CATEGORY"] = "Trading",
        ["Zatca:Onboarding:InvoiceType"] = invoiceType,
        ["Zatca:Onboarding:Otp"] = otp,
    };

    private ZatcaServerOnboarding Onboarding() => new(_db,
        generateKey: o =>
        {
            // ‏مفتاحٌ صريحٌ في موضعه (ختمُ DPAPI ويندوزيّ) — والـCSR نفسُه الذي يُرسل في التشغيل.
            var keyPem = ZatcaSecp256k1.GenerateKeyPem();
            var dir = Path.Combine(_root, "zatca", o.CompanyId.ToString(), "csid");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "private.key"), keyPem);
            _zatca.KeyPem = keyPem;
            var (der, pem) = ZatcaCsidOnboardingService.BuildCsr(keyPem, o.OrganizationName, o.Vat, o.SolutionName,
                ZatcaPortals.Environment(o.Portal), o.InvoiceType, o.Address, o.BusinessCategory,
                o.Branch, $"{o.SolutionName}-{o.Vat}", ZatcaPortals.CertificateTemplate(o.Portal));
            return new ZatcaKeyMaterial(Path.Combine(dir, "private.key"), "", Convert.ToBase64String(der), pem);
        },
        readKeyPem: File.ReadAllText);

    [Fact]
    public async Task TheSandbox_OnboardsWithItsPublishedOtp_ThroughAllComplianceChecks_ToAProductionCsid()
    {
        var options = ZatcaOnboardingOptions.FromConfiguration(k => Config("sandbox").GetValueOrDefault(k));

        var result = await Onboarding().RunAsync(options);

        Assert.Equal(ZatcaOnboardingStatus.Onboarded, result.Status);
        Assert.Equal(ZatcaServerOnboarding.SandboxTestOtp, _zatca.OtpSeen);
        Assert.StartsWith("https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal/", _zatca.Paths[0]);
        Assert.Contains("TSTZATCA-Code-Signing", Encoding.ASCII.GetString(Convert.FromBase64String(_zatca.CsrBase64!)));
        Assert.Equal(6, _zatca.Paths.Count(p => p.EndsWith("/compliance/invoices")));
        Assert.Equal(6, result.ComplianceChecksPassed);
        Assert.EndsWith("/production/csids", _zatca.Paths[^1]);

        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == Company);
        Assert.True(settings.IsEnabled);
        Assert.Equal(ZatcaPortals.BaseUrl(ZatcaPortal.Sandbox), settings.ApiBaseUrl);
        Assert.True(File.Exists(settings.CertificateFilePath));
        Assert.Equal("777", result.ComplianceRequestId);
        Assert.Equal("888", settings.ComplianceRequestId);
    }

    [Fact]
    public async Task SimplifiedOnly_SubmitsThreeComplianceDocuments()
    {
        var options = ZatcaOnboardingOptions.FromConfiguration(k => Config("sandbox", "0100").GetValueOrDefault(k));
        var result = await Onboarding().RunAsync(options);
        Assert.Equal(ZatcaOnboardingStatus.Onboarded, result.Status);
        Assert.Equal(3, result.ComplianceChecksTotal);
    }

    [Theory]
    [InlineData("simulation")]
    [InlineData("production")]
    public async Task WithoutARealOtp_NothingIsSent_AndTheStepIsNamed(string environment)
    {
        var options = ZatcaOnboardingOptions.FromConfiguration(k => Config(environment).GetValueOrDefault(k));

        var result = await Onboarding().RunAsync(options);

        Assert.Equal(ZatcaOnboardingStatus.AwaitingOtp, result.Status);
        Assert.Contains("enter real OTP on registration", result.Message);
        Assert.Empty(_zatca.Paths);
    }

    [Fact]
    public async Task AFailedComplianceCheck_StopsBeforeTheProductionCsid()
    {
        _zatca.FailCompliance = true;
        var options = ZatcaOnboardingOptions.FromConfiguration(k => Config("simulation", otp: "654321").GetValueOrDefault(k));

        var result = await Onboarding().RunAsync(options);

        Assert.Equal(ZatcaOnboardingStatus.ComplianceFailed, result.Status);
        Assert.Equal("654321", _zatca.OtpSeen);
        Assert.StartsWith(ZatcaPortals.BaseUrl(ZatcaPortal.Simulation), _zatca.Paths[0]);
        Assert.DoesNotContain(_zatca.Paths, p => p.EndsWith("/production/csids"));
    }

    [Theory]
    [InlineData("Environment", "staging")]
    [InlineData("BRQOSH_ZATCA_VAT", "210123456700003")]
    [InlineData("Zatca:Onboarding:Cr", "12345")]
    [InlineData("Zatca:Onboarding:InvoiceType", "0000")]
    public void BadConfiguration_IsRefusedByName(string key, string value)
    {
        var config = Config("sandbox");
        config[key == "Environment" ? "Zatca:Onboarding:Environment" : key] = value;
        Assert.Throws<AccountingException>(() => ZatcaOnboardingOptions.FromConfiguration(k => config.GetValueOrDefault(k)));
    }

    /// <summary>‏هيئةٌ مُحاكاة: تُصدر شهادةً لمفتاح الـCSR، وتقبل فحوصَ الامتثال، وتُصدر شهادةَ الإنتاج.</summary>
    private sealed class FakeZatca : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public string? OtpSeen { get; private set; }
        public string? CsrBase64 { get; private set; }
        public string? KeyPem { get; set; }
        public bool FailCompliance { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.ToString();
            Paths.Add(path);
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            if (path.EndsWith("/compliance"))
            {
                OtpSeen = request.Headers.GetValues("OTP").Single();
                CsrBase64 = JsonDocument.Parse(body).RootElement.GetProperty("csr").GetString();
                return Json(new { requestID = 777, dispositionMessage = "ISSUED", binarySecurityToken = Certificate(), secret = "compliance-secret" });
            }
            if (path.EndsWith("/compliance/invoices"))
            {
                Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
                return FailCompliance
                    ? Json(new { validationResults = new { status = "ERROR" } }, HttpStatusCode.BadRequest)
                    : Json(new { validationResults = new { status = "PASS" }, reportingStatus = "REPORTED" });
            }
            if (path.EndsWith("/production/csids"))
                return Json(new { requestID = 888, dispositionMessage = "ISSUED", binarySecurityToken = Certificate(), secret = "production-secret" });
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private string Certificate()
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(KeyPem);
            var cert = new CertificateRequest("CN=Fake ZATCA CSID", ecdsa, HashAlgorithmName.SHA256)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            return Convert.ToBase64String(cert.Export(X509ContentType.Cert));
        }

        private static HttpResponseMessage Json(object value, HttpStatusCode code = HttpStatusCode.OK) =>
            new(code) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    }
}
