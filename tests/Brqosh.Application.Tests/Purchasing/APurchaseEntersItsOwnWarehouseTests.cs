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

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١١١ · <c>order-to-cash-procure-to-pay-inventory#12</c>) <b>الشراءُ يدخل مستودعَ فاتورته ومرتجعُه يخرج منه — لا الافتراضيَّ أبداً.</b>
///
/// <para>كانت فاتورةُ الشراء المباشرةُ والاستلامُ الآليُّ من أمر الشراء والإشعارُ المدينُ تأخذ <c>GetDefaultWarehouseAsync</c>، بينما الاستلامُ بإذنٍ
/// والجردُ والتحويلُ تختار أيَّ مستودع. <b>والفارغُ هو الافتراضيّ كما كان</b>، فلا يتغيّر شيءٌ لفاتورةٍ قائمة.</para>
/// </summary>
public sealed class APurchaseEntersItsOwnWarehouseTests : IDisposable
{
    private static readonly DateOnly BuyDate = new(2026, 2, 1);

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _aId;      // الافتراضيّ
    private long _bId;      // الفرع الثاني
    private long _customerId;
    private long _supplierId;
    private int _references;

    public APurchaseEntersItsOwnWarehouseTests()
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

    /// <summary><b>الشراءُ المباشرُ إلى B يرفع B لا A</b> — عشرٌ في B، وA صفرٌ، وحركةُ الاستلام في B.</summary>
    [Fact]
    public async Task ADirectPurchaseIntoB_RaisesB_NotA()
    {
        await SetupAsync();
        var product = await ProductAsync("P-1", "ضمادة", tracked: false);

        var invoice = await BuyAsync(_bId, product, 10m);

        Assert.Equal(10m, await OnHandAsync(product, _bId));
        Assert.Equal(0m, await OnHandAsync(product, _aId));
        var movement = Assert.Single(await MovementsAsync($"PurchaseInvoice:{invoice.InvoiceNumber}", StockMovementType.Receipt));
        Assert.Equal(_bId, movement.WarehouseId);
    }

    /// <summary><b>ومستودعٌ موقوفٌ يُرفض باسمه</b> ولا يُرحَّل شيء — لا حركةَ ولا قيدَ ولا فاتورةَ مرحَّلة.</summary>
    [Fact]
    public async Task AStoppedWarehouse_IsRefusedByName_AndNothingIsPosted()
    {
        await SetupAsync();
        var product = await ProductAsync("P-1", "ضمادة", tracked: false);
        (await _db.Warehouses.SingleAsync(w => w.Id == _bId)).Deactivate();
        await _db.SaveChangesAsync();
        var entriesBefore = await _db.JournalEntries.CountAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() => BuyAsync(_bId, product, 10m));

        Assert.Contains("فرع ثانٍ", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.StockMovements.AnyAsync(m => m.Type == StockMovementType.Receipt));
        Assert.False(await _db.PurchaseInvoices.AnyAsync(i => i.Status == PurchaseInvoiceStatus.Posted));
        Assert.Equal(entriesBefore, await _db.JournalEntries.CountAsync());
    }

    /// <summary><b>وبلا مستودعٍ مختارٍ يبقى الافتراضيّ كما كان.</b></summary>
    [Fact]
    public async Task AnInvoiceWithoutAWarehouse_StaysOnTheDefault()
    {
        await SetupAsync();
        var product = await ProductAsync("P-1", "ضمادة", tracked: false);

        var invoice = await BuyAsync(null, product, 10m);

        Assert.Equal(10m, await OnHandAsync(product, _aId));
        Assert.Equal(0m, await OnHandAsync(product, _bId));
        Assert.Equal(_aId, Assert.Single(await MovementsAsync($"PurchaseInvoice:{invoice.InvoiceNumber}", StockMovementType.Receipt)).WarehouseId);
    }

    /// <summary><b>وأمرُ الشراء يُفوتَر إلى B فيُستلم آليّاً في B</b> — إذنُ الاستلام الآليُّ في B، والفاتورةُ تحمل B، وA صفر.</summary>
    [Fact]
    public async Task APurchaseOrderInvoicedWithWarehouseB_AutoReceivesIntoB()
    {
        await SetupAsync();
        var product = await ProductAsync("P-1", "ضمادة", tracked: false);
        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, BuyDate);
        order.AddLine("بضاعة", 10m, 10m, product);
        await new PurchaseOrderService(_db).ApproveAsync(order);

        var invoice = await new PurchaseOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, "SUP-PO-1", BuyDate, vatDeductible: true, warehouseId: _bId);

        Assert.Equal(_bId, invoice.WarehouseId);
        var receipt = await _db.GoodsReceipts.AsNoTracking().SingleAsync(g => g.PurchaseOrderId == order.Id);
        Assert.Equal(_bId, receipt.WarehouseId);
        Assert.Equal(10m, await OnHandAsync(product, _bId));
        Assert.Equal(0m, await OnHandAsync(product, _aId));
    }

    /// <summary>
    /// <b>والإشعارُ المدينُ يخرج من B ولو تغيّر الافتراضيُّ بعد الشراء</b> — عشرٌ في B وعشرٌ في A، ثمّ جُعل C الافتراضيَّ؛ مرتجعٌ بأربعٍ ⟵ B=٦
    /// وA=١٠ وC=٠.
    /// </summary>
    [Fact]
    public async Task ADebitNote_LeavesFromWarehouseB_EvenAfterTheDefaultChanged()
    {
        await SetupAsync();
        var product = await ProductAsync("P-1", "ضمادة", tracked: false);
        await BuyAsync(_aId, product, 10m);
        var invoice = await BuyAsync(_bId, product, 10m);
        var c = Warehouse.Create(_companyId, "WH-C", "فرع ثالث");
        _db.Warehouses.Add(c);
        (await _db.Warehouses.SingleAsync(w => w.Id == _aId)).ClearDefault();
        await _db.SaveChangesAsync();
        c.SetAsDefault();
        await _db.SaveChangesAsync();

        var note = await ReturnAsync(invoice, product, 4m);

        var movement = Assert.Single(await MovementsAsync($"DebitNote:{note.NoteNumber}", StockMovementType.Issue));
        Assert.Equal(_bId, movement.WarehouseId);
        Assert.Equal(6m, await OnHandAsync(product, _bId));
        Assert.Equal(10m, await OnHandAsync(product, _aId));
        Assert.Equal(0m, await OnHandAsync(product, c.Id));
    }

    /// <summary>
    /// <b>والمتتبَّعُ يخرج من دفعته في B ويُقيَّد برصيد B</b> — LOT-1 عشرٌ في B وعشرٌ في A (باسمٍ واحد)، وبيع سبعٌ من B فبقي ثلاثٌ؛ مرتجعٌ بخمسٍ يُرفض
    /// (رصيدُ A لا يُحتسب)، وبثلاثٍ يخرج من LOT-1 في B فتصير صفراً وتبقى LOT-1 في A عشراً.
    /// </summary>
    [Fact]
    public async Task ATrackedDebitNote_LeavesTheBatchInB_AndIsCappedByBsOnHand()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        var expiry = new DateOnly(2027, 6, 1);
        await BuyAsync(_aId, product, 10m, "LOT-1", expiry);
        var invoice = await BuyAsync(_bId, product, 10m, "LOT-1", expiry);
        var sale = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 1));
        sale.SetWarehouse(_bId);
        sale.AddLine("صنف", 7m, 20m, product, batchNumber: "LOT-1");
        await new SalesInvoicePostingService(_db).PostAsync(sale);
        Assert.Equal(3m, await BatchAsync(product, "LOT-1", _bId));

        await Assert.ThrowsAsync<AccountingException>(() => ReturnAsync(invoice, product, 5m));
        _db.ChangeTracker.Clear();
        Assert.Equal(3m, await BatchAsync(product, "LOT-1", _bId));
        Assert.Equal(10m, await BatchAsync(product, "LOT-1", _aId));

        var note = await ReturnAsync(invoice, product, 3m);

        var movement = Assert.Single(await MovementsAsync($"DebitNote:{note.NoteNumber}", StockMovementType.Issue));
        Assert.Equal(_bId, movement.WarehouseId);
        Assert.Equal("LOT-1", Assert.Single(movement.Lines).BatchNumber);
        Assert.Equal(0m, await BatchAsync(product, "LOT-1", _bId));
        Assert.Equal(10m, await BatchAsync(product, "LOT-1", _aId));
    }

    /// <summary><b>وإلغاءُ ذلك الإشعار يُعيد إلى B</b> — ٦ ← ١٠ في B، وA كما هو.</summary>
    [Fact]
    public async Task CancellingTheDebitNote_ReEntersWarehouseB()
    {
        await SetupAsync();
        var product = await ProductAsync("P-1", "ضمادة", tracked: false);
        await BuyAsync(_aId, product, 10m);
        var invoice = await BuyAsync(_bId, product, 10m);
        var note = await ReturnAsync(invoice, product, 4m);
        Assert.Equal(6m, await OnHandAsync(product, _bId));

        await new DebitNoteCancellationService(_db).CancelAsync(_companyId, note.Id, new DateOnly(2026, 6, 6), "admin");

        Assert.Equal(10m, await OnHandAsync(product, _bId));
        Assert.Equal(10m, await OnHandAsync(product, _aId));
    }

    /// <summary>
    /// <b>ودفعةٌ استُلمت في مستودعين تُرجَع منهما معاً، ويُلغى إشعارُها مرّةً</b> — LOT-1 ستٌّ في A وأربعٌ في B على أمرٍ واحدٍ وفاتورةٍ واحدة؛ مرتجعٌ
    /// بثمانٍ منها باسمها يخرج ستّاً من A واثنتين من B بحركتين، <b>ولا يسأل البانيُ «حدِّد الدفعة» بين LOT-1 وLOT-1</b>؛ وإلغاؤه يُعيد ٦ و٤.
    /// <b>وكانت المسمّاةُ تُؤخذ من أوّل مستودعاتها وحدَه فيُرفض فوق الست</b> (Fable).
    /// </summary>
    [Fact]
    public async Task OneBatchReceivedIntoTwoWarehouses_ReturnsFromBoth_AndCancelsOnce()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        var expiry = new DateOnly(2027, 6, 1);
        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 1, 1));
        order.AddLine("بضاعة", 10m, 10m, product);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();
        foreach (var (warehouse, qty) in new[] { (_aId, 6m), (_bId, 4m) })
        {
            var receipt = GoodsReceipt.CreateDraft(_companyId, _supplierId, order.Id, warehouse, new DateOnly(2026, 1, 2));
            receipt.AddLine("بضاعة", 10m, qty, 10m, product, "LOT-1", expiry);
            await new GoodsReceiptPostingService(_db).PostAsync(receipt);
            await _db.SaveChangesAsync();
        }
        var invoice = PurchaseInvoice.CreateDraft(_companyId, _supplierId, BuyDate, $"SUP-PO-{++_references}", vatDeductible: true);
        invoice.LinkPurchaseOrder(order.Id);
        invoice.AddLine("بضاعة", 10m, 10m, product);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var draft = await new DebitNoteDraftBuilder(_db).BuildAsync(
            _companyId, invoice.Id, new DateOnly(2026, 6, 5), "مرتجع", [new RequestedReturn(0, 8m, BatchNumber: "LOT-1")]);
        var note = await new DebitNotePostingService(_db).PostAsync(draft);

        Assert.Equal(2, (await MovementsAsync($"DebitNote:{note.NoteNumber}", StockMovementType.Issue)).Count);
        Assert.Equal(0m, await BatchAsync(product, "LOT-1", _aId));
        Assert.Equal(2m, await BatchAsync(product, "LOT-1", _bId));

        await new DebitNoteCancellationService(_db).CancelAsync(_companyId, note.Id, new DateOnly(2026, 6, 6), "admin");
        _db.ChangeTracker.Clear();
        Assert.Equal(6m, await BatchAsync(product, "LOT-1", _aId));
        Assert.Equal(4m, await BatchAsync(product, "LOT-1", _bId));
    }

    /// <summary><b>والمجالُ</b>: معرّفٌ غيرُ موجبٍ يُرفض، ولا يُغيَّر المستودعُ بعد الترحيل.</summary>
    [Fact]
    public async Task TheDomain_RefusesABadWarehouse_AndAChangeAfterPosting()
    {
        await SetupAsync();
        var product = await ProductAsync("P-1", "ضمادة", tracked: false);
        var draft = PurchaseInvoice.CreateDraft(_companyId, _supplierId, BuyDate, "SUP-D", vatDeductible: true);

        Assert.Throws<AccountingException>(() => draft.SetWarehouse(0));
        draft.SetWarehouse(_bId);
        Assert.Equal(_bId, draft.WarehouseId);
        draft.SetWarehouse(null);
        Assert.Null(draft.WarehouseId);

        var posted = await BuyAsync(null, product, 1m);
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

    /// <summary>فاتورةُ شراءٍ مباشرةٌ (بلا أمرٍ ولا إذن) إلى <paramref name="warehouseId"/> بسعر ١٠.</summary>
    private async Task<PurchaseInvoice> BuyAsync(long? warehouseId, long productId, decimal qty,
        string? batch = null, DateOnly? expiry = null)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, BuyDate, $"SUP-{productId}-{++_references}", vatDeductible: true);
        invoice.SetWarehouse(warehouseId);
        invoice.AddLine("بضاعة", qty, 10m, productId, batchNumber: batch, expiryDate: expiry);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
        return invoice;
    }

    private async Task<DebitNote> ReturnAsync(PurchaseInvoice invoice, long productId, decimal qty)
    {
        var note = DebitNote.CreateDraft(_companyId, _supplierId, invoice.Id, new DateOnly(2026, 6, 5), "مرتجع");
        note.AddLine("صنف", qty, 10m, productId);
        return await new DebitNotePostingService(_db).PostAsync(note);
    }

    private Task<decimal> OnHandAsync(long productId, long warehouseId) =>
        new InventoryService(_db).GetOnHandAsync(_companyId, productId, warehouseId);

    private async Task<List<StockMovement>> MovementsAsync(string source, StockMovementType type) =>
        await _db.StockMovements.AsNoTracking().Include(m => m.Lines)
            .Where(m => m.SourceDocument == source && m.Type == type).ToListAsync();

    private async Task<decimal> BatchAsync(long productId, string batch, long warehouseId) =>
        (await _db.StockBatches.AsNoTracking()
            .Where(b => b.ProductId == productId && b.BatchNumber == batch && b.WarehouseId == warehouseId)
            .Select(b => b.Quantity).ToListAsync()).Sum();
}
