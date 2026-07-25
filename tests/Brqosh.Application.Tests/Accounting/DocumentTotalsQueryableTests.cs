using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

public class DocumentTotalsQueryableTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _supplierId;

    public DocumentTotalsQueryableTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميل");
        _db.Customers.Add(customer);
        var supplier = Supplier.Create(_companyId, "مورد");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _supplierId = supplier.Id;
    }

    [Fact]
    public async Task SumGrandTotalAsync_translates_stored_columns()
    {
        await SetupAsync();

        var first = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5));
        first.AddLine("بضاعة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(first);

        var second = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 6));
        second.AddLine("بضاعة", 2m, 500m);
        await new SalesInvoicePostingService(_db).PostAsync(second);

        var monthStart = new DateOnly(2026, 7, 1);
        var total = await _db.SalesInvoices
            .Where(i => i.CompanyId == _companyId
                        && i.Status == SalesInvoiceStatus.Posted
                        && i.InvoiceDate >= monthStart)
            .SumGrandTotalAsync();

        Assert.Equal(2300m, total); // 1150 + 1150
    }

    [Fact]
    public async Task SumGrandTotalAsync_works_for_purchase_orders()
    {
        await SetupAsync();

        var first = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 5));
        first.AddLine("مواد", 1m, 1000m);
        first.Approve("PO-001");
        _db.PurchaseOrders.Add(first);

        var second = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 6));
        second.AddLine("مواد", 2m, 500m);
        second.Approve("PO-002");
        _db.PurchaseOrders.Add(second);
        await _db.SaveChangesAsync();

        var total = await _db.PurchaseOrders
            .Where(o => o.CompanyId == _companyId)
            .SumGrandTotalAsync();

        Assert.Equal(2300m, total);
    }

    [Fact]
    public async Task SumGrandTotalAsync_works_for_posted_credit_notes()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 1));
        invoice.AddLine("بضاعة", 1m, 2000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var first = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 5), "مرتجع");
        first.AddLine("بضاعة", 1m, 200m);
        await new CreditNotePostingService(_db).PostAsync(first);

        var second = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 6), "خصم");
        second.AddLine("خصم", 1m, 100m);
        await new CreditNotePostingService(_db).PostAsync(second);

        var total = await _db.CreditNotes
            .Where(n => n.CompanyId == _companyId && n.Status == CreditNoteStatus.Posted)
            .SumGrandTotalAsync();

        Assert.Equal(345m, total); // 230 + 115
    }

    [Fact]
    public void RecalculateTotals_persists_subtotal_and_vat()
    {
        var invoice = SalesInvoice.CreateDraft(1, 2, new DateOnly(2026, 7, 1));
        invoice.AddLine("صنف", 2m, 100m);

        Assert.Equal(200m, invoice.Subtotal);
        Assert.Equal(30m, invoice.VatAmount);
        Assert.Equal(230m, invoice.GrandTotal());
    }
}
