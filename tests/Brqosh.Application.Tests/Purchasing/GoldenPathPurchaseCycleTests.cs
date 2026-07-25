using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>Golden Path: أمر شراء → استلام تلقائي → فاتورة مشتريات → سداد مورد.</summary>
public class GoldenPathPurchaseCycleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;
    private long _productId;

    public GoldenPathPurchaseCycleTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupStockableAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة GP شراء", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var supplier = Supplier.Create(_companyId, "مورد GP", "312222222200003");
        _db.Suppliers.Add(supplier);
        var product = Product.Create(_companyId, "P-GP", "صنف GP", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;
        _productId = product.Id;
    }

    [Fact]
    public async Task GoldenPath_Po_AutoGrn_Invoice_Payment_ClearsPayable()
    {
        await SetupStockableAsync();

        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 9));
        order.AddLine("مواد بناء", 10m, 100m, _productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        var invoice = await new PurchaseOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, "SUP-GP-001", new DateOnly(2026, 7, 10), vatDeductible: true);

        Assert.Equal(PurchaseInvoiceStatus.Posted, invoice.Status);
        Assert.NotNull(invoice.JournalEntryId);
        Assert.Equal(PurchaseOrderStatus.Fulfilled, order.Status);

        var grn = await _db.GoodsReceipts.SingleAsync(g =>
            g.CompanyId == _companyId && g.PurchaseOrderId == order.Id);
        Assert.Equal(GoodsReceiptStatus.Posted, grn.Status);

        var onHand = await new InventoryService(_db).GetCompanyOnHandAsync(_companyId, _productId);
        Assert.Equal(10m, onHand);

        var apBefore = await PayableBalanceAsync();
        Assert.Equal(invoice.GrandTotal(), apBefore);

        var voucher = PaymentVoucher.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 7, 11),
            invoice.GrandTotal(), ReceiptMethod.Bank, "سداد GP", "TRF-GP");
        await new PaymentVoucherPostingService(_db).PostAsync(voucher);

        var apAfter = await PayableBalanceAsync();
        Assert.Equal(0m, apAfter);

        var balances = await new SupplierBalanceQuery(_db).AllBalancesAsync(_companyId);
        Assert.Equal(0m, balances.GetValueOrDefault(_supplierId));
    }

    [Fact]
    public async Task CreateInvoiceFromOrder_WithMultipleDefaultWarehouses_DoesNotThrow()
    {
        await SetupStockableAsync();

        var second = Warehouse.Create(_companyId, "WH-002", "مستودع ثانٍ", isDefault: true);
        _db.Warehouses.Add(second);
        await _db.SaveChangesAsync();

        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 9));
        order.AddLine("مواد", 5m, 50m, _productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        var invoice = await new PurchaseOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, "SUP-MULTI-WH", new DateOnly(2026, 7, 10), vatDeductible: true);

        Assert.Equal(PurchaseInvoiceStatus.Posted, invoice.Status);
        Assert.True(await _db.GoodsReceipts.AnyAsync(g => g.PurchaseOrderId == order.Id));
    }

    private async Task<decimal> PayableBalanceAsync() =>
        await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsPayable)
            .SumAsync(l => l.Credit - l.Debit);
}
