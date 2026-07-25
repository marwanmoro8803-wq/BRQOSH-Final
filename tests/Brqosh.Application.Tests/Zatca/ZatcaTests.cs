using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Zatca;

public class ZatcaTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public ZatcaTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }

    [Fact]
    public void QrTlv_MatchesZatcaFormat()
    {
        var qr = ZatcaQrGenerator.GenerateTlvBase64(
            "شركة BRQOSH", "310123456700003",
            new DateTime(2026, 7, 6, 9, 0, 0, DateTimeKind.Utc),
            1150m, 150m);
        Assert.False(string.IsNullOrWhiteSpace(qr));
        var bytes = Convert.FromBase64String(qr);
        Assert.True(bytes.Length > 20);
        Assert.Equal(1, bytes[0]); // tag 1 seller
    }

    [Fact]
    public void ZeroHash_IsDeterministic() =>
        Assert.Equal(ZatcaHashHelper.ZeroHash(), ZatcaHashHelper.ZeroHash());

    [Fact]
    public async Task GenerateFromPostedInvoice_CreatesDocumentWithXml()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);

        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
        settings.Update(true, ZatcaEnvironment.Simulation, "", "", "", "");
        await _db.SaveChangesAsync();

        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 6));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync();
        Assert.Equal(ZatcaDocumentType.SalesInvoice, doc.DocumentType);
        Assert.Equal(ZatcaTransmissionMode.Clearance, doc.TransmissionMode);
        Assert.True(File.Exists(doc.XmlFilePath));
        Assert.False(string.IsNullOrEmpty(doc.QrTlvBase64));
        Assert.Equal(1, doc.Icv);
    }

    [Fact]
    public async Task AutoSubmit_AfterB2bInvoicePost_MarksCleared()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
        settings.Update(true, ZatcaEnvironment.Simulation, "", "", "", "");
        await _db.SaveChangesAsync();
        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 6));
        invoice.AddLine("صنف", 1m, 100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var doc = await _db.ZatcaEInvoiceDocuments.SingleAsync();
        Assert.Equal(ZatcaComplianceStatus.Cleared, doc.ComplianceStatus);
    }

    [Fact]
    public async Task CancelPostedInvoice_GeneratesZatcaCancellationCreditNote()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
        settings.Update(true, ZatcaEnvironment.Simulation, "", "", "", "");
        await _db.SaveChangesAsync();

        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 6));
        invoice.AddLine("صنف", 1m, 100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        await new SalesInvoiceCancellationService(_db).CancelAsync(
            company.Id, invoice.Id, new DateOnly(2026, 7, 7), "admin");

        var docs = await _db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == company.Id)
            .OrderBy(d => d.Id)
            .ToListAsync();
        Assert.Equal(2, docs.Count);
        Assert.Equal(ZatcaDocumentType.SalesInvoice, docs[0].DocumentType);
        Assert.Equal(ZatcaDocumentType.CreditNote, docs[1].DocumentType);
        Assert.Equal(invoice.Id, docs[1].SourceDocumentId);
        Assert.StartsWith("CN-CANCEL-", docs[1].InvoiceNumber);
    }

    [Fact]
    public async Task CancelPostedCreditNote_GeneratesZatcaDebitNote()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
        settings.Update(true, ZatcaEnvironment.Simulation, "", "", "", "");
        await _db.SaveChangesAsync();

        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 5));
        invoice.AddLine("صنف", 1m, 100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var note = CreditNote.CreateDraft(company.Id, customer.Id, invoice.Id, new DateOnly(2026, 7, 6), "مرتجع");
        note.AddLine("صنف", 1m, 50m);
        await new CreditNotePostingService(_db).PostAsync(note);

        await new CreditNoteCancellationService(_db).CancelAsync(
            company.Id, note.Id, new DateOnly(2026, 7, 7), "admin");

        var docs = await _db.ZatcaEInvoiceDocuments
            .Where(d => d.CompanyId == company.Id)
            .OrderBy(d => d.Id)
            .ToListAsync();
        Assert.Contains(docs, d => d.DocumentType == ZatcaDocumentType.SalesInvoice);
        Assert.Contains(docs, d => d.DocumentType == ZatcaDocumentType.CreditNote);
        var debit = docs.Single(d => d.DocumentType == ZatcaDocumentType.DebitNote);
        Assert.StartsWith("DN-CANCEL-", debit.InvoiceNumber);
    }
}
