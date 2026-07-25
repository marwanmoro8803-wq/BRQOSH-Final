using System.Xml.Linq;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

public class ZatcaGoldenTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public ZatcaGoldenTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }

    [Fact]
    public async Task FirstInvoice_IcvIsOne_AndXmlContainsProfileId()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
        settings.Update(true, ZatcaEnvironment.Simulation, "", "", "", "");
        await _db.SaveChangesAsync();

        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 8));
        invoice.AddLine("صنف", 1m, 100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync();
        Assert.Equal(1, doc.Icv);
        Assert.True(File.Exists(doc.XmlFilePath));

        var xml = XDocument.Load(doc.XmlFilePath);
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        Assert.Equal("reporting:1.0", xml.Root?.Element(cbc + "ProfileID")?.Value);
        Assert.Equal("388", xml.Descendants(cbc + "InvoiceTypeCode").First().Value);
    }

    /// <summary>
    /// ع-Z2 (CEEF-202) — cac:TaxSubtotal إلزاميّ لدى الهيئة (BR-CO-18/KSA) وكان غائباً تماماً،
    /// فتُرفض الفاتورة في الفحص رغم صحة التوقيع.
    /// </summary>
    [Fact]
    public async Task Ubl_TaxTotal_ContainsMandatoryTaxSubtotal()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700004", BusinessProfile.Retail);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
        settings.Update(true, ZatcaEnvironment.Simulation, "", "", "", "");
        await _db.SaveChangesAsync();

        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 8));
        invoice.AddLine("صنف", 2m, 100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync();
        var xml = XDocument.Load(doc.XmlFilePath);
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

        // TaxSubtotal على مستوى المستند (لا داخل سطور الفاتورة)
        var docTaxTotal = xml.Root!.Elements(cac + "TaxTotal").First();
        var subtotal = docTaxTotal.Element(cac + "TaxSubtotal");
        Assert.NotNull(subtotal);

        Assert.Equal("200.00", subtotal!.Element(cbc + "TaxableAmount")?.Value);
        Assert.Equal("30.00", subtotal.Element(cbc + "TaxAmount")?.Value);

        var category = subtotal.Element(cac + "TaxCategory");
        Assert.NotNull(category);
        Assert.Equal("S", category!.Element(cbc + "ID")?.Value);
        Assert.Equal("15.00", category.Element(cbc + "Percent")?.Value);
        Assert.Equal("VAT", category.Element(cac + "TaxScheme")?.Element(cbc + "ID")?.Value);

        // إجمالي ضريبة المستند = ضريبة الوعاء (اتساق حسابي يفحصه مدقق الهيئة)
        Assert.Equal(docTaxTotal.Element(cbc + "TaxAmount")?.Value, subtotal.Element(cbc + "TaxAmount")?.Value);
    }

    /// <summary>
    /// (CEEF-207) فاتورة بفئتين ضريبيتين تُنتج وعاءين في XML، ونسبة كل وعاء تطابق مبلغه.
    /// كان XML يعلن 15.00 لكل سطر ويصنّف الصفريّ "O" — تناقض يرفضه مدقّق الهيئة.
    /// </summary>
    [Fact]
    public async Task Ubl_MixedVatCategories_EmitOneSubtotalEach()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700005", BusinessProfile.Retail);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
        settings.Update(true, ZatcaEnvironment.Simulation, "", "", "", "");
        await _db.SaveChangesAsync();

        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 8));
        invoice.AddLine("بيع محلي", 1m, 1000m);
        invoice.AddLine("تصدير", 1m, 500m, vatCategory: Brqosh.Domain.Tax.VatCategory.ZeroRated);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync();
        var xml = XDocument.Load(doc.XmlFilePath);
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

        var taxTotal = xml.Root!.Elements(cac + "TaxTotal").First();
        var subtotals = taxTotal.Elements(cac + "TaxSubtotal").ToList();
        Assert.Equal(2, subtotals.Count);

        var standard = subtotals.Single(s =>
            s.Element(cac + "TaxCategory")!.Element(cbc + "ID")!.Value == "S");
        Assert.Equal("1000.00", standard.Element(cbc + "TaxableAmount")?.Value);
        Assert.Equal("150.00", standard.Element(cbc + "TaxAmount")?.Value);
        Assert.Equal("15.00", standard.Element(cac + "TaxCategory")!.Element(cbc + "Percent")?.Value);

        var zero = subtotals.Single(s =>
            s.Element(cac + "TaxCategory")!.Element(cbc + "ID")!.Value == "Z");
        Assert.Equal("500.00", zero.Element(cbc + "TaxableAmount")?.Value);
        Assert.Equal("0.00", zero.Element(cbc + "TaxAmount")?.Value);
        // النسبة المعلنة تطابق المبلغ — لا صفر مصنّف بنسبة 15.
        Assert.Equal("0.00", zero.Element(cac + "TaxCategory")!.Element(cbc + "Percent")?.Value);

        // اتساق حسابيّ: إجمالي ضريبة المستند = مجموع الأوعية.
        Assert.Equal("150.00", taxTotal.Element(cbc + "TaxAmount")?.Value);
    }

    [Fact]
    public async Task SecondInvoice_IcvIncrementsSequentially()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
        settings.Update(true, ZatcaEnvironment.Simulation, "", "", "", "");
        await _db.SaveChangesAsync();

        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        for (var i = 0; i < 2; i++)
        {
            var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 8));
            invoice.AddLine($"صنف{i}", 1m, 50m + i);
            await new SalesInvoicePostingService(_db).PostAsync(invoice);
        }

        var icvs = await _db.ZatcaEInvoiceDocuments.OrderBy(d => d.Icv).Select(d => d.Icv).ToListAsync();
        Assert.Equal([1, 2], icvs);
    }
}
