using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Licensing;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// ‏(Phase 7) اتفاقيةُ الترخيص في القاعدة: مربوطةٌ بالمنشأة وبسجلّ التفعيل، تحمل الطرفين (اسم/ضريبي/سجل)، رقمٌ ضريبيٌّ سعوديٌّ صحيح،
/// ‏تُدقَّق، ولا تُعدَّل بعد حفظها إلا بإرفاق PDF مرّةً واحدة.
/// </summary>
public class TheLicenceAgreementIsSavedOnceLinkedAndAuditedTests : IDisposable
{
    private static readonly LicenseAgreementPartyInfo Vendor = new("مؤسسة برقش للبرمجيات", "1010101010", "300000000000003", "الرياض");
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public TheLicenceAgreementIsSavedOnceLinkedAndAuditedTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _companyId = new DatabaseInitializer(_db).InitializeAsync("شركة العميل", "311111111111113", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;
    }

    public void Dispose() => _db.Dispose();

    private async Task<LicenseActivation> ActivateAsync()
    {
        var a = LicenseActivation.Record(Guid.NewGuid().ToString("N"), "device", "professional", 3, null, false,
            new string('a', 64), DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        _db.LicenseActivations.Add(a);
        await _db.SaveChangesAsync();
        return a;
    }

    private LicenseAgreementService Service(LicenseAgreementPartyInfo? vendor = null) => new(_db, () => vendor ?? Vendor);

    [Fact]
    public async Task GenerateAndSave_LinksCompanyAndActivation_StoresBothParties_AndAudits()
    {
        var activation = await ActivateAsync();

        var agreement = await Service().GenerateAndSaveAsync(_companyId, "شركة العميل المحدودة", "311111111111113");

        var saved = await _db.LicenseAgreements.AsNoTracking().SingleAsync();
        Assert.Equal(_companyId, saved.CompanyId);
        Assert.Equal(activation.Id, saved.LicenseActivationId);
        Assert.Equal("professional", saved.Edition);
        Assert.Equal(3, saved.MaxCompanies);
        Assert.Equal(Vendor.Name, saved.FirstPartyName);
        Assert.Equal("300000000000003", saved.FirstPartyVatNumber);
        Assert.Equal("1010101010", saved.FirstPartyCommercialRegistration);
        Assert.Equal("311111111111113", saved.SecondPartyVatNumber);
        Assert.Contains("311111111111113", saved.BodyText);
        Assert.Contains(saved.AgreementNumber, saved.BodyText);
        Assert.Equal(64, saved.BodySha256.Length);
        Assert.Null(saved.PdfBytes);
        Assert.DoesNotContain("ريال", saved.BodyText);
        Assert.True(await _db.AuditLogEntries.AnyAsync(e => e.Action == LicenseAgreementService.CreateAction && e.EntityKey == agreement.Id.ToString()));
    }

    [Theory]
    [InlineData("31111111111111")]    // 14
    [InlineData("211111111111113")]   // لا يبدأ بـ3
    [InlineData("311111111111112")]   // لا ينتهي بـ3
    [InlineData("3111111111a1113")]
    public async Task ANonSaudiVat_IsRefused(string vat)
    {
        await ActivateAsync();
        await Assert.ThrowsAsync<AccountingException>(() => Service().GenerateAndSaveAsync(_companyId, "عميل", vat));
        Assert.Empty(_db.LicenseAgreements);
    }

    [Fact]
    public async Task WithoutAnActivation_OrVendorSettings_NothingIsSaved()
    {
        await Assert.ThrowsAsync<AccountingException>(() => Service().GenerateAndSaveAsync(_companyId, "عميل", "311111111111113"));
        await ActivateAsync();
        var noVendor = new LicenseAgreementService(_db, () => null);
        await Assert.ThrowsAsync<AccountingException>(() => noVendor.GenerateAndSaveAsync(_companyId, "عميل", "311111111111113"));
        Assert.Empty(_db.LicenseAgreements);
    }

    [Fact]
    public async Task ThePdf_IsAttachedOnce_ThenTheAgreementIsSealed()
    {
        await ActivateAsync();
        var agreement = await Service().GenerateAndSaveAsync(_companyId, "عميل", "311111111111113");

        await Service().AttachPdfAsync(agreement.Id, [1, 2, 3], null);
        await Assert.ThrowsAsync<AccountingException>(() => Service().AttachPdfAsync(agreement.Id, null, "C:/x.pdf"));

        _db.ChangeTracker.Clear();
        var saved = await _db.LicenseAgreements.SingleAsync();
        Assert.Equal(new byte[] { 1, 2, 3 }, saved.PdfBytes);
        Assert.True(await _db.AuditLogEntries.AnyAsync(e => e.Action == LicenseAgreementService.AttachPdfAction));
    }

    [Fact]
    public async Task OnlyAnAdministrator_GeneratesAnAgreement()
    {
        await ActivateAsync();
        TestActor.Accountant();
        await Assert.ThrowsAnyAsync<Exception>(() => Service().GenerateAndSaveAsync(_companyId, "عميل", "311111111111113"));
    }

    [PostgresFact]
    public async Task OnPostgres_TheRowCannotBeEditedOrDeleted_OnlyThePdfOnce()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("p7_licence_agreement");
        var db = scope.Db;
        var company = await new DatabaseInitializer(db).InitializeAsync("شركة", "311111111111113", BusinessProfile.General);
        var activation = LicenseActivation.Record(Guid.NewGuid().ToString("N"), "device", "standard", 1, null, false,
            new string('b', 64), DateTime.UtcNow, DateTime.UtcNow);
        db.LicenseActivations.Add(activation);
        await db.SaveChangesAsync();
        var service = new LicenseAgreementService(db, () => Vendor);
        var agreement = await service.GenerateAndSaveAsync(company.Id, "عميل", "311111111111113");

        await service.AttachPdfAsync(agreement.Id, null, "/agreements/a.pdf");

        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE license_agreements SET \"SecondPartyName\" = 'x' WHERE \"Id\" = {0}", agreement.Id));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE license_agreements SET \"PdfPath\" = '/other.pdf' WHERE \"Id\" = {0}", agreement.Id));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "DELETE FROM license_agreements WHERE \"Id\" = {0}", agreement.Id));
    }
}
