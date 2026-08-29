using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Construction;

public class ProgressBillingVatTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public ProgressBillingVatTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Million_riyal_progress_billing_vat_journal_vat_return_and_zatca()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة مقاولات", "300000000000001", BusinessProfile.Construction);

        // (CEEF-229 · م2-١٣أ) هويةُ بائعٍ مكتملة — بلا هذا تُصدر منشأةٌ ناقصةٌ مستنداتٍ ترفضها الهيئة.
        await ZatcaReadySeller.CompleteAsync(_db, company.Id);
        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == company.Id);
        settings.Update(true, ZatcaEnvironment.Simulation, "",
            ZatcaReadySeller.Certificate.CertPath, ZatcaReadySeller.Certificate.KeyPath, "");
        await _db.SaveChangesAsync();

        var customer = Customer.Create(company.Id, "عميل مشروع", "300000000000002").WithStandardIdentity();
        _db.Customers.Add(customer);

        var project = ConstructionProject.Create(company.Id, "PRJ-VAT", "برج", customer.Id,
            contractValue: 5_000_000m, retentionPercent: 10m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);
        await _db.SaveChangesAsync();

        var billing = ProgressBilling.CreateDraft(
            company.Id, project.Id, customer.Id, new DateOnly(2026, 3, 1),
            "مستخلص مليون", 1_000_000m, 10m);

        await new ProgressBillingPostingService(_db).PostAsync(billing);

        Assert.Equal(1_000_000m, billing.TaxableAmount);
        Assert.Equal(150_000m, billing.VatAmount);
        Assert.Equal(1_150_000m, billing.TotalAmount);
        Assert.Equal(ProgressBillingStatus.Posted, billing.Status);

        var entry = await _db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == billing.JournalEntryId);
        Assert.Equal(entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit));

        var vatReturn = await new VatReturnQuery(_db).ExecuteAsync(
            company.Id, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        Assert.Equal(150_000m, vatReturn.ProgressBillingsVat);
        Assert.Equal(150_000m, vatReturn.NetOutputVat);

        var zatca = await _db.ZatcaEInvoiceDocuments.SingleAsync(d =>
            d.DocumentType == ZatcaDocumentType.ProgressBilling && d.SourceDocumentId == billing.Id);
        Assert.Equal(billing.BillingNumber, zatca.InvoiceNumber);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
