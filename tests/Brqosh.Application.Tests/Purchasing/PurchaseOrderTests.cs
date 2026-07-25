using Brqosh.Domain.Accounting;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

public class PurchaseOrderTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public PurchaseOrderTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Purchase_order_converts_to_invoice()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة", "300000000000001",
            Brqosh.Domain.Modules.BusinessProfile.Manufacturing);

        var supplier = Supplier.Create(company.Id, "مورد", "300000000000003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var order = PurchaseOrder.CreateDraft(company.Id, supplier.Id, new DateOnly(2026, 3, 1));
        order.AddLine("مواد خام", 10m, 100m);

        await new PurchaseOrderService(_db).ApproveAsync(order);

        var invoice = await new PurchaseOrderService(_db).CreateInvoiceFromOrderAsync(
            company.Id, order.Id, "SUP-INV-99", new DateOnly(2026, 3, 5), vatDeductible: true);

        Assert.NotNull(invoice.InvoiceNumber);
        var updated = await _db.PurchaseOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(PurchaseOrderStatus.Fulfilled, updated.Status);
    }

    [Fact]
    public async Task Cancel_approved_order_succeeds()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة", "300000000000001",
            Brqosh.Domain.Modules.BusinessProfile.Manufacturing);

        var supplier = Supplier.Create(company.Id, "مورد", "300000000000003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var order = PurchaseOrder.CreateDraft(company.Id, supplier.Id, new DateOnly(2026, 3, 1));
        order.AddLine("مواد خام", 10m, 100m);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        await new PurchaseOrderService(_db).CancelAsync(company.Id, order.Id);

        var updated = await _db.PurchaseOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(PurchaseOrderStatus.Cancelled, updated.Status);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
