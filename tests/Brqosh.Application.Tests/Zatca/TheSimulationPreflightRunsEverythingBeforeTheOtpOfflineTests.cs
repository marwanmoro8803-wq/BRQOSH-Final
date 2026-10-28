using System.Text;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// ‏(Phase 7 · تجهيزُ تجربة المحاكاة) <b>كلُّ ما قبل OTP يُقاس محلّيّاً وبلا اتصال</b> — الـCSR بقارئٍ مستقلّ (BouncyCastle)،
/// ‏وعيّناتُ الامتثال مختومةً بشهادةٍ ذاتيّة التوقيع ثمّ مُتحقَّقاً منها: البصمة والتوقيع وQR وسلسلة ICV/PIH.
/// ‏والـOTP حقلٌ في الإعداد: غيابُه لا يُفشل شيئاً، ويُسمّى الخطوةَ الباقية.
/// </summary>
public class TheSimulationPreflightRunsEverythingBeforeTheOtpOfflineTests
{
    private static ZatcaOnboardingOptions Options(string invoiceType = "1100", string? otp = null, ZatcaPortal portal = ZatcaPortal.Simulation) =>
        new(1, portal, otp, "310000000000003", "1010000000", "BRQOSH Trading", "Riyadh Branch",
            "1234 King Fahd Road, Riyadh", "Trading", invoiceType);

    [Fact]
    public void WithoutAnOtp_EveryLocalCheckPasses_AndTheOnlyStepLeftIsTheRealOtp()
    {
        var report = ZatcaSimulationPreflight.Run(Options());

        Assert.True(report.Ready, ZatcaSimulationPreflight.Format(report));
        Assert.False(report.OtpPresent);
        Assert.Equal(ZatcaServerOnboarding.AwaitingOtpMessage, report.NextStep);
        Assert.Contains("enter real OTP on registration", report.NextStep);
        Assert.Equal("https://gw-fatoora.zatca.gov.sa/e-invoicing/simulation", report.EndpointBaseUrl);
        Assert.Equal(6, report.Documents.Count);   // فاتورة/دائن/مدين × ضريبيّة/مبسّطة
        Assert.Contains(report.Checks, c => c.Name == "CSR: قالب الشهادة" && c.Passed && c.Detail.StartsWith("PREZATCA-Code-Signing"));
        Assert.Contains("BEGIN CERTIFICATE REQUEST", report.CsrPem);
    }

    [Fact]
    public void TheSignedSamples_PassTheIndependentTestVerifierToo()
    {
        var report = ZatcaSimulationPreflight.Run(Options());
        foreach (var doc in report.Documents)
        {
            var verdict = ZatcaSignatureVerifier.Verify(doc.SignedXml);
            Assert.True(verdict.InvoiceDigestMatches && verdict.SignedPropertiesDigestMatches, $"{doc.Label}: {verdict}");
        }
    }

    [Fact]
    public void WithAnOtpInTheSettings_TheNextStepIsTheRealOnboarding_AndNothingWasSent()
    {
        var report = ZatcaSimulationPreflight.Run(Options(otp: "654321"));

        Assert.True(report.Ready);
        Assert.True(report.OtpPresent);
        Assert.Contains("/zatca-signer/onboard", report.NextStep);
    }

    [Fact]
    public void ASimplifiedOnlyUnit_GetsThreeSamples_EachWithTheCertificateSignatureTag()
    {
        var report = ZatcaSimulationPreflight.Run(Options(invoiceType: "0100"));

        Assert.True(report.Ready, ZatcaSimulationPreflight.Format(report));
        Assert.Equal(3, report.Documents.Count);
        Assert.All(report.Documents, d => Assert.StartsWith("SIM", d.Label));
    }

    [Fact]
    public void AWrongVat_FailsTheSettingsCheck_AndStopsBeforeAnyKey()
    {
        var bad = Options() with { Vat = "123" };
        var report = ZatcaSimulationPreflight.Run(bad);

        Assert.False(report.Ready);
        Assert.Single(report.Checks);
        Assert.Null(report.CsrPem);
        Assert.Empty(report.Documents);
    }

    [Fact]
    public void ACsrBuiltForAnotherEnvironment_IsCaughtByTheTemplateCheck()
    {
        var o = Options();
        var key = ZatcaSecp256k1.GenerateKeyPem();
        var (der, _) = ZatcaCsidOnboardingService.BuildCsr(key, o.OrganizationName, o.Vat, o.SolutionName,
            Brqosh.Domain.Zatca.ZatcaEnvironment.Simulation, o.InvoiceType, o.Address, o.BusinessCategory,
            organizationUnit: o.Branch, commonName: $"{o.SolutionName}-{o.Vat}",
            certificateTemplate: ZatcaPortals.CertificateTemplate(ZatcaPortal.Production));

        var checks = ZatcaSimulationPreflight.InspectCsr(der, o);

        Assert.Contains(checks, c => c.Name == "CSR: قالب الشهادة" && !c.Passed);
        Assert.Contains(checks, c => c.Name == "CSR: SAN" && c.Passed);
        Assert.Contains(checks, c => c.Name == "CSR: المنحنى" && c.Passed);
    }

    [Fact]
    public void ADocumentEditedAfterSigning_FailsItsHashCheck()
    {
        var o = Options();
        var report = ZatcaSimulationPreflight.Run(o);
        var first = report.Documents[0];
        var sample = ZatcaServerOnboarding.ComplianceSamples(o).First().Data;

        var xml = Encoding.UTF8.GetString(first.SignedXml);
        var edited = Encoding.UTF8.GetBytes(xml.Replace("Compliance sample item", "Compliance sample itex"));
        var key = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(ExtractCertificatePem(xml));
        var problems = ZatcaSimulationPreflight.InspectDocument(edited, sample with { Icv = "1" }, o,
            ZatcaHashHelper.GenesisPreviousInvoiceHash(), 1, key.PublicKey.ExportSubjectPublicKeyInfo());

        Assert.Contains("بصمةُ الفاتورة لا تطابق المستند", problems);
        Assert.Contains("QR6 البصمة", problems);
    }

    [Fact]
    public void TheReportAndSamples_AreWrittenForAnOfflineSdkRun()
    {
        var dir = Directory.CreateTempSubdirectory("brqosh-preflight-").FullName;
        try
        {
            var report = ZatcaSimulationPreflight.Run(Options());
            var path = ZatcaSimulationPreflight.WriteTo(report, dir);

            Assert.True(File.Exists(Path.Combine(dir, "request.csr")));
            Assert.Equal(6, Directory.GetFiles(dir, "*.xml").Length);
            Assert.Contains("[OK] CSR: SAN", File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(dir, "*.key"));
        }
        finally { Directory.Delete(dir, true); }
    }

    private static string ExtractCertificatePem(string signedXml)
    {
        var m = System.Text.RegularExpressions.Regex.Match(signedXml, @"<(?:\w+:)?X509Certificate>([^<]+)</");
        Assert.True(m.Success, "لا شهادة في المستند المختوم");
        return "-----BEGIN CERTIFICATE-----\n" + m.Groups[1].Value + "\n-----END CERTIFICATE-----";
    }
}
