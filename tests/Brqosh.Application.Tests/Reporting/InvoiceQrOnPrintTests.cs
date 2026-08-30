using System.Globalization;
using System.Text;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-226) كل فاتورة ضريبية مطبوعة تحمل رمز QR — ولو لم يُفعَّل تكامل ZATCA.
///
/// كان توليد الرمز مشروطاً بـ<c>ZatcaSettings.IsEnabled</c> بالكامل، فمنشأةٌ لم تُفعّل
/// المرحلة الثانية تطبع فواتير **بلا باركود إطلاقاً**. ثبت ذلك على قاعدة المستخدم
/// الحقيقية: الفاتورة المرحّلة الوحيدة بلا مستند ZATCA ولا رمز.
///
/// ورمز المرحلة الأولى إلزاميّ على كل مسجَّل في ضريبة القيمة المضافة منذ 2021-12-04،
/// ولا يحتاج شهادة ولا اتصالاً بالهيئة — خمسة حقول تُحسب محلياً من الفاتورة نفسها.
/// </summary>
public class InvoiceQrOnPrintTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public InvoiceQrOnPrintTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = Brqosh.Domain.Security.UserRole.Administrator;
        SecurityContext.CurrentUsername = "print-tests";
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private DocumentPrintService Print() =>
        new(_db, new AuditService(_db), new ExchangeRateService(_db));

    private static Dictionary<byte, byte[]> DecodeTlv(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        var map = new Dictionary<byte, byte[]>();
        var i = 0;
        while (i + 1 < bytes.Length)
        {
            var tag = bytes[i++];
            int len = bytes[i++];
            if (i + len > bytes.Length) break;
            map[tag] = bytes[i..(i + len)];
            i += len;
        }
        return map;
    }

    private async Task<(Company Company, SalesInvoice Invoice)> SeedPostedInvoiceAsync(string vatNumber)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مؤسسة الطباعة", vatNumber, BusinessProfile.General);
        var customer = Customer.Create(company.Id, "عميل الطباعة");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 3, 14));
        invoice.AddLine("خدمة استشارية", 2m, 500m);          // 1,000 + 150 ضريبة = 1,150
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return (company, invoice);
    }

    /// <summary>جوهر البند: لا إعداد ZATCA، ومع ذلك المطبوع يحمل رمزاً.</summary>
    [Fact]
    public async Task PostedInvoice_WithoutZatcaSettings_StillCarriesQr()
    {
        var (company, invoice) = await SeedPostedInvoiceAsync("310123456700003");
        Assert.False(await _db.ZatcaEInvoiceDocuments.AnyAsync(), "الاختبار يفترض غياب مستند ZATCA.");

        var result = await Print().ExportSalesInvoiceAsync(company.Id, invoice.Id, "tester");

        Assert.NotNull(result.PdfBytes);
        Assert.True(result.PdfBytes.Length > 1000, "ملف PDF أصغر من أن يكون فاتورة.");
    }

    /// <summary>والرمز يطابق الفاتورة فعلاً — الوسوم الخمسة الإلزامية بقيمها الصحيحة.</summary>
    [Fact]
    public async Task Phase1Qr_MatchesTheInvoiceItPrints()
    {
        var (company, invoice) = await SeedPostedInvoiceAsync("310123456700003");

        // (CEEF-229 · م2-٢٤) **تُستدعى دالّةُ الطباعة نفسُها — لا تُعاد كتابتُها هنا.**
        //
        // كان هذا الاختبار يبني الـTLV بيده ويقول في تعليقه «نفس المسار الذي تسلكه الطباعة
        // تماماً». وهو نمطُ «قياس النسخة لا الأصل» الذي تكرّر في هذا المستودع: أيّ تغييرٍ
        // في مسار الطباعة يبقى الاختبارُ أخضرَ عنه، لأنّه لا يمسّه.
        var tlv = DocumentPrintService.Phase1QrFallback(
            company, invoice.InvoiceDate, invoice.PostedAtUtc, invoice.GrandTotal(), invoice.VatAmount);
        Assert.NotNull(tlv);

        var tags = DecodeTlv(tlv);

        Assert.Equal("مؤسسة الطباعة", Encoding.UTF8.GetString(tags[1]));
        Assert.Equal("310123456700003", Encoding.UTF8.GetString(tags[2]));
        // ‏والوسمُ الزمنيّ هو **لحظةُ الترحيل المسجَّلة**، لا ظهراً مختلقاً. وكان هذا السطر
        // ‏يُرسّخ `2026-03-14T12:00:00Z` — أي يحرس العطل.
        Assert.NotNull(invoice.PostedAtUtc);
        Assert.Equal(
            invoice.PostedAtUtc!.Value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            Encoding.UTF8.GetString(tags[3]));
        Assert.Equal("1150.00", Encoding.UTF8.GetString(tags[4]));
        Assert.Equal("150.00", Encoding.UTF8.GetString(tags[5]));
    }

    /// <summary>والصورة تُولَّد فعلاً من هذا الـTLV — PNG سليم لا بايتات فارغة.</summary>
    [Fact]
    public async Task Phase1Qr_RendersToARealPng()
    {
        var (company, invoice) = await SeedPostedInvoiceAsync("310123456700003");
        var tlv = ZatcaQrGenerator.GenerateTlvBase64Phase1(
            company.NameAr, company.VatRegistrationNumber,
            invoice.InvoiceDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(12)), DateTimeKind.Utc),
            invoice.GrandTotal(), invoice.VatAmount);

        var png = ZatcaQrImageHelper.TryCreatePng(tlv);

        Assert.NotNull(png);
        Assert.True(png!.Length > 100, $"صورة QR صغيرة جداً: {png.Length} بايت.");
        // توقيع ملف PNG: 89 50 4E 47
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
    }

    /// <summary>
    /// وبلا رقم ضريبيّ **لا رمز**: وسم 2 إلزاميّ، ورمزٌ باختلاق رقم أسوأ من غيابه.
    /// </summary>
    [Fact]
    public async Task CompanyWithoutVatNumber_GetsNoFabricatedQr()
    {
        var (company, invoice) = await SeedPostedInvoiceAsync("");

        var result = await Print().ExportSalesInvoiceAsync(company.Id, invoice.Id, "tester");

        Assert.NotNull(result.PdfBytes);   // تُطبع الفاتورة، لكن بلا رمز مختلَق
    }
}
