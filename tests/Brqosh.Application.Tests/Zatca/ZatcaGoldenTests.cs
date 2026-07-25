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
