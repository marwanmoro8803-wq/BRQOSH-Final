using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

public class FifoCostingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public FifoCostingTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
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
    public async Task Fifo_TwoReceipts_IssueConsumesOldestLayer()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        company.SetCostingMethod(InventoryCostingMethod.Fifo);
        await _db.SaveChangesAsync();

        var wh = await _db.Warehouses.SingleAsync(w => w.CompanyId == company.Id && w.IsDefault);
        var product = Product.Create(company.Id, "P-FIFO", "صنف FIFO", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        var customer = Customer.Create(company.Id, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var inv = new InventoryService(_db);
        await inv.PostDocumentMovementAsync(company.Id, wh.Id, StockMovementType.Receipt,
            new DateOnly(2026, 1, 1), "دفعة 1", "R1",
            [(product.Id, 10m, 100m)]);
        await inv.PostDocumentMovementAsync(company.Id, wh.Id, StockMovementType.Receipt,
            new DateOnly(2026, 1, 2), "دفعة 2", "R2",
            [(product.Id, 10m, 200m)]);
        await _db.SaveChangesAsync();

        var layers = await _db.InventoryCostLayers.Where(l => l.ProductId == product.Id).ToListAsync();
        Assert.Equal(2, layers.Count);
        Assert.Equal(20m, layers.Sum(l => l.RemainingQuantity));

        // بيع 10 وحدات — يجب أن تكون تكلفة COGS = 1000 (الطبقة الأولى) وليس 1500 (WAC)
        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 1, 10));
        invoice.AddLine("صنف FIFO", 10m, 300m, product.Id);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var entry = await _db.JournalEntries.Include(e => e.Lines)
            .SingleAsync(e => e.Id == invoice.JournalEntryId);
        var cogs = entry.Lines.Single(l => l.AccountCode == Brqosh.Domain.Accounting.SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold);
        Assert.Equal(1000m, cogs.Debit);

        product = await _db.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(200m, product.WeightedAverageCost); // الطبقة المتبقية فقط @200

        layers = await _db.InventoryCostLayers.Where(l => l.ProductId == product.Id).ToListAsync();
        Assert.Equal(10m, layers.Sum(l => l.RemainingQuantity));
        Assert.Equal(0m, layers.Single(l => l.UnitCost == 100m).RemainingQuantity);
        Assert.Equal(10m, layers.Single(l => l.UnitCost == 200m).RemainingQuantity);
    }

    [Fact]
    public async Task Wac_TwoReceipts_IssueUsesAverage()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700004", BusinessProfile.Retail);
        company.SetCostingMethod(InventoryCostingMethod.WeightedAverage);
        await _db.SaveChangesAsync();

        var wh = await _db.Warehouses.SingleAsync(w => w.CompanyId == company.Id && w.IsDefault);
        var product = Product.Create(company.Id, "P-WAC", "صنف", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        var customer = Customer.Create(company.Id, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var inv = new InventoryService(_db);
        await inv.PostDocumentMovementAsync(company.Id, wh.Id, StockMovementType.Receipt,
            new DateOnly(2026, 1, 1), "دفعة 1", "R1",
            [(product.Id, 10m, 100m)]);
        await _db.SaveChangesAsync();
        await inv.PostDocumentMovementAsync(company.Id, wh.Id, StockMovementType.Receipt,
            new DateOnly(2026, 1, 2), "دفعة 2", "R2",
            [(product.Id, 10m, 200m)]);
        await _db.SaveChangesAsync();

        product = await _db.Products.SingleAsync(p => p.Id == product.Id);
        Assert.Equal(150m, product.WeightedAverageCost);

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 1, 10));
        invoice.AddLine("صنف", 10m, 300m, product.Id);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var entry = await _db.JournalEntries.Include(e => e.Lines)
            .SingleAsync(e => e.Id == invoice.JournalEntryId);
        var cogs = entry.Lines.Single(l => l.AccountCode == Brqosh.Domain.Accounting.SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold);
        Assert.Equal(1500m, cogs.Debit);
    }
}
