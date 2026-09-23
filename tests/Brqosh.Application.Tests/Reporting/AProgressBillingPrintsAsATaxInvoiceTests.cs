using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-٥٦ · <c>ui-ux-i18n-printing#1</c>) <b>المستخلصُ يُطبع فاتورةً ضريبيّة — وكان يُطبع
/// «مستخلص أعمال» بلا هويّة المشتري.</b>
///
/// <para>‏المستخلصُ توريدٌ خاضع، <b>ومستندُه عند الهيئة فاتورةٌ</b>: `TryGenerateForProgressBillingAsync`
/// يُولّده تخليصاً لعميلٍ مسجَّلٍ وإبلاغاً لغيره. <b>ثمّ الورقةُ التي يأخذها المشتري</b> عنوانُها «مستخلص
/// أعمال»، <b>ولا رقمَ ضريبيّاً للمشتري ولا عنوان</b> — وهما على الفاتورة منذ CEEF-227 — <b>ورأسُ الضريبة
/// «15%» نصّاً</b> لا من النسبة التي حُسبت بها.</para>
///
/// <para>‏<b>والقياسُ على الطلب قبل الرسم</b> — مذهبُ ح-٤٢: محتوى PDF لا يُقرأ في الاختبار؛ <b>وحارسٌ
/// بنيويٌّ</b> أنّ الرسمَ يأخذ من الدوالّ المقيسة لا من نصٍّ بجانبها.</para>
/// </summary>
public class AProgressBillingPrintsAsATaxInvoiceTests : IDisposable
{
    private const string CustomerVat = "311111111100003";

    private readonly BrqoshDbContext _db;
    private long _companyId;

    public AProgressBillingPrintsAsATaxInvoiceTests()
    {
        TestActor.Accountant();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Database.CloseConnection();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>‏<b>القياسُ الحاكم:</b> مستخلصُ عميلٍ مسجَّلٍ فاتورةٌ ضريبيّة — وهو ما يُخلَّص عند الهيئة.</summary>
    [Fact]
    public async Task ARegisteredBuyer_GetsATaxInvoice()
    {
        var data = await RequestAsync(Customer.Create(_companyId, "عميل مسجّل", CustomerVat, address: "الرياض"));

        Assert.Equal(PrintDocumentKind.StandardSalesInvoice, data.Kind);
        Assert.Equal("فاتورة ضريبية — مستخلص أعمال", ProgressBillingPdfGenerator.Title(data));
    }

    /// <summary>‏وعميلٌ بلا رقمٍ ضريبيٍّ يُبلَّغ مستخلصُه — <b>فورقتُه فاتورةٌ ضريبيّةٌ مبسّطة</b>.</summary>
    [Fact]
    public async Task AnUnregisteredBuyer_GetsASimplifiedTaxInvoice()
    {
        var data = await RequestAsync(Customer.Create(_companyId, "عميل فرد"));

        Assert.Equal(PrintDocumentKind.SimplifiedSalesInvoice, data.Kind);
        Assert.Equal("فاتورة ضريبية مبسّطة — مستخلص أعمال", ProgressBillingPdfGenerator.Title(data));
    }

    /// <summary>
    /// ‏<b>وما أُرسل للهيئة يحكم</b> — نوعُ المستند المُولَّد، لا حالُ العميل اليوم (قد يُضاف رقمُه أو يُحذف
    /// بعد الإرسال)؛ <b>وبلا مستندٍ فقاعدةُ المولِّد نفسُها</b>: `Customer.SupportsStandardTaxInvoice`.
    /// </summary>
    [Theory]
    [InlineData(ZatcaTransmissionMode.Clearance, false, PrintDocumentKind.StandardSalesInvoice)]
    [InlineData(ZatcaTransmissionMode.Reporting, true, PrintDocumentKind.SimplifiedSalesInvoice)]
    [InlineData(null, true, PrintDocumentKind.StandardSalesInvoice)]
    [InlineData(null, false, PrintDocumentKind.SimplifiedSalesInvoice)]
    public void TheSubmittedModeDecides_ThenTheGeneratorsRule(
        ZatcaTransmissionMode? mode, bool supportsStandard, PrintDocumentKind expected) =>
        Assert.Equal(expected, DocumentPrintService.BillingKind(mode, supportsStandard));

    /// <summary>‏<b>شاهدُ ضبط</b> — المستخلصُ فاتورةٌ أو مبسّطةٌ لا غير: «إشعار دائن — مستخلص أعمال» لا يُطبع.</summary>
    [Fact]
    public async Task ATitleOtherThanAnInvoice_IsRefused()
    {
        var data = await RequestAsync(Customer.Create(_companyId, "عميل مسجّل", CustomerVat, address: "الرياض"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProgressBillingPdfGenerator.Title(data with { Kind = PrintDocumentKind.CreditNote }));
    }

    /// <summary>‏<b>وهويّةُ المشتري على الورقة</b> — رقمُه الضريبيُّ وعنوانُه، كما على الفاتورة منذ CEEF-227.</summary>
    [Fact]
    public async Task TheBuyersIdentity_IsPrinted()
    {
        var data = await RequestAsync(Customer.Create(_companyId, "عميل مسجّل", CustomerVat, address: "الرياض"));

        Assert.Equal(CustomerVat, data.PartyVatNumber);
        Assert.Equal("الرياض", data.PartyAddress);

        var rows = ProgressBillingPdfGenerator.PartyRows(data);
        Assert.Contains(("الرقم الضريبي", PrintText.Code(CustomerVat)), rows);
        Assert.Contains(("العنوان", "الرياض"), rows);
    }

    /// <summary>‏<b>والنسبةُ نسبةُ الكيان</b> — التي حُسبت بها الضريبة، لا نصٌّ بجانبها.</summary>
    [Fact]
    public async Task TheRate_IsTheRateTheVatWasComputedWith()
    {
        var data = await RequestAsync(Customer.Create(_companyId, "عميل مسجّل", CustomerVat, address: "الرياض"));

        Assert.Equal(ProgressBilling.SaudiVatRate, data.VatRate);
        Assert.Equal(Math.Round(data.CurrentWorkAmount * data.VatRate, 2, MidpointRounding.AwayFromZero), data.VatAmount);
        Assert.Equal($"ضريبة {PrintText.Ltr("15%")}", ProgressBillingPdfGenerator.VatLabel(data.VatRate));
    }

    /// <summary>
    /// ‏<b>والرأسُ يتبع النسبة</b> — فنسبةُ الكيان إن تغيّرت لا تبقى الورقةُ على «15%». <b>وهذا وحدَه</b> يرى
    /// النصَّ الثابت: النسبةُ اليومَ ١٥٪ فيتساويان صدفةً.
    /// </summary>
    [Fact]
    public void TheLabel_FollowsTheRate() =>
        Assert.Equal($"ضريبة {PrintText.Ltr("5%")}", ProgressBillingPdfGenerator.VatLabel(0.05m));

    /// <summary>
    /// ‏<b>والرسمُ يأخذ من المقيس</b> — وإلّا فالدوالُّ صحيحةٌ والورقةُ «مستخلص أعمال» و«15%» كما كانت:
    /// ‏<c>Generate</c> يستدعي <c>Title</c> و<c>PartyRows</c> و<c>VatLabel</c>، ولا يكتب نصَّها بيده.
    /// </summary>
    [Fact]
    public void TheRenderer_PrintsWhatIsMeasured()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "Brqosh.Infrastructure", "Reporting", "ProgressBillingPdfGenerator.cs");
        var source = File.ReadAllText(path).Replace("\r\n", "\n");
        var start = source.IndexOf("public static byte[] Generate(", StringComparison.Ordinal);
        Assert.True(start >= 0, "Generate غير موجود — القياسُ أعمى.");
        var body = source[start..];
        body = body[..body.IndexOf("\n    }\n", StringComparison.Ordinal)];

        Assert.Contains("Title(data)", body, StringComparison.Ordinal);
        Assert.Contains("PartyRows(data)", body, StringComparison.Ordinal);
        Assert.Contains("VatLabel(data.VatRate)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"مستخلص أعمال\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("15%", body, StringComparison.Ordinal);
    }

    private async Task<ProgressBillingPdfGenerator.BillingPrintData> RequestAsync(Customer customer)
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة مقاولات", "300000000000001", BusinessProfile.Construction);
        _companyId = company.Id;

        var buyer = Customer.Create(_companyId, customer.NameAr, customer.VatRegistrationNumber,
            address: customer.Address);
        _db.Customers.Add(buyer);
        await _db.SaveChangesAsync();

        var project = ConstructionProject.Create(_companyId, "PRJ-H56", "برج", buyer.Id,
            contractValue: 1_000_000m, retentionPercent: 10m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);
        await _db.SaveChangesAsync();

        var billing = ProgressBilling.CreateDraft(
            _companyId, project.Id, buyer.Id, new DateOnly(2026, 3, 1), "مستخلص أول", 100_000m, 10m);
        await new ProgressBillingPostingService(_db).PostAsync(billing);

        return await Print().ProgressBillingRequestAsync(_companyId, billing.Id);
    }

    private DocumentPrintService Print() =>
        new(_db, new AuditService(_db), new ExchangeRateService(_db));

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }
}
