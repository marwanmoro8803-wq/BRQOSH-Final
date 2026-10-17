using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Assets;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٦١ · <c>treasury-payroll-assets#10</c>) <b>الاستبعادُ يُعكس — وبيعُه المُفوتَرُ يُلغى بإشعارٍ دائنٍ يُشير إليه.</b>
///
/// <para><b>ما كان:</b> استبعادٌ خطأٌ لا يُصحَّح، والأصلُ خارج السجلّ أبداً؛ وبيعٌ فُوتِر لا سبيلَ إلى إلغائه لدى الهيئة.</para>
/// </summary>
public class ADisposalCanBeReversedTests : IDisposable
{
    private static readonly DateOnly Disposed = new(2026, 1, 15);
    private static readonly DateOnly Reversed = new(2026, 1, 20);

    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _customerId;
    private long _assetAccountId;
    private long _accumAccountId;

    public ADisposalCanBeReversedTests()
    {
        TestActor.Administrator();
        _workDirectory = Path.Combine(Path.GetTempPath(), "brqosh-h161", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        try { Directory.Delete(_workDirectory, recursive: true); }
        catch (IOException) { /* ‏ملفٌّ مؤقّتٌ ممسوكٌ من التشغيل — لا يعني فشلَ القياس. */ }
        catch (UnauthorizedAccessException) { /* ‏كسابقه. */ }
        GC.SuppressFinalize(this);
    }

    /// <summary><b>خردةٌ بلا مقابل</b>: القيدُ يُعكس فيعود المجمّعُ والتكلفة، ويعود الأصلُ قائماً ويُهلَك.</summary>
    [Fact]
    public async Task AScrapDisposal_IsReversed_AndTheAssetReturnsToTheRegister()
    {
        await SetupAsync(zatcaEnabled: false);
        var asset = await DisposeAsync(proceeds: 0m, buyer: null);

        await ReverseAsync(asset);

        Assert.Equal(0m, await BalanceAsync(_assetAccountId));
        Assert.Equal(0m, await BalanceAsync(_accumAccountId));
        _db.ChangeTracker.Clear();
        var back = await _db.FixedAssets.SingleAsync(a => a.Id == asset.Id);
        Assert.True(back.IsActive);
        Assert.Null(back.DisposalJournalEntryId);
        Assert.Equal(1, await new DepreciationService(_db).RunMonthlyAsync(_companyId, new DateOnly(2026, 2, 28), "admin"));
    }

    /// <summary>
    /// <b>القياسُ الحاكم: بيعٌ فُوتِر يُلغى بإشعارٍ دائنٍ (381) مرجعُه فاتورةُ البيع</b> — وضريبةُ المخرجات تعود صفراً.
    /// </summary>
    [Fact]
    public async Task AnInvoicedSale_IsCancelledByACreditNoteThatReferencesItsInvoice()
    {
        await SetupAsync(zatcaEnabled: true);
        var asset = await DisposeAsync(proceeds: 50_000m, buyer: _customerId);
        var invoiceNumber = (await _db.FixedAssets.AsNoTracking().SingleAsync(a => a.Id == asset.Id)).DisposalInvoiceNumber!;

        await ReverseAsync(asset);

        var note = await _db.ZatcaEInvoiceDocuments.SingleAsync(d =>
            d.CompanyId == _companyId && d.DocumentType == ZatcaDocumentType.DisposalCancel && d.SourceDocumentId == asset.Id);
        var xml = File.ReadAllText(!string.IsNullOrEmpty(note.SignedXmlFilePath) ? note.SignedXmlFilePath! : note.XmlFilePath);
        Assert.Contains(">381<", xml, StringComparison.Ordinal);
        Assert.Contains("BillingReference", xml, StringComparison.Ordinal);
        Assert.Contains(invoiceNumber, xml, StringComparison.Ordinal);
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.VatOutput));
    }

    /// <summary><b>والفوترةُ المعطَّلةُ تمنع عكسَ بيعٍ فُوتِر</b> — لا يُلغى توريدٌ في الدفتر وهو قائمٌ لدى الهيئة.</summary>
    [Fact]
    public async Task AnInvoicedSale_WithEInvoicingNowOff_IsRefused_AndNothingChanges()
    {
        await SetupAsync(zatcaEnabled: true);
        var asset = await DisposeAsync(proceeds: 50_000m, buyer: _customerId);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: false, settings.Environment, settings.ApiBaseUrl, settings.CertificateFilePath,
            settings.PrivateKeyFilePath, "");
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() => ReverseAsync(asset));

        Assert.Contains("معطَّلة", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False((await _db.FixedAssets.SingleAsync(a => a.Id == asset.Id)).IsActive);
        Assert.False(await _db.ZatcaEInvoiceDocuments.AnyAsync(d => d.DocumentType == ZatcaDocumentType.DisposalCancel));
    }

    [Fact]
    public async Task AnAssetThatWasNotDisposed_IsRefused()
    {
        await SetupAsync(zatcaEnabled: false);
        var asset = FixedAsset.Create(_companyId, "FA-9", "مكتب", new DateOnly(2024, 1, 1), 10_000m, 0m, 60,
            _assetAccountId, _accumAccountId);
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<AccountingException>(() => ReverseAsync(asset));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private Task ReverseAsync(FixedAsset asset) =>
        new FixedAssetDisposalReversalService(_db).ReverseDisposalAsync(_companyId, asset.Id, Reversed, "admin");

    private async Task<FixedAsset> DisposeAsync(decimal proceeds, long? buyer)
    {
        var asset = FixedAsset.Create(_companyId, "FA-1", "سيارة", new DateOnly(2024, 1, 1),
            acquisitionCost: 120_000m, salvageValue: 0m, usefulLifeMonths: 120,
            glAssetAccountId: _assetAccountId, glDepreciationAccountId: _accumAccountId);
        for (var i = 0; i < 24; i++) asset.ApplyDepreciation();
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();
        await new FixedAssetDisposalPostingService(_db).PostDisposalAsync(
            asset, proceeds, Disposed, "admin", VatCategory.Standard, buyer);
        return asset;
    }

    private Task<decimal> BalanceAsync(long accountId) => BalanceWhereAsync(l => l.AccountId == accountId);

    private Task<decimal> BalanceAsync(string accountCode) => BalanceWhereAsync(l => l.AccountCode == accountCode);

    private async Task<decimal> BalanceWhereAsync(System.Linq.Expressions.Expression<Func<JournalLine, bool>> which) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(which)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Debit - l.Credit);

    private async Task SetupAsync(bool zatcaEnabled)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700099", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
        ZatcaReadySeller.Complete(company);

        var customer = Customer.Create(_companyId, "مشتري الأصل", "311111111100003");
        customer.UpdateRegulatoryIdentity(
            "مؤسسة المشتري التجارية",
            Brqosh.Domain.Companies.NationalAddress.Create(
                buildingNumber: "4321", streetName: "طريق العليا", districtName: "الملز",
                cityName: "الرياض", postalZone: "54321", countrySubentity: "منطقة الرياض",
                additionalNumber: "8765"));
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        _assetAccountId = (await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1203")).Id;
        _accumAccountId = (await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1207")).Id;

        if (zatcaEnabled)
        {
            var (certPath, keyPath) = ZatcaSignatureVerifier.CreateSigningCertificate(_workDirectory);
            var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
            settings.Update(isEnabled: true, ZatcaEnvironment.Simulation, "", certPath, keyPath, "");
            await _db.SaveChangesAsync();
        }
    }
}
