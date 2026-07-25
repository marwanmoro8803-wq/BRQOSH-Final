using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// اختبار تكامل حي اختياري — يُشغَّل فقط عند ضبط متغيرات البيئة:
/// BRQOSH_ZATCA_OTP, BRQOSH_ZATCA_VAT (15 رقماً), BRQOSH_ZATCA_ORG (اسم المنشأة)
/// </summary>
public class ZatcaLiveCsidTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public ZatcaLiveCsidTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Live_compliance_csid_when_otp_env_configured()
    {
        var otp = Environment.GetEnvironmentVariable("BRQOSH_ZATCA_OTP");
        var vat = Environment.GetEnvironmentVariable("BRQOSH_ZATCA_VAT");
        var org = Environment.GetEnvironmentVariable("BRQOSH_ZATCA_ORG") ?? "شركة BRQOSH";

        if (string.IsNullOrWhiteSpace(otp) || string.IsNullOrWhiteSpace(vat))
            return; // تخطّي — اختبار اختياري للبيئة الحية

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync(org, vat, BusinessProfile.Retail);

        var service = new ZatcaCsidOnboardingService();
        var material = service.GenerateKeyPairAndCsr(company.Id, org, vat);

        var apiUrl = Environment.GetEnvironmentVariable("BRQOSH_ZATCA_API_URL")
            ?? "https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal";

        var result = await service.RequestComplianceCsidAsync(apiUrl, otp, material.CsrBase64, company.Id);

        Assert.False(string.IsNullOrWhiteSpace(result.RequestId));
        Assert.False(string.IsNullOrWhiteSpace(result.BinarySecurityToken));
        Assert.True(File.Exists(result.CertificatePemPath));
    }
}
