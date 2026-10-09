using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١١٠ · <c>order-to-cash-procure-to-pay-inventory#12</c>) <b>البيعُ يخرج من مستودع فاتورته — لا الافتراضيِّ أبداً.</b>
///
/// <para>كان <c>SalesInvoicePostingService</c> وإشعارُ الدائن وتحويلُ أمر البيع يأخذون <c>GetDefaultWarehouseAsync</c>، بينما الاستلامُ والجردُ
/// والتحويلُ تختار أيَّ مستودع — <b>فمنشأةٌ متعدّدةُ الفروع لا تبيع من مخزن فرعها الثاني</b>، ومرتجعُها يدخل الافتراضيَّ ولو خرجت البضاعةُ
/// من غيره. <b>والفارغُ هو الافتراضيّ كما كان</b>، فلا يتغيّر شيءٌ لفاتورةٍ قائمة.</para>
/// </summary>
public sealed class ASaleLeavesFromItsOwnWarehouseTests : IDisposable
{
    private static readonly DateOnly SaleDate = new(2026, 6, 1);

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _aId;      // الافتراضيّ
    private long _bId;      // الفرع الثاني
    private long _customerId;
    private long _supplierId;

    public ASaleLeavesFromItsOwnWarehouseTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary><b>البيعُ من الثاني ينقص الثانيَ لا الأوّل</b> — ١٠ في كلٍّ، وبيع ٤ من B ⟵ B=٦ وA=١٠ وحركةُ الصرف في B.</summary>
    [Fact]
    public async Task ASaleFromWarehouseB_ReducesB_AndLeavesA()
    {
        await SetupAsync();
        var product = await ProductAsync("P-1", "ضمادة", tracked: false);
        await ReceiveAsync(product, 10m, _aId);
        await ReceiveAsync(product, 10m, _bId);

        var invoice = await SellAsync(_bId, (product, 4m, null));

        Assert.Equal(6m, await OnHandAsync(product, _bId));
        Assert.Equal(10m, await OnHandAsync(product, _aId));
        var movement = Assert.Single(await MovementsAsync($"SalesInvoice:{invoice.InvoiceNumber}", StockMovementType.Issue));
        Assert.Equal(_bId, movement.WarehouseId);
    }

    /// <summary>
    /// <b>ومستودعٌ موقوفٌ يُرفض باسمه</b> ولا يُرحَّل شيء — لا حركةَ ولا قيدَ ولا فاتورةَ مرحَّلة، ورصيدُ B كما كان.
    /// </summary>
    [Fact]
    public async Task AStoppedWarehouse_IsRefusedByName_AndNothingIsPosted()
    {
        await SetupAsync();
        var product = await ProductAsync("P-1", "ضمادة", tracked: false);
        await ReceiveAsync(product, 10m, _bId);
        (await _db.Warehouses.SingleAsync(w => w.Id == _bId)).Deactivate();
        await _db.SaveChangesAsync();
        var entriesBefore = await _db.JournalEntries.CountAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() => SellAsync(_bId, (product, 4m, null)));

        Assert.Contains("فرع ثانٍ", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.StockMovements.AnyAsync(m => m.Type == StockMovementType.Issue));
        Assert.False(await _db.SalesInvoices.AnyAsync(i => i.Status == SalesInvoiceStatus.Posted));
        Assert.Equal(entriesBefore, await _db.JournalEntries.CountAsync());
        Assert.Equal(10m, await OnHandAsync(product, _bId));
    }

    /// <summary><b>وبلا مستودعٍ مختارٍ يبقى الافتراضيّ كما كان</b> — ويصرف الرصيدَ من A.</summary>
    [Fact]
    public async Task AnInvoiceWithoutAWarehouse_StaysOnTheDefault()
    {
        await SetupAsync();
        var product = await ProductAsync("P-1", "ضمادة", tracked: false);
        await ReceiveAsync(product, 10m, _aId);
        await ReceiveAsync(product, 10m, _bId);

        var invoice = await SellAsync(null, (product, 4m, null));

        Assert.Equal(6m, await OnHandAsync(product, _aId));
        Assert.Equal(10m, await OnHandAsync(product, _bId));
        Assert.Equal(_aId, Assert.Single(await MovementsAsync($"SalesInvoice:{invoice.InvoiceNumber}", StockMovementType.Issue)).WarehouseId);
    }

    /// <summary>
    /// <b>والمرتجعُ يعود إلى المستودع الذي خرج منه البيعُ ولو تغيّر الافتراضيُّ بعده</b> — بيع من B ثمّ جُعل C الافتراضيَّ ⟵ المرتجع
    /// يدخل B (٦ ← ٨) ولا يدخل C ولا A.
    /// </summary>
    [Fact]
    public async Task ACreditNote_ReturnsIntoTheWarehouseTheSaleLeftFrom_EvenAfterTheDefaultChanged()
    {
        await SetupAsync();
        var product = await ProductAsync("P-1", "ضمادة", tracked: false);
        await ReceiveAsync(product, 10m, _aId);
        await ReceiveAsync(product, 10m, _bId);
        var invoice = await SellAsync(_bId, (product, 4m, null));
        var c = Warehouse.Create(_companyId, "WH-C", "فرع ثالث");
        _db.Warehouses.Add(c);
        (await _db.Warehouses.SingleAsync(w => w.Id == _aId)).ClearDefault();
        await _db.SaveChangesAsync();
        c.SetAsDefault();
        await _db.SaveChangesAsync();

        var note = await ReturnAsync(invoice, product, 2m);

        var movement = Assert.Single(await MovementsAsync($"CreditNote:{note.NoteNumber}", StockMovementType.SalesReturn));
        Assert.Equal(_bId, movement.WarehouseId);
        Assert.Equal(8m, await OnHandAsync(product, _bId));
        Assert.Equal(10m, await OnHandAsync(product, _aId));
        Assert.Equal(0m, await OnHandAsync(product, c.Id));
    }

    /// <summary><b>والمتتبَّعُ يعود إلى دفعته في المستودع الذي بيع منه</b> — LOT-B في B: بيع ٤ ومرتجع ٣ ⟵ LOT-B تصير ٩ في B.</summary>
    [Fact]
    public async Task ATrackedReturn_GoesBackToTheBatchInWarehouseB()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        await ReceiveAsync(product, 10m, _aId, "LOT-A", new DateOnly(2027, 1, 1));
        await ReceiveAsync(product, 10m, _bId, "LOT-B", new DateOnly(2027, 6, 1));
        var invoice = await SellAsync(_bId, (product, 4m, "LOT-B"));
        Assert.Equal(6m, await BatchAsync(product, "LOT-B", _bId));

        var note = await ReturnAsync(invoice, product, 3m);

        var movement = Assert.Single(await MovementsAsync($"CreditNote:{note.NoteNumber}", StockMovementType.SalesReturn));
        Assert.Equal(_bId, movement.WarehouseId);
        Assert.Equal("LOT-B", Assert.Single(movement.Lines).BatchNumber);
        Assert.Equal(9m, await BatchAsync(product, "LOT-B", _bId));
        Assert.Equal(10m, await BatchAsync(product, "LOT-A", _aId));
        Assert.Equal(0m, await BatchAsync(product, "LOT-B", _aId));
    }

    /// <summary>
    /// <b>وأمرُ البيع يُفوتَر من B فتُخصَّص دفعاتُ B</b> — LOT-A (أقدمُ انتهاءً) في A وLOT-B في B ⟵ الفاتورةُ من B تسحب LOT-B، ولا تُمسّ LOT-A
    /// (وكان التخصيصُ من الافتراضيّ يختار LOT-A فيُرفض الصرفُ من B).
    /// </summary>
    [Fact]
    public async Task ASalesOrderInvoicedFromB_AllocatesTheBatchesOfB()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        await ReceiveAsync(product, 10m, _aId, "LOT-A", new DateOnly(2026, 9, 1));
        await ReceiveAsync(product, 10m, _bId, "LOT-B", new DateOnly(2027, 6, 1));
        var order = SalesOrder.CreateDraft(_companyId, _customerId, SaleDate);
        order.AddLine("صنف", 4m, 20m, product);
        await new SalesOrderService(_db).ConfirmAsync(order);

        var invoice = await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, SaleDate, warehouseId: _bId);

        Assert.Equal(_bId, invoice.WarehouseId);
        var movement = Assert.Single(await MovementsAsync($"SalesInvoice:{invoice.InvoiceNumber}", StockMovementType.Issue));
        Assert.Equal(_bId, movement.WarehouseId);
        Assert.Equal("LOT-B", Assert.Single(movement.Lines).BatchNumber);
        Assert.Equal(6m, await BatchAsync(product, "LOT-B", _bId));
        Assert.Equal(10m, await BatchAsync(product, "LOT-A", _aId));
    }

    /// <summary><b>والإلغاءُ يعكس إلى B</b> — بيع ٤ من B ثمّ إلغاؤه ⟵ B=١٠ وA=١٠ كما كانا.</summary>
    [Fact]
    public async Task CancellingTheInvoice_ReversesIntoWarehouseB()
    {
        await SetupAsync();
        var product = await ProductAsync("P-1", "ضمادة", tracked: false);
        await ReceiveAsync(product, 10m, _aId);
        await ReceiveAsync(product, 10m, _bId);
        var invoice = await SellAsync(_bId, (product, 4m, null));
        Assert.Equal(6m, await OnHandAsync(product, _bId));

        await new SalesInvoiceCancellationService(_db).CancelAsync(_companyId, invoice.Id, SaleDate.AddDays(1), "admin");

        Assert.Equal(10m, await OnHandAsync(product, _bId));
        Assert.Equal(10m, await OnHandAsync(product, _aId));
    }

    /// <summary><b>والمجالُ</b>: معرّفٌ غيرُ موجبٍ يُرفض، ولا يُغيَّر المستودعُ بعد الترحيل.</summary>
    [Fact]
    public async Task TheDomain_RefusesABadWarehouse_AndAChangeAfterPosting()
    {
        await SetupAsync();
        var product = await ProductAsync("P-1", "ضمادة", tracked: false);
        await ReceiveAsync(product, 10m, _aId);
        var draft = SalesInvoice.CreateDraft(_companyId, _customerId, SaleDate);

        Assert.Throws<AccountingException>(() => draft.SetWarehouse(0));
        draft.SetWarehouse(_bId);
        Assert.Equal(_bId, draft.WarehouseId);
        draft.SetWarehouse(null);
        Assert.Null(draft.WarehouseId);

        var posted = await SellAsync(null, (product, 1m, null));
        Assert.Throws<AccountingException>(() => posted.SetWarehouse(_bId));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("صيدلية", "310123456700066", BusinessProfile.Healthcare);
        _companyId = company.Id;
        _aId = (await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId)).Id;
        var b = Warehouse.Create(_companyId, "WH-B", "فرع ثانٍ");
        _db.Warehouses.Add(b);
        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _bId = b.Id;
        _customerId = customer.Id;
        _supplierId = supplier.Id;
    }

    private async Task<long> ProductAsync(string code, string name, bool tracked)
    {
        var product = Product.Create(_companyId, code, name, "علبة", ProductType.Stockable, salePrice: 20m);
        if (tracked)
            product.SetTracking(InventoryTracking.Batch, currentOnHand: 0m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product.Id;
    }

    private async Task ReceiveAsync(long productId, decimal qty, long warehouseId, string? batch = null, DateOnly? expiry = null)
    {
        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 1, 1));
        order.AddLine("بضاعة", qty, 10m, productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();
        var receipt = GoodsReceipt.CreateDraft(_companyId, _supplierId, order.Id, warehouseId, new DateOnly(2026, 1, 2));
        receipt.AddLine("بضاعة", qty, qty, 10m, productId, batch, expiry);
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);
        await _db.SaveChangesAsync();
    }

    private async Task<SalesInvoice> SellAsync(long? warehouseId, (long Product, decimal Qty, string? Batch) line)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, SaleDate);
        invoice.SetWarehouse(warehouseId);
        invoice.AddLine("صنف", line.Qty, 20m, line.Product, batchNumber: line.Batch);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private async Task<CreditNote> ReturnAsync(SalesInvoice invoice, long productId, decimal qty)
    {
        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 6, 5), "مرتجع");
        note.AddLine("صنف", qty, 20m, productId);
        return await new CreditNotePostingService(_db).PostAsync(note);
    }

    private Task<decimal> OnHandAsync(long productId, long warehouseId) =>
        new InventoryService(_db).GetOnHandAsync(_companyId, productId, warehouseId);

    private async Task<List<StockMovement>> MovementsAsync(string source, StockMovementType type) =>
        await _db.StockMovements.AsNoTracking().Where(m => m.SourceDocument == source && m.Type == type).ToListAsync();

    private async Task<decimal> BatchAsync(long productId, string batch, long warehouseId) =>
        (await _db.StockBatches.AsNoTracking()
            .Where(b => b.ProductId == productId && b.BatchNumber == batch && b.WarehouseId == warehouseId)
            .Select(b => b.Quantity).ToListAsync()).Sum();
}
