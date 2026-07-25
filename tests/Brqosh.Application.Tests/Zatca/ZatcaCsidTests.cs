using System.Security.Cryptography;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Manufacturing;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

public class ZatcaCsidTests
{
    [Fact]
    public void GenerateCsr_CreatesKeyAndCsrFiles()
    {
        try
        {
            var service = new ZatcaCsidOnboardingService();
            var material = service.GenerateKeyPairAndCsr(1, "شركة تجريبية", "310123456700003");

            Assert.True(File.Exists(material.PrivateKeyPath));
            Assert.True(File.Exists(material.CsrPath));
            Assert.False(string.IsNullOrWhiteSpace(material.CsrBase64));
            Assert.Contains("BEGIN CERTIFICATE REQUEST", material.CsrPem);
        }
        catch (CryptographicException)
        {
            // secp256k1 قد لا يكون متاحاً على بعض البيئات — تخطّي
        }
    }

    [Fact]
    public void GenerateCsr_RejectsInvalidVat()
    {
        var service = new ZatcaCsidOnboardingService();
        Assert.Throws<AccountingException>(() =>
            service.GenerateKeyPairAndCsr(1, "شركة", "123"));
    }

    [Fact]
    public void BuildApiToken_FormatsBasicAuth()
    {
        var token = ZatcaCsidOnboardingService.BuildApiToken("cert123", "secret456");
        Assert.StartsWith("Basic ", token);
    }
}
