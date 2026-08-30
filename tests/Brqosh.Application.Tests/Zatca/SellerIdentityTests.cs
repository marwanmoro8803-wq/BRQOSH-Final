using System.Xml.Linq;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (CEEF-229 · Z-10) بيانات البائع النظامية — الشقّ الأول: المنشأة.
///
/// <para><b>أطول عطلٍ في عنقود ZATCA، ووحدَه بنيويّ:</b> بقيّة الأعطال كانت منطقاً في غير محلّه
/// — سطرٌ في ترتيبٍ خطأ، أو بصمةٌ على صورةٍ خطأ، أو حقلٌ لا يُمرَّر. أمّا هذا فـ<c>Company</c>
/// <b>لا تملك</b> سجلاً تجارياً ولا عنواناً وطنياً مفكَّكاً: عنوانها حقلٌ نصّيّ حرّ واحد يُطبع
/// على الترويسة. ومعيار الهيئة يفرض <c>cac:PostalAddress</c> بحقولٍ منفصلة — فلا سبيل إلى
/// توليدها من نصٍّ حرّ مهما بلغت براعة المولّد. أي أن العطل في <b>نموذج البيانات</b>.</para>
///
/// <para><b>الشقّ المُنجَز هنا يكفي الفاتورة المبسّطة</b> — وهي فاتورة المنشأة التجارية المحلية
/// التي تبيع للأفراد. حقول المشتري للفاتورة القياسية B2B شقٌّ ثانٍ مؤجَّل بوعي.</para>
/// </summary>
public class SellerIdentityTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;

    private static readonly NationalAddress Riyadh = NationalAddress.Create(
        buildingNumber: "1234", streetName: "طريق الملك فهد", districtName: "العليا",
        cityName: "الرياض", postalZone: "12345", countrySubentity: "منطقة الرياض",
        additionalNumber: "5678");

    public SellerIdentityTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("شركة تجارية", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;
    }

    private async Task<Company> CompanyAsync() =>
        await _db.Companies.SingleAsync(c => c.Id == _companyId);

    // ── النطاق: الاكتمال سؤالٌ يُجاب عنه في مكان واحد ───────────────────────

    /// <summary>منشأةٌ جديدة ينقصها كل شيء — والرسائل تقول <b>ما</b> ينقص لا «غير جاهزة».</summary>
    [Fact]
    public async Task NewCompany_ReportsEveryMissingRegulatoryField()
    {
        var gaps = (await CompanyAsync()).RegulatoryGaps();

        Assert.False((await CompanyAsync()).IsReadyForEInvoicing);
        Assert.Contains(gaps, g => g.Contains("السجل التجاري"));
        Assert.Contains(gaps, g => g.Contains("رقم المبنى"));
        Assert.Contains(gaps, g => g.Contains("الرمز البريدي"));
    }

    /// <summary>وبعد الاستكمال لا ينقص شيء.</summary>
    [Fact]
    public async Task CompleteCompany_HasNoGaps()
    {
        var company = await CompanyAsync();
        company.UpdateRegulatoryIdentity("منشأة الاختبار للتجارة", "1010123456", Riyadh);
        await _db.SaveChangesAsync();

        Assert.Empty(company.RegulatoryGaps());
        Assert.True(company.IsReadyForEInvoicing);
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> عنوانٌ ناقص حقلاً واحداً يبقى ناقصاً. لولا هذا لكان
    /// <c>IsComplete</c> الذي يعيد <c>true</c> دائماً يُمرّر الاختبار السابق.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task AddressMissingOneField_IsStillIncomplete(string? district)
    {
        var company = await CompanyAsync();
        company.UpdateRegulatoryIdentity("منشأة", "1010123456",
            NationalAddress.Create("1234", "طريق الملك فهد", district, "الرياض", "12345", "منطقة الرياض"));

        Assert.False(company.IsReadyForEInvoicing);
        Assert.Contains(company.RegulatoryGaps(), g => g.Contains("الحي"));
    }

    /// <summary>
    /// التحقق من طول رقم المبنى والرمز البريديّ <b>تنبيهيّ لا مانع</b> — القيمة تُحفَظ
    /// ويُبلَّغ عنها. حدُّ الثقة مُصرَّح به: لم يُراجَع النصّ النظاميّ.
    /// </summary>
    [Fact]
    public void UnusualLengths_AreReportedButNotRejected()
    {
        var address = NationalAddress.Create("12", "شارع", "حي", "مدينة", "123", "منطقة");

        Assert.True(address.IsComplete);   // مكتمل — كل الحقول موجودة
        Assert.Contains(address.Problems, p => p.Contains("أربع خانات"));
        Assert.Contains(address.Problems, p => p.Contains("خمس خانات"));
    }

    // ── القاعدة: الحقول تُحفَظ وتُقرأ ───────────────────────────────────────

    /// <summary>العنوان كائنُ قيمةٍ مملوك — يُسطَّح في جدول المنشآت ويعود كما حُفظ.</summary>
    [Fact]
    public async Task RegulatoryIdentity_RoundTripsThroughTheDatabase()
    {
        var company = await CompanyAsync();
        company.UpdateRegulatoryIdentity("منشأة الاختبار للتجارة", "1010123456", Riyadh);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var reloaded = await CompanyAsync();

        Assert.Equal("منشأة الاختبار للتجارة", reloaded.RegistrationName);
        Assert.Equal("1010123456", reloaded.CommercialRegistrationNumber);
        Assert.Equal("1234", reloaded.NationalAddress.BuildingNumber);
        Assert.Equal("العليا", reloaded.NationalAddress.DistrictName);
        Assert.Equal("SA", reloaded.NationalAddress.CountryCode);
    }

    /// <summary>
    /// <b>حارس انحدار — عطلٌ أحدثه هذا البند نفسه وأمسكته اختبارات قائمة.</b>
    ///
    /// <para>العنوان كائنُ قيمةٍ مملوك، والقيمة الافتراضية كانت نسخةً ساكنة <b>مشتركة</b> بين كل
    /// المنشآت. فمتى أُنشئت منشأةٌ ثانية ظنّ متتبِّع EF أن صفّها هو صفّ الأولى وحاول تغيير
    /// <c>CompanyId</c> — وهو جزء من المفتاح — فسقط إنشاء المنشأة الثانية كلّه: التوحيد
    /// والتهيئة والمجموعات. لا يظهر البتّة بمنشأةٍ واحدة، ولهذا يُحرَس صراحةً.</para>
    /// </summary>
    [Fact]
    public async Task SecondCompany_GetsItsOwnAddressRow()
    {
        var second = await new DatabaseInitializer(_db)
            .CreateAdditionalCompanyAsync("منشأة ثانية", "310999888700003", BusinessProfile.General);

        var first = await CompanyAsync();
        first.UpdateRegulatoryIdentity("الأولى", "1010000001", Riyadh);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // عنوان الأولى لا يتسرّب إلى الثانية، والثانية تبقى فارغة لا محذوفة.
        var reloadedSecond = await _db.Companies.SingleAsync(c => c.Id == second.Id);
        Assert.Equal("", reloadedSecond.NationalAddress.CityName);
        Assert.Equal("الرياض", (await CompanyAsync()).NationalAddress.CityName);
    }

    /// <summary>
    /// <b>حارسٌ وُلد من عطلٍ لم تمسكه أيّ اختبارة — أمسكه مسبار الهجرات وحده.</b>
    ///
    /// <para>ترقيع المخطّط يُعلن نوع كل عمود لقواعد البيانات القائمة، والنموذج يُعلنه للقواعد
    /// الجديدة. فإن اختلفا في قابلية العدم انقسم النظام قسمَين: تركيبٌ جديد يعمل وتركيبٌ مُرقَّع
    /// يسقط — أو العكس. وقع الاثنان هنا فعلاً: <c>NULL</c> على عمود عنوانٍ أسقط تجسيد المنشآت
    /// على قاعدةٍ حيّة، ثم <c>NOT NULL</c> على <c>RegistrationName</c> منع إدراج أيّ منشأة على
    /// قاعدةٍ نظيفة.</para>
    ///
    /// <para>ولا تكشفهما اختباراتنا لأنها كلّها على SQLite تُنشأ بـ <c>EnsureCreated</c> من
    /// النموذج — فمسار الترقيع لا يُمسّ أصلاً. فبدل محاكاة الترقيع نقيس <b>بيانات النموذج
    /// الفعلية</b> ونطابقها بجدول الترقيع عموداً عموداً.</para>
    /// </summary>
    [Fact]
    public void MigrationNullability_MatchesTheModelExactly()
    {
        var company = _db.Model.FindEntityType(typeof(Company))!;
        var address = company.GetNavigations()
            .Single(n => n.Name == nameof(Company.NationalAddress)).TargetEntityType;

        var modelNullability = company.GetProperties().Concat(address.GetProperties())
            .Where(p => p.GetColumnName() is { } c
                        && BrqoshSchemaUpdater.RegulatoryIdentityColumns.Any(r => r.Column == c))
            .ToDictionary(p => p.GetColumnName()!, p => p.IsNullable);

        Assert.Equal(BrqoshSchemaUpdater.RegulatoryIdentityColumns.Length, modelNullability.Count);

        foreach (var (column, _, nullable) in BrqoshSchemaUpdater.RegulatoryIdentityColumns)
        {
            Assert.True(modelNullability[column] == nullable,
                $"العمود {column}: النموذج يقول " +
                $"{(modelNullability[column] ? "يقبل العدم" : "لا يقبل العدم")} " +
                $"والترقيع يقول {(nullable ? "يقبل العدم" : "لا يقبل العدم")}.");
        }
    }

    // ── المولّد: طرف البائع كاملاً ──────────────────────────────────────────

    private static XDocument BuildInvoice(ZatcaUblInvoiceBuilder.SellerIdentity? seller)
    {
        var bytes = ZatcaUblInvoiceBuilder.BuildXml(new ZatcaUblInvoiceBuilder.InvoiceData(
            InvoiceNumber: "INV-1", Uuid: Guid.NewGuid(), InvoiceDate: new DateOnly(2026, 3, 14), IssueTime: new TimeOnly(12, 0, 0),
            SellerName: "شركة تجارية", SellerVat: "310123456700099",
            CustomerName: "", CustomerVat: null,
            Subtotal: 1000m, VatAmount: 150m, GrandTotal: 1150m,
            Lines: [new SalesInvoiceLine("صنف", 1m, 1000m)],
            Icv: "1", PreviousInvoiceHash: "cHJldg==", QrTlvBase64: "AQE=",
            Seller: seller));

        using var stream = new MemoryStream(bytes);
        return XDocument.Load(stream);
    }

    /// <summary><b>القياس الأساسيّ.</b> طرف البائع يخرج كاملاً ويجتاز المُصدِّق بلا مخالفة.</summary>
    [Fact]
    public void CompleteSeller_ProducesAConformingSupplierParty()
    {
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var doc = BuildInvoice(new ZatcaUblInvoiceBuilder.SellerIdentity(
            "منشأة الاختبار للتجارة", "1010123456", Riyadh));

        var violations = UblStructureValidator.Validate(doc);
        Assert.True(violations.Count == 0, string.Join("\n", violations));

        var party = doc.Root!.Element(cac + "AccountingSupplierParty")!.Element(cac + "Party")!;
        Assert.Equal("1010123456", party.Element(cac + "PartyIdentification")!.Element(cbc + "ID")!.Value);
        Assert.Equal("CRN", party.Element(cac + "PartyIdentification")!
            .Element(cbc + "ID")!.Attribute("schemeID")!.Value);
        Assert.Equal("منشأة الاختبار للتجارة",
            party.Element(cac + "PartyLegalEntity")!.Element(cbc + "RegistrationName")!.Value);

        var address = party.Element(cac + "PostalAddress")!;
        Assert.Equal("1234", address.Element(cbc + "BuildingNumber")!.Value);
        Assert.Equal("طريق الملك فهد", address.Element(cbc + "StreetName")!.Value);
        Assert.Equal("العليا", address.Element(cbc + "CitySubdivisionName")!.Value);
        Assert.Equal("12345", address.Element(cbc + "PostalZone")!.Value);
        Assert.Equal("SA", address.Element(cac + "Country")!.Element(cbc + "IdentificationCode")!.Value);
    }

    /// <summary>
    /// <b>وهذا هو إثبات العضّ الدائم:</b> بائعٌ ناقص يُرفض من المُصدِّق باسم كل حقلٍ ناقص —
    /// وهي الحالة التي كان عليها كل مستند يُولَّد قبل هذا البند.
    /// </summary>
    [Fact]
    public void SellerWithoutIdentity_IsRejectedByTheValidator()
    {
        var violations = UblStructureValidator.Validate(BuildInvoice(seller: null));

        Assert.Contains(violations, v => v.Contains("السجل التجاري"));
        Assert.Contains(violations, v => v.Contains("الاسم المسجَّل"));
        Assert.Contains(violations, v => v.Contains("العنوان الوطني"));
    }

    /// <summary>وعنوانٌ ناقص لا يُكتب بحقولٍ فارغة — الغيابُ أوضح من الفراغ.</summary>
    [Fact]
    public void IncompleteAddress_IsOmittedNotEmitted()
    {
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

        var doc = BuildInvoice(new ZatcaUblInvoiceBuilder.SellerIdentity(
            "منشأة", "1010123456",
            NationalAddress.Create("1234", "شارع", "", "الرياض", "12345", "منطقة الرياض")));

        var party = doc.Root!.Element(cac + "AccountingSupplierParty")!.Element(cac + "Party")!;
        Assert.Null(party.Element(cac + "PostalAddress"));
    }

    // ── بوابة التفعيل ───────────────────────────────────────────────────────

    /// <summary>
    /// منشأةٌ ناقصة البيانات لا تُعدّ «جاهزة للإرسال» — وكان الفحص يقتصر على الرقم الضريبيّ،
    /// فتظهر جاهزةً بينما يُرفض كل مستند تُصدره.
    /// </summary>
    [Fact]
    public async Task ReadinessCheck_RequiresTheFullSellerIdentity()
    {
        // الفحص يقصر إن كانت ZATCA معطّلة، فنُفعّلها لنبلغ فحص بيانات البائع.
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(isEnabled: true, ZatcaEnvironment.Simulation,
            "https://example.invalid/zatca", "", "", "");
        await _db.SaveChangesAsync();

        var status = await new ZatcaConfigurationValidator(_db).ValidateAsync(_companyId);

        Assert.False(status.ReadyForSubmission);
        Assert.Contains(status.Warnings, w => w.Contains("السجل التجاري"));
        Assert.Contains(status.Warnings, w => w.Contains("العنوان") || w.Contains("رقم المبنى"));
    }

    // ── المطبوعة ────────────────────────────────────────────────────────────

    /// <summary>
    /// الترويسة تُفضّل العنوان الوطنيّ متى اكتمل — فلا يُدخله المالك مرّتين، ولا تختلف
    /// الورقة عن XML المرسَل.
    /// </summary>
    [Fact]
    public void PrintLine_RendersTheNationalAddress()
    {
        var line = Riyadh.ToPrintLine();

        Assert.Contains("مبنى 1234", line);
        Assert.Contains("العليا", line);
        Assert.Contains("ر.ب 12345", line);
    }

    /// <summary>وعنوانٌ ناقص لا يُنتج سطراً بفواصل فارغة.</summary>
    [Fact]
    public void PrintLine_SkipsEmptyParts()
    {
        var line = NationalAddress.Create("", "شارع الملك", "", "جدة", "", "").ToPrintLine();

        Assert.Equal("شارع الملك · جدة", line);
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
