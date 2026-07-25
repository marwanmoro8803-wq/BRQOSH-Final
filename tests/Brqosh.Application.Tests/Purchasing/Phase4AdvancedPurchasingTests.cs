using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

public class Phase4AdvancedPurchasingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public Phase4AdvancedPurchasingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<(long CompanyId, long SupplierId, long ProductId, long WarehouseId)> SetupStockableAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);

        var supplier = Supplier.Create(company.Id, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        var product = Product.Create(company.Id, "P-GRN", "صنف مخزني", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var wh = await _db.Warehouses.SingleAsync(w => w.CompanyId == company.Id && w.IsDefault);
        return (company.Id, supplier.Id, product.Id, wh.Id);
    }

    [Fact]
    public async Task GoodsReceipt_PostsInventory_WithoutPayable()
    {
        var (companyId, supplierId, productId, warehouseId) = await SetupStockableAsync();

        var order = PurchaseOrder.CreateDraft(companyId, supplierId, new DateOnly(2026, 8, 1));
        order.AddLine("مواد", 10m, 50m, productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        var receipt = GoodsReceipt.CreateDraft(companyId, supplierId, order.Id, warehouseId, new DateOnly(2026, 8, 2));
        receipt.AddLine("مواد", 10m, 10m, 50m, productId);
        var posted = await new GoodsReceiptPostingService(_db).PostAsync(receipt);

        Assert.StartsWith("GRN-", posted.ReceiptNumber);
        Assert.NotNull(posted.StockMovementId);

        var onHand = await new InventoryService(_db).GetCompanyOnHandAsync(companyId, productId);
        Assert.Equal(10m, onHand);
        Assert.Equal(0, await _db.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task ThreeWayMatch_RejectsInvoice_WhenNoGrn()
    {
        var (companyId, supplierId, productId, _) = await SetupStockableAsync();

        var order = PurchaseOrder.CreateDraft(companyId, supplierId, new DateOnly(2026, 8, 1));
        order.AddLine("مواد", 5m, 100m, productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        var invoice = PurchaseInvoice.CreateDraft(companyId, supplierId, new DateOnly(2026, 8, 5), "SUP-3W-1", true);
        invoice.AddLine("مواد", 5m, 100m, productId);
        invoice.LinkPurchaseOrder(order.Id);

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new PurchaseInvoicePostingService(_db).PostAsync(invoice));
        Assert.Contains("المطابقة الثلاثية", ex.Message);
    }

    [Fact]
    public async Task ThreeWayMatch_WithGrn_PostsInvoice_WithoutDoubleInventory()
    {
        var (companyId, supplierId, productId, warehouseId) = await SetupStockableAsync();

        var order = PurchaseOrder.CreateDraft(companyId, supplierId, new DateOnly(2026, 8, 1));
        order.AddLine("مواد", 5m, 100m, productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        var receipt = GoodsReceipt.CreateDraft(companyId, supplierId, order.Id, warehouseId, new DateOnly(2026, 8, 2));
        receipt.AddLine("مواد", 5m, 5m, 100m, productId);
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);

        var movementsBefore = await _db.StockMovements.CountAsync();

        var invoice = PurchaseInvoice.CreateDraft(companyId, supplierId, new DateOnly(2026, 8, 5), "SUP-3W-2", true);
        invoice.AddLine("مواد", 5m, 100m, productId);
        invoice.LinkPurchaseOrder(order.Id);
        var posted = await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        Assert.NotNull(posted.JournalEntryId);
        var movementsAfter = await _db.StockMovements.CountAsync();
        Assert.Equal(movementsBefore, movementsAfter);

        var onHand = await new InventoryService(_db).GetCompanyOnHandAsync(companyId, productId);
        Assert.Equal(5m, onHand);
    }

    [Fact]
    public async Task DebitNote_ReducesPayable_OnPostedPurchaseInvoice()
    {
        var (companyId, supplierId, productId, _) = await SetupStockableAsync();

        var invoice = PurchaseInvoice.CreateDraft(companyId, supplierId, new DateOnly(2026, 8, 1), "SUP-DN-1", true);
        invoice.AddLine("مواد", 10m, 100m, productId);
        var postedInv = await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var note = DebitNote.CreateDraft(companyId, supplierId, postedInv.Id, new DateOnly(2026, 8, 10), "خصم");
        note.AddLine("خصم", 2m, 100m, productId);
        var postedNote = await new DebitNotePostingService(_db).PostAsync(note);

        Assert.StartsWith("DN-", postedNote.NoteNumber);
        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == postedNote.JournalEntryId);
        Assert.Equal(230m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsPayable).Debit);
    }

    [Fact]
    public async Task LandedCost_IncreasesFifoLayerCost()
    {
        var (companyId, _, productId, warehouseId) = await SetupStockableAsync();
        var company = await _db.Companies.SingleAsync(c => c.Id == companyId);
        company.SetCostingMethod(InventoryCostingMethod.Fifo);
        await _db.SaveChangesAsync();

        await new InventoryService(_db).PostDocumentMovementAsync(
            companyId, warehouseId, StockMovementType.Receipt,
            new DateOnly(2026, 8, 1), "استلام", "LC-1", [(productId, 10m, 100m)]);
        await _db.SaveChangesAsync();

        await new LandedCostAllocationService(_db).AllocateAsync(
            companyId, new DateOnly(2026, 8, 5), "شحن", 500m,
            [(productId, warehouseId, 10m)], "admin");

        var layer = await _db.InventoryCostLayers.SingleAsync(l => l.ProductId == productId);
        Assert.Equal(150m, layer.UnitCost);
    }

    [Fact]
    public async Task ProjectProfitability_ReturnsCostToCostMargin()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مقاولات", "300000000000010", BusinessProfile.Construction);
        var customer = Customer.Create(company.Id, "عميل");
        _db.Customers.Add(customer);
        var project = ConstructionProject.Create(company.Id, "PRJ-P4", "مشروع ربحية",
            customer.Id, contractValue: 1_000_000m, retentionPercent: 10m, estimatedTotalCost: 800_000m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);
        await _db.SaveChangesAsync();

        await new ProjectCostService(_db).RecordAsync(
            company.Id, project.Id, new DateOnly(2026, 8, 1), "تكاليف", 200_000m, "admin", postJournal: true);

        var rows = await new ProjectProfitabilityQuery(_db).ExecuteAsync(company.Id);
        var row = Assert.Single(rows);
        Assert.Equal("cost-to-cost", row.CompletionMethod);
        Assert.Equal(250_000m, row.RecognizedRevenueEstimate);
        Assert.Equal(50_000m, row.GrossMarginEstimate);
    }

    [Fact]
    public async Task GoodsReceipt_Cancel_ReversesInventory()
    {
        var (companyId, supplierId, productId, warehouseId) = await SetupStockableAsync();

        var order = PurchaseOrder.CreateDraft(companyId, supplierId, new DateOnly(2026, 8, 1));
        order.AddLine("مواد", 10m, 50m, productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        var receipt = GoodsReceipt.CreateDraft(companyId, supplierId, order.Id, warehouseId, new DateOnly(2026, 8, 2));
        receipt.AddLine("مواد", 10m, 10m, 50m, productId);
        var posted = await new GoodsReceiptPostingService(_db).PostAsync(receipt);

        await new GoodsReceiptCancellationService(_db).CancelAsync(
            companyId, posted.Id, new DateOnly(2026, 8, 3), "admin");

        Assert.Equal(GoodsReceiptStatus.Cancelled, posted.Status);
        var onHand = await new InventoryService(_db).GetCompanyOnHandAsync(companyId, productId);
        Assert.Equal(0m, onHand);
    }

    [Fact]
    public async Task GoodsReceipt_Cancel_Blocked_WhenPurchaseInvoicePosted()
    {
        var (companyId, supplierId, productId, warehouseId) = await SetupStockableAsync();

        var order = PurchaseOrder.CreateDraft(companyId, supplierId, new DateOnly(2026, 8, 1));
        order.AddLine("مواد", 5m, 100m, productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        var receipt = GoodsReceipt.CreateDraft(companyId, supplierId, order.Id, warehouseId, new DateOnly(2026, 8, 2));
        receipt.AddLine("مواد", 5m, 5m, 100m, productId);
        var postedGrn = await new GoodsReceiptPostingService(_db).PostAsync(receipt);

        var invoice = PurchaseInvoice.CreateDraft(companyId, supplierId, new DateOnly(2026, 8, 5), "SUP-BLK", true);
        invoice.AddLine("مواد", 5m, 100m, productId);
        invoice.LinkPurchaseOrder(order.Id);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new GoodsReceiptCancellationService(_db).CancelAsync(
                companyId, postedGrn.Id, new DateOnly(2026, 8, 6), "admin"));
        Assert.Contains("فاتورة مشتريات", ex.Message);
    }

    [Fact]
    public async Task DebitNote_Cancel_ReversesJournal_AndRestoresStock()
    {
        var (companyId, supplierId, productId, _) = await SetupStockableAsync();

        var invoice = PurchaseInvoice.CreateDraft(companyId, supplierId, new DateOnly(2026, 8, 1), "SUP-DN-C", true);
        invoice.AddLine("مواد", 10m, 100m, productId);
        var postedInv = await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var note = DebitNote.CreateDraft(companyId, supplierId, postedInv.Id, new DateOnly(2026, 8, 10), "مرتجع جزئي");
        note.AddLine("مرتجع", 2m, 100m, productId);
        var postedNote = await new DebitNotePostingService(_db).PostAsync(note);

        var onHandAfterDn = await new InventoryService(_db).GetCompanyOnHandAsync(companyId, productId);
        Assert.Equal(8m, onHandAfterDn);

        await new DebitNoteCancellationService(_db).CancelAsync(
            companyId, postedNote.Id, new DateOnly(2026, 8, 11), "admin");

        Assert.Equal(DebitNoteStatus.Cancelled, postedNote.Status);
        var onHand = await new InventoryService(_db).GetCompanyOnHandAsync(companyId, productId);
        Assert.Equal(10m, onHand);

        var original = await _db.JournalEntries.SingleAsync(e => e.Id == postedNote.JournalEntryId);
        Assert.Equal(JournalEntryStatus.Reversed, original.Status);
    }
}
