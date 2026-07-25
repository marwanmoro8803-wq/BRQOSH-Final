using System.Security.Cryptography;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Application.Tests.TestSupport;

public static class RsaTestKeys
{
    private static readonly Lazy<(string PublicPem, string PrivatePem)> Keys = new(() =>
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportSubjectPublicKeyInfoPem(), rsa.ExportPkcs8PrivateKeyPem());
    });

    public static string PublicPem => Keys.Value.PublicPem;
    public static string PrivatePem => Keys.Value.PrivatePem;

    public static IDisposable UseForValidation()
    {
        RsaLicenseValidator.PublicKeyPemOverride = PublicPem;
        return new ResetScope();
    }

    public static string Sign(string company, string machineId, int days = 30) =>
        RsaLicenseValidator.CreateSignedLicenseJson(
            new LicensePayload(company, machineId, "test", DateTime.UtcNow.AddDays(days), ["core"]),
            PrivatePem);

    private sealed class ResetScope : IDisposable
    {
        public void Dispose() => RsaLicenseValidator.PublicKeyPemOverride = null;
    }
}
