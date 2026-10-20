using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>tax-zatca#16</c> · إجراءٌ مؤقّتٌ حتى قرار المالك) <b>الجهازُ الثاني يُقال له أين يُختم — ولا يُدعى إلى ما يكسر الأوّل.</b>
///
/// <para><b>ما كان:</b> إعداداتُ الهيئة مشتركةٌ في القاعدة، ومفتاحُ الختم مشفَّرٌ لجهاز تسجيله (DPAPI). فالجهازُ الثاني يُرفض بـ«أكمِل تسجيل
/// الشهادة» — <b>وتشغيلُ المعالج عليه يكتب فوق إعدادات الأوّل فيتوقّف الأوّل</b>. والآن يُسجَّل جهازُ الختم عند التسجيل، ويُقال للثاني إنّ
/// الختمَ هناك، ويسأله المعالجُ قبل أن ينقله.</para>
/// </summary>
public class AnotherWorkstationIsToldWhereSigningLivesTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public AnotherWorkstationIsToldWhereSigningLivesTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<ZatcaSettings> SetupAsync()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("منشأة", "310123456700099", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
        ZatcaReadySeller.Complete(company);
        var customer = Customer.Create(_companyId, "عميل نقدي");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        var missing = Path.Combine(Path.GetTempPath(), "brqosh-no-such-csid-" + Guid.NewGuid().ToString("N"));
        settings.Update(isEnabled: true, ZatcaEnvironment.Simulation, "", Path.Combine(missing, "cert.pem"), Path.Combine(missing, "private.key"), "");
        await _db.SaveChangesAsync();
        return settings;
    }

    private Task SellAsync()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 10));
        invoice.AddLine("خدمة", 1m, 100m);
        return new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    [Fact]
    public async Task OnAnotherWorkstation_TheRefusalNamesTheRegisteredMachine_NotTheCertificateWizard()
    {
        var settings = await SetupAsync();
        settings.RecordSigningMachine("ANOTHER-WORKSTATION");
        await _db.SaveChangesAsync();

        var refused = await Assert.ThrowsAsync<AccountingException>(SellAsync);

        Assert.Contains("الجهاز المسجَّل لدى الهيئة", refused.Message, StringComparison.Ordinal);
        Assert.True(ZatcaSettingsSecrets.IsSignedOnAnotherMachine(settings));
    }

    [Fact]
    public async Task OnTheSigningWorkstation_AMissingCertificate_StillPointsToTheWizard()
    {
        var settings = await SetupAsync();
        settings.RecordSigningMachine(LicenseService.MachineId());
        await _db.SaveChangesAsync();

        var refused = await Assert.ThrowsAsync<AccountingException>(SellAsync);

        Assert.Contains("بلا شهادة توقيع", refused.Message, StringComparison.Ordinal);
        Assert.False(ZatcaSettingsSecrets.IsSignedOnAnotherMachine(settings));
    }

    [Fact]
    public void RegisteringACertificate_RecordsThisMachineAsTheSigningOne()
    {
        var settings = ZatcaSettings.CreateDefault(companyId: 1);
        Assert.False(ZatcaSettingsSecrets.IsSignedOnAnotherMachine(settings));   // ‏إعدادٌ سابقٌ لا يُعرف جهازُه — لا يُسأل عنه

        ZatcaSettingsSecrets.ApplyProtectedCsid(settings, "cert.pem", "private.key", "token", "req-1", "secret");

        Assert.Equal(LicenseService.MachineId(), settings.SigningMachineId);
        Assert.False(ZatcaSettingsSecrets.IsSignedOnAnotherMachine(settings));
    }
}
