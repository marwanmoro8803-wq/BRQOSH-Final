using Brqosh.Application.Tests.TestSupport;
using Brqosh.Infrastructure.Licensing;

namespace Brqosh.Application.Tests.Licensing;

public class LicensingTests
{
    [Fact]
    public void Valid_signed_license_passes_validation()
    {
        using var _ = RsaTestKeys.UseForValidation();
        var company = "شركة اختبار";
        var machineId = LicenseService.MachineId();
        var json = RsaTestKeys.Sign(company, machineId);

        Assert.True(RsaLicenseValidator.TryValidate(company, machineId, json, out var payload));
        Assert.Equal(company, payload!.CompanyName);
    }

    [Fact]
    public void Tampered_license_fails_validation()
    {
        using var _ = RsaTestKeys.UseForValidation();
        var company = "شركة";
        var machineId = LicenseService.MachineId();
        var json = RsaTestKeys.Sign(company, machineId) + "tampered";

        Assert.False(RsaLicenseValidator.TryValidate(company, machineId, json, out LicensePayload? _));
    }

    [Fact]
    public void Wrong_machine_id_fails_validation()
    {
        using var _ = RsaTestKeys.UseForValidation();
        var json = RsaTestKeys.Sign("شركة", "OTHERMACHINE0001");

        Assert.False(RsaLicenseValidator.TryValidate("شركة", LicenseService.MachineId(), json, out LicensePayload? _));
    }
}
