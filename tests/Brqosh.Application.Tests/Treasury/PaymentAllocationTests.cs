using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

public class PaymentAllocationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public PaymentAllocationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ReceiptAllocation_ReducesOpenBalance_AndSumMustMatch()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700005", BusinessProfile.Retail);
        var customer = Customer.Create(company.Id, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var inv1 = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, 1));
        inv1.AddLine("بضاعة", 10m, 100m); // 1150
        await new SalesInvoicePostingService(_db).PostAsync(inv1);

        var inv2 = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, 2));
        inv2.AddLine("بضاعة", 5m, 100m); // 575
        await new SalesInvoicePostingService(_db).PostAsync(inv2);

        var alloc = new PaymentAllocationService(_db);
        var open = await alloc.OpenSalesDocumentsAsync(company.Id, customer.Id);
        Assert.Equal(2, open.Count);
        Assert.Equal(1150m, open.Single(o => o.DocumentId == inv1.Id).RemainingAmount);

        var voucher = ReceiptVoucher.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, 5),
            1150m, ReceiptMethod.Cash, "سداد فاتورة 1");

        var bad = await Assert.ThrowsAsync<Brqosh.Domain.Accounting.AccountingException>(() =>
            new ReceiptVoucherPostingService(_db).PostAsync(voucher, [
                (PaymentAllocationDocumentType.SalesInvoice, inv1.Id, inv1.InvoiceNumber!, 500m),
            ]));
        Assert.Contains("يساوي", bad.Message);

        var voucher2 = ReceiptVoucher.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, 5),
            1150m, ReceiptMethod.Cash, "سداد فاتورة 1");
        await new ReceiptVoucherPostingService(_db).PostAsync(voucher2, [
            (PaymentAllocationDocumentType.SalesInvoice, inv1.Id, inv1.InvoiceNumber!, 1150m),
        ]);

        open = await alloc.OpenSalesDocumentsAsync(company.Id, customer.Id);
        Assert.Single(open);
        Assert.Equal(inv2.Id, open[0].DocumentId);
        Assert.Equal(575m, open[0].RemainingAmount);
    }

    [Fact]
    public async Task PaymentAllocation_AgainstPurchaseInvoice()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700006", BusinessProfile.Retail);
        var supplier = Supplier.Create(company.Id, "مورد", "312233344400003");
        var product = Product.Create(company.Id, "P1", "صنف", "قطعة", ProductType.Stockable);
        _db.Suppliers.Add(supplier);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var pi = PurchaseInvoice.CreateDraft(company.Id, supplier.Id, new DateOnly(2026, 3, 1), "PI-1", true);
        pi.AddLine("صنف", 10m, 50m, product.Id); // 500 + 75 = 575
        await new PurchaseInvoicePostingService(_db).PostAsync(pi);

        var open = await new PaymentAllocationService(_db).OpenPurchaseDocumentsAsync(company.Id, supplier.Id);
        Assert.Single(open);
        Assert.Equal(575m, open[0].RemainingAmount);

        var voucher = PaymentVoucher.CreateDraft(company.Id, supplier.Id, new DateOnly(2026, 3, 5),
            575m, ReceiptMethod.Cash, "سداد");
        await new PaymentVoucherPostingService(_db).PostAsync(voucher, [
            (PaymentAllocationDocumentType.PurchaseInvoice, pi.Id, pi.InvoiceNumber!, 575m),
        ]);

        open = await new PaymentAllocationService(_db).OpenPurchaseDocumentsAsync(company.Id, supplier.Id);
        Assert.Empty(open);
    }
}
