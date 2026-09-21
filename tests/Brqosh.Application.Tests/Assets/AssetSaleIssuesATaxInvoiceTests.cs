using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Assets;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Assets;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;

namespace Brqosh.Application.Tests.Assets;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٦) <b>بيعُ أصلٍ ثابت كان يُقيّد ضريبةً بلا فاتورةٍ ولا مشترٍ.</b>
///
/// <para><b>وبيعُ أصلٍ من أصول النشاط توريدٌ خاضعٌ نظاماً.</b> والاستبعادُ كان يُقيّد
/// التزامَ ضريبةٍ على <c>2106</c> — <b>والضريبةُ تبلغ الإقرارَ منذ CEEF-232 · ض-١</b> —
/// <b>ولا فاتورةَ ضريبةٍ خلفَه ولا مستندَ إلكترونيّ ولا طرفٌ ثانٍ يُعرف</b>.</para>
///
/// <para><b>فالنقصُ ليس في الإفصاح بل في المستند</b>: توريدٌ خاضعٌ <b>لا تعرفه الهيئةُ
/// أبداً</b> — مخالفةُ الطور الثاني بعينها. <b>ولا يشكو منه شيء</b>: الميزانُ متوازن،
/// والإقرارُ يحمل الضريبة، <b>والفحصُ وحدَه يكشفه</b>.</para>
/// </summary>
public class AssetSaleIssuesATaxInvoiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly string _workDirectory;
    private long _companyId;
    private long _customerId;
    private long _assetAccountId;
    private long _accumAccountId;

    public AssetSaleIssuesATaxInvoiceTests()
    {
        TestActor.Administrator();
        _workDirectory = Path.Combine(Path.GetTempPath(), "brqosh-h16", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
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

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> بيعُ أصلٍ خاضعٍ ⟵ <b>مستندٌ إلكترونيٌّ واحدٌ يُولَّد</b>،
    /// رقمُه مسجَّلٌ على الأصل ومبلغُه هو المقابل.
    /// </summary>
    [Fact]
    public async Task ATaxableAssetSale_IssuesAnEInvoice()
    {
        await SetupAsync(zatcaEnabled: true);
        var asset = await SellAsync(proceeds: 80_000m, buyerCustomerId: _customerId);

        var document = await DocumentAsync(asset.Id);

        Assert.NotNull(document);
        Assert.Equal(asset.DisposalInvoiceNumber, document!.InvoiceNumber);

        // ‏**والمقيسُ ما استقرّ على القرص** — وهو ما يصل الهيئةَ فعلاً، لا صفٌّ في جدول.
        var (net, tax, gross) = Totals(document);
        Assert.Equal(80_000m, net);
        Assert.Equal(12_000m, tax);
        Assert.Equal(92_000m, gross);
    }

    /// <summary>
    /// <b>والبيعُ يُسجَّل على الأصل نفسِه</b> — المشتري والمقابلُ والفئة. <b>ولم يكن
    /// شيءٌ من ذلك محفوظاً في حقل</b>: سؤالُ المراجع «لمن بيع وبكم؟» جوابُه سردُ قيد.
    /// </summary>
    [Fact]
    public async Task TheSale_IsRecordedOnTheAssetItself()
    {
        await SetupAsync(zatcaEnabled: true);
        var asset = await SellAsync(proceeds: 80_000m, buyerCustomerId: _customerId);

        Assert.Equal(_customerId, asset.DisposalBuyerCustomerId);
        Assert.Equal(80_000m, asset.DisposalProceeds);
        Assert.Equal(VatCategory.Standard, asset.DisposalVatCategory);
        Assert.StartsWith("FAS-2026-", asset.DisposalInvoiceNumber, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ومشترٍ مُعرَّفٌ يستوجب فاتورةً قياسيّةً تُعتمد</b> (B2B) — <b>ومجهولٌ يُفوتَر
    /// مبسَّطةً تُبلَّغ</b> (B2C). وهي قاعدةُ النظام نفسُها في كلّ مستنداته.
    /// </summary>
    [Fact]
    public async Task AnIdentifiedBuyer_GetsAStandardInvoice_AnAnonymousOne_GetsASimplified()
    {
        await SetupAsync(zatcaEnabled: true);

        var sold = await SellAsync(proceeds: 50_000m, buyerCustomerId: _customerId, code: "FA-B2B");
        var scrapped = await SellAsync(proceeds: 3_000m, buyerCustomerId: null, code: "FA-B2C");

        Assert.Equal(ZatcaTransmissionMode.Clearance, (await DocumentAsync(sold.Id))!.TransmissionMode);
        Assert.Equal(ZatcaTransmissionMode.Reporting, (await DocumentAsync(scrapped.Id))!.TransmissionMode);
    }

    /// <summary>
    /// <b>ومشترٍ مجهولٌ يُنسب إلى «عميل نقدي»</b> — <b>وواحدٌ لا يتكاثر</b>: بيعتان بلا
    /// مشترٍ لا تُنشئان عميلَين بالمعنى نفسِه. والاسمُ والقاعدةُ من <c>PosSaleService</c>.
    /// </summary>
    [Fact]
    public async Task TwoAnonymousSales_ShareOneWalkInCustomer()
    {
        await SetupAsync(zatcaEnabled: true);
        await SellAsync(proceeds: 3_000m, buyerCustomerId: null, code: "FA-S1");
        await SellAsync(proceeds: 4_000m, buyerCustomerId: null, code: "FA-S2");

        Assert.Equal(1, await _db.Customers.CountAsync(c => c.CompanyId == _companyId && c.NameAr == "عميل نقدي"));
    }

    /// <summary>
    /// <b>وتاريخُ الفاتورة تاريخُ القيد لا تاريخُ اليوم</b> — وإلّا وقع المستندُ في فترةٍ
    /// ضريبيّةٍ غير فترة قيده، <b>فاختلف الإقرارُ عن الدفتر</b>.
    /// </summary>
    [Fact]
    public async Task TheInvoiceDate_IsThePostingDate_NotToday()
    {
        await SetupAsync(zatcaEnabled: true);
        var asset = await SellAsync(proceeds: 10_000m, buyerCustomerId: _customerId,
            disposalDate: new DateOnly(2026, 3, 9));

        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == asset.DisposalJournalEntryId);
        Assert.Equal(new DateOnly(2026, 3, 9), entry.EntryDate);
        Assert.Contains("2026-03-09", ReadXml(await DocumentAsync(asset.Id)), StringComparison.Ordinal);
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — خردةٌ بلا مقابلٍ لا تُفوتَر:</b> لا توريدَ حيث لا عوض.
    /// <b>وفوترتُها كانت ستُنشئ مستندَ ضريبةٍ بصفرٍ عن واقعةٍ لم تقع.</b>
    /// </summary>
    [Fact]
    public async Task AScrappedAsset_WithNoProceeds_IssuesNoInvoice()
    {
        await SetupAsync(zatcaEnabled: true);
        var asset = await SellAsync(proceeds: 0m, buyerCustomerId: null);

        Assert.Null(asset.DisposalInvoiceNumber);
        Assert.Null(await DocumentAsync(asset.Id));
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — منشأةٌ غير مسجَّلةٍ لا تُصدر فاتورةَ ضريبة:</b> وهي خارج
    /// تفويض الفوترة أصلاً. <b>والاستبعادُ يمرّ كما كان</b> — فلم يُغلق بابٌ سليم.
    /// </summary>
    [Fact]
    public async Task AnUnregisteredCompany_StillDisposes_ButIssuesNoInvoice()
    {
        await SetupAsync(zatcaEnabled: true);
        var company = await _db.Companies.SingleAsync(c => c.Id == _companyId);
        company.Update(company.NameAr, "");
        await _db.SaveChangesAsync();

        var asset = await SellAsync(proceeds: 80_000m, buyerCustomerId: _customerId);

        Assert.False(asset.IsActive);                       // ‏استُبعد فعلاً
        Assert.Null(asset.DisposalInvoiceNumber);
        Assert.Null(await DocumentAsync(asset.Id));
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثالث — والفوترةُ الإلكترونيّةُ غيرُ مفعَّلة:</b> البيعُ يُسجَّل على
    /// الأصل بفاتورةٍ مرقَّمة، <b>ولا مستندَ إلكترونيّ</b>. <b>ولولاه لتعطّل استبعادُ كلّ
    /// منشأةٍ لم تُفعّل ZATCA بعد</b> — وهي حالُ كلّ منشأةٍ يومَ تركيبها.
    /// </summary>
    [Fact]
    public async Task WithZatcaDisabled_TheSaleIsStillRecorded_WithoutAnEInvoice()
    {
        await SetupAsync(zatcaEnabled: false);
        var asset = await SellAsync(proceeds: 80_000m, buyerCustomerId: _customerId);

        Assert.NotNull(asset.DisposalInvoiceNumber);
        Assert.Null(await DocumentAsync(asset.Id));
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ رابع — الفئةُ الصفريّةُ تُفوتَر ولا تُعفى:</b> تصديرُ معدّةٍ توريدٌ
    /// <b>خاضعٌ بنسبة صفر</b>، <b>فله فاتورةٌ وإن كانت ضريبتُه صفراً</b>. <b>ولو رُبط
    /// إصدارُ الفاتورة بوجود ضريبةٍ موجبةٍ لسقط هذا التوريد من المستندات كلِّه.</b>
    /// </summary>
    [Fact]
    public async Task AZeroRatedExport_IsStillInvoiced()
    {
        await SetupAsync(zatcaEnabled: true);
        var asset = await SellAsync(proceeds: 80_000m, buyerCustomerId: _customerId,
            category: VatCategory.ZeroRated, exemptionCode: "VATEX-SA-32");

        var document = await DocumentAsync(asset.Id);

        Assert.NotNull(document);
        var (net, tax, gross) = Totals(document!);
        Assert.Equal(80_000m, net);
        Assert.Equal(0m, tax);
        Assert.Equal(80_000m, gross);
        Assert.Equal("VATEX-SA-32", asset.DisposalVatExemptionCode);
    }

    // ── الحرّاس ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>وفئةٌ غيرُ أساسيّةٍ بلا رمزِ سببٍ تُرفض قبل استهلاك العدّاد</b> — <c>BR-KSA-CL-06</c>.
    ///
    /// <para><b>وموضعُ الرفض مقصود:</b> المولّدُ يقفل العدّادَ التسلسليَّ ويستهلكه قبل أن
    /// يفحص، <b>وبصمةُ المستند المستهلِك تصير أساسَ ما بعده</b> — فرفضٌ متأخّرٌ يُلوّث
    /// السلسلةَ بأثرٍ رجعيّ. <b>والرفضُ عند الباب أرحم.</b></para>
    /// </summary>
    [Fact]
    public async Task AZeroRatedSale_WithoutAnExemptionCode_IsRefused()
    {
        await SetupAsync(zatcaEnabled: true);

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => SellAsync(proceeds: 80_000m, buyerCustomerId: _customerId,
                category: VatCategory.ZeroRated, exemptionCode: null));

        Assert.Contains("BT-121", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, await _db.ZatcaEInvoiceDocuments.CountAsync());
    }

    /// <summary>
    /// <b>والمشتري يأخذ فاتورتَه مطبوعةً</b> — ومستندٌ ضريبيٌّ لا يُطبع نصفُ مستند.
    ///
    /// <para><b>وزرُّ الطباعة في شاشة مستندات ZATCA كان سيرمي</b> على النوع السابع:
    /// مُوزِّعُ الطباعة يرفض ما لا يعرفه صراحةً — <b>وهو صوابٌ يُبقى</b>، فطباعةُ مستندٍ
    /// آخر أسوأُ من رسالةٍ صريحة. فأُدرج النوعُ فيه بدل أن يُترك يرمي.</para>
    /// </summary>
    [Fact]
    public async Task TheBuyer_CanBeHandedAPrintedTaxInvoice()
    {
        await SetupAsync(zatcaEnabled: true);
        var asset = await SellAsync(proceeds: 80_000m, buyerCustomerId: _customerId);
        var document = await DocumentAsync(asset.Id);

        var print = new DocumentPrintService(_db, new AuditService(_db), new ExchangeRateService(_db));
        var result = await print.ExportZatcaDocumentAsync(_companyId, document!.Id, "tester");

        Assert.NotEmpty(result.PdfBytes);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(result.PdfBytes, 0, 4));
        Assert.Contains(asset.DisposalInvoiceNumber!, result.SuggestedFileName, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ولا تُطبع فاتورةٌ لبيعٍ لم يقع</b> — خردةٌ بلا مقابلٍ لا مستندَ لها،
    /// <b>ورسالةٌ صريحةٌ أصدقُ من ورقةٍ بصفر</b>.
    /// </summary>
    [Fact]
    public async Task AScrappedAsset_HasNothingToPrint()
    {
        await SetupAsync(zatcaEnabled: true);
        var asset = await SellAsync(proceeds: 0m, buyerCustomerId: null);

        var print = new DocumentPrintService(_db, new AuditService(_db), new ExchangeRateService(_db));
        var error = await Assert.ThrowsAsync<AccountingException>(
            () => print.ExportAssetDisposalAsync(_companyId, asset.Id, "tester"));

        Assert.Contains("لا يوجد مستند يُطبع", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ولا يُسجَّل بيعان لأصلٍ واحد</b> — والرقمُ الثاني كان سيطمس الأوّلَ بلا أثر.
    /// </summary>
    [Fact]
    public void TheDomain_RefusesASecondSale()
    {
        var asset = FixedAsset.Create(
            1L, "FA-X", "أصل", new DateOnly(2024, 1, 1), 1_000m, 0m, 12, 1L, 2L);
        asset.RecordDisposalSale(null, "FAS-2026-000001", 500m, VatCategory.Standard, null);

        var error = Assert.Throws<AccountingException>(
            () => asset.RecordDisposalSale(null, "FAS-2026-000002", 600m, VatCategory.Standard, null));

        Assert.Contains("FAS-2026-000001", error.Message, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

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

    private async Task<FixedAsset> SellAsync(
        decimal proceeds, long? buyerCustomerId,
        VatCategory category = VatCategory.Standard, string? exemptionCode = null,
        string code = "FA-1", DateOnly? disposalDate = null)
    {
        var asset = FixedAsset.Create(
            _companyId, code, "سيارة", new DateOnly(2024, 1, 1),
            acquisitionCost: 120_000m, salvageValue: 0m, usefulLifeMonths: 120,
            glAssetAccountId: _assetAccountId, glDepreciationAccountId: _accumAccountId);
        for (var i = 0; i < 24; i++) asset.ApplyDepreciation();
        _db.FixedAssets.Add(asset);
        await _db.SaveChangesAsync();

        await new FixedAssetDisposalPostingService(_db).PostDisposalAsync(
            asset, proceeds, disposalDate ?? new DateOnly(2026, 1, 15), "admin",
            category, buyerCustomerId, exemptionCode);

        return asset;
    }

    private Task<ZatcaEInvoiceDocument?> DocumentAsync(long assetId) =>
        _db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
            d.CompanyId == _companyId &&
            d.DocumentType == ZatcaDocumentType.AssetDisposal &&
            d.SourceDocumentId == assetId);

    private static string ReadXml(ZatcaEInvoiceDocument? document)
    {
        Assert.NotNull(document);
        var path = !string.IsNullOrEmpty(document!.SignedXmlFilePath)
            ? document.SignedXmlFilePath!
            : document.XmlFilePath;
        return File.ReadAllText(path);
    }

    /// <summary>
    /// مبالغُ المستند <b>من الملفّ لا من الذاكرة</b> — و<c>ZatcaEInvoiceDocument</c> لا
    /// يحمل مبالغَ أصلاً. <b>فالمقيسُ هو ما يصل الهيئة.</b>
    /// </summary>
    private static (decimal Net, decimal Tax, decimal Gross) Totals(ZatcaEInvoiceDocument document)
    {
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var xml = XDocument.Load(new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(ReadXml(document))));
        var totals = xml.Root!.Element(cac + "LegalMonetaryTotal")!;

        decimal Value(XElement parent, XName name) =>
            decimal.Parse(parent.Element(name)!.Value, System.Globalization.CultureInfo.InvariantCulture);

        return (Value(totals, cbc + "LineExtensionAmount"),
                Value(xml.Root!.Elements(cac + "TaxTotal").First(), cbc + "TaxAmount"),
                Value(totals, cbc + "TaxInclusiveAmount"));
    }
}
