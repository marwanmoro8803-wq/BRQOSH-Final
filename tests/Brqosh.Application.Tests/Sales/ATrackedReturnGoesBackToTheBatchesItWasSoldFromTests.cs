using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٠٢ · <c>order-to-cash-procure-to-pay-inventory#7</c>) <b>المرتجعُ المتتبَّعُ يعود إلى الدفعات التي بيع منها.</b>
///
/// <para>كان <c>CreditNotePostingService</c> يُرحّل سطرَ المرتجع بلا هويّة دفعة، والمَخنَقُ يرفض كلَّ سطرٍ متتبَّعٍ بلاها —
/// <b>فلا يُرجَع دواءٌ ولا غذاءٌ متتبَّعٌ بإشعارٍ دائنٍ أبداً</b>، وصيدليّةٌ لا تقبل مرتجعاً إلا بتسويةٍ يدويّةٍ تُفسد التكلفة.</para>
///
/// <para><b>ومن أين يعرف الإشعارُ الدفعة؟</b> من سطور حركة البيع نفسِها — تحمل الدفعةَ منذ CEEF-230 — ناقصاً ما أرجعته الإشعاراتُ
/// قبله على الفاتورة نفسِها، <b>بترتيب البيع</b>. فلا عمودَ جديدَ ولا سؤالَ في الشاشة.</para>
/// </summary>
public sealed class ATrackedReturnGoesBackToTheBatchesItWasSoldFromTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private long _customerId;
    private long _supplierId;

    public ATrackedReturnGoesBackToTheBatchesItWasSoldFromTests()
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

    /// <summary>
    /// <b>بترتيب البيع</b>: بيع أربعٌ من LOT-A وثلاثٌ من LOT-B، ومرتجعٌ بخمس ⟵ الأربعُ إلى LOT-A وواحدةٌ إلى LOT-B —
    /// سطران بهويّتيهما، والدفعتان ١٠ و٨.
    /// </summary>
    [Fact]
    public async Task AReturn_GoesBackToTheBatchesItWasSoldFrom_InSaleOrder()
    {
        var product = await SoldAcrossTwoBatchesAsync();

        var note = await ReturnAsync(product, 5m);

        var lines = await ReturnLinesAsync(note);
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.BatchNumber == "LOT-A" && l.Quantity == 4m);
        Assert.Contains(lines, l => l.BatchNumber == "LOT-B" && l.Quantity == 1m);
        Assert.Equal(10m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(8m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary><b>والإشعارُ الثاني يبدأ حيث وقف الأوّل</b> — لا يُرجِع إلى LOT-A ما لم يبق منها عند العميل.</summary>
    [Fact]
    public async Task ASecondReturn_ContinuesWhereTheFirstLeftOff()
    {
        var product = await SoldAcrossTwoBatchesAsync();
        await ReturnAsync(product, 5m);

        var second = await ReturnAsync(product, 2m);

        var line = Assert.Single(await ReturnLinesAsync(second));
        Assert.Equal("LOT-B", line.BatchNumber);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(10m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(10m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary><b>والصنفُ نفسُه على سطرين لا يُرجِع الدفعةَ الواحدةَ مرّتين</b> — ٣ و٣ ⟵ LOT-A أربعٌ وLOT-B اثنتان.</summary>
    [Fact]
    public async Task TheSameProductOnTwoLines_DoesNotOverfillOneBatch()
    {
        var product = await SoldAcrossTwoBatchesAsync();

        var note = CreditNote.CreateDraft(_companyId, _customerId, _invoiceId, new DateOnly(2026, 6, 5), "مرتجع");
        note.AddLine("دواء", 3m, 20m, product);
        note.AddLine("دواء", 3m, 20m, product);
        await new CreditNotePostingService(_db).PostAsync(note);

        var lines = await ReturnLinesAsync(note);
        Assert.Equal(4m, lines.Where(l => l.BatchNumber == "LOT-A").Sum(l => l.Quantity));
        Assert.Equal(2m, lines.Where(l => l.BatchNumber == "LOT-B").Sum(l => l.Quantity));
        Assert.Equal(10m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(9m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary>
    /// <b>وما بيع قبل أن يُتتبَّع الصنفُ يُرفض مرتجعُه باسمه</b> — لا يُعرف إلى أيّ دفعةٍ يعود، ولا يُخمَّن؛ ولا إشعارَ ولا حركة.
    /// </summary>
    [Fact]
    public async Task AProductSoldBeforeItWasTracked_IsRefusedByName()
    {
        await SetupAsync();
        var product = await ProductAsync("P-OLD", "شراب", tracked: false);
        await ReceiveAsync(product, 5m, null, null);
        await SellAsync([(product, 5m, null)]);
        (await _db.Products.SingleAsync(p => p.Id == product)).SetTracking(InventoryTracking.Batch, currentOnHand: 0m);
        await _db.SaveChangesAsync();

        var note = CreditNote.CreateDraft(_companyId, _customerId, _invoiceId, new DateOnly(2026, 6, 5), "مرتجع");
        note.AddLine("شراب", 2m, 20m, product);
        var ex = await Assert.ThrowsAsync<AccountingException>(() => new CreditNotePostingService(_db).PostAsync(note));

        Assert.Contains("شراب", ex.Message, StringComparison.Ordinal);
        Assert.Contains("قبل أن يُتتبَّع", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.CreditNotes.AnyAsync(n => n.Status == CreditNoteStatus.Posted));
        Assert.False(await _db.StockMovements.AnyAsync(m => m.Type == StockMovementType.SalesReturn));
    }

    /// <summary>
    /// <b>والدفعةُ التي يسمّيها السطرُ يعود إليها وحدَها</b> — مرتجعٌ بثلاثٍ من LOT-B (الشاشةُ تنسخها من سطر الفاتورة) لا يُقيَّد
    /// أربعاً على LOT-A ولا يبدأ بها: LOT-A تبقى ٦ وLOT-B تصير ١٠.
    /// </summary>
    [Fact]
    public async Task TheBatchNamedOnTheLine_IsTheOneItGoesBackTo()
    {
        var product = await SoldAcrossTwoBatchesAsync();

        var note = await ReturnAsync(product, 3m, "LOT-B");

        var line = Assert.Single(await ReturnLinesAsync(note));
        Assert.Equal("LOT-B", line.BatchNumber);
        Assert.Equal(3m, line.Quantity);
        Assert.Equal(6m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(10m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary><b>ودفعةٌ مسمّاةٌ لم يُبع منها ما يستوعب المرتجعَ تُرفض باسمها</b> — لا يُكمَل الباقي من دفعةٍ أخرى.</summary>
    [Fact]
    public async Task ANamedBatchThatCannotHoldTheReturn_IsRefusedByName()
    {
        var product = await SoldAcrossTwoBatchesAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() => ReturnAsync(product, 4m, "LOT-B"));

        Assert.Contains("LOT-B", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.StockMovements.AnyAsync(m => m.Type == StockMovementType.SalesReturn));
        Assert.Equal(6m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(7m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary>
    /// <b>وكلُّ دفعةٍ تعود بتكلفتها هي</b> — تحت FIFO (والمتوسّطُ المرجّحُ يُسعّر الدفعتين سواءً): LOT-A خمسٌ بعشرة وLOT-B عشرٌ
    /// باثني عشر، وبيع أربعٌ من A (٤٠) وأربعٌ من B تأخذ الباقيةَ بعشرةٍ وثلاثاً باثني عشر (٤٦) — ٨٦؛ ومرتجعُ LOT-A الأربع يعكس
    /// ٤٠ فتبقى ٤٦. <b>وكانت تكلفةُ آخر سطرٍ للصنف (١١٫٥) تُعكس على الكلّ</b> — ٤٦ — فتبقى ٤٠ وتنتفخ طبقةُ المرتجع بستّة.
    /// </summary>
    [Fact]
    public async Task EachBatchGoesBackAtItsOwnCost()
    {
        await SetupAsync();
        (await _db.Companies.SingleAsync(c => c.Id == _companyId)).SetCostingMethod(InventoryCostingMethod.Fifo);
        await _db.SaveChangesAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        await ReceiveAsync(product, 5m, "LOT-A", new DateOnly(2027, 1, 1), unitCost: 10m);
        await ReceiveAsync(product, 10m, "LOT-B", new DateOnly(2027, 6, 1), unitCost: 12m);
        await SellAsync([(product, 4m, "LOT-A"), (product, 4m, "LOT-B")]);
        Assert.Equal(86m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold));

        var note = await ReturnAsync(product, 4m, "LOT-A");

        Assert.Equal(10m, Assert.Single(await ReturnLinesAsync(note)).UnitCost);
        Assert.Equal(46m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold));
        Assert.Equal(5m, await BatchAsync(product, "LOT-A"));
    }

    /// <summary><b>وغيرُ المتتبَّع يعود كما كان</b> — سطرٌ بلا هويّة، ولا دفعةَ تُنشأ.</summary>
    [Fact]
    public async Task AnUntrackedProduct_ReturnsAsBefore()
    {
        await SetupAsync();
        var product = await ProductAsync("P-PLAIN", "ضمادة", tracked: false);
        await ReceiveAsync(product, 10m, null, null);
        await SellAsync([(product, 4m, null)]);

        var note = await ReturnAsync(product, 3m);

        var line = Assert.Single(await ReturnLinesAsync(note));
        Assert.Null(line.BatchNumber);
        Assert.Equal(3m, line.Quantity);
        Assert.False(await _db.StockBatches.AnyAsync(b => b.ProductId == product));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private long _invoiceId;

    private async Task<long> SoldAcrossTwoBatchesAsync()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        await ReceiveAsync(product, 10m, "LOT-A", new DateOnly(2027, 1, 1));
        await ReceiveAsync(product, 10m, "LOT-B", new DateOnly(2027, 6, 1));
        await SellAsync([(product, 4m, "LOT-A"), (product, 3m, "LOT-B")]);
        return product;
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("صيدلية", "310123456700066", BusinessProfile.Healthcare);
        _companyId = company.Id;
        _warehouseId = (await _db.Warehouses.FirstAsync(w => w.CompanyId == _companyId)).Id;
        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
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

    private async Task ReceiveAsync(long productId, decimal qty, string? batch, DateOnly? expiry, decimal unitCost = 10m)
    {
        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 1, 1));
        order.AddLine("بضاعة", qty, unitCost, productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();
        var receipt = GoodsReceipt.CreateDraft(_companyId, _supplierId, order.Id, _warehouseId, new DateOnly(2026, 1, 2));
        receipt.AddLine("بضاعة", qty, qty, unitCost, productId, batch, expiry);
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);
        await _db.SaveChangesAsync();
    }

    private async Task SellAsync(IEnumerable<(long Product, decimal Qty, string? Batch)> lines)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 6, 1));
        foreach (var (product, qty, batch) in lines)
            invoice.AddLine("صنف", qty, 20m, product, batchNumber: batch);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        _invoiceId = invoice.Id;
    }

    private async Task<CreditNote> ReturnAsync(long productId, decimal qty, string? batch = null)
    {
        var note = CreditNote.CreateDraft(_companyId, _customerId, _invoiceId, new DateOnly(2026, 6, 5), "مرتجع");
        note.AddLine("صنف", qty, 20m, productId, batchNumber: batch);
        return await new CreditNotePostingService(_db).PostAsync(note);
    }

    private async Task<List<StockMovementLine>> ReturnLinesAsync(CreditNote note) =>
        (await _db.StockMovements.AsNoTracking()
            .Where(m => m.SourceDocument == $"CreditNote:{note.NoteNumber}" && m.Type == StockMovementType.SalesReturn)
            .ToListAsync())
        .SelectMany(m => m.Lines).ToList();

    private async Task<decimal> BalanceAsync(string code)
    {
        var trial = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        return Math.Round(trial.Where(l => l.AccountCode == code)
            .Sum(l => l.TotalDebit - l.TotalCredit), 2);
    }

    private async Task<decimal> BatchAsync(long productId, string batch) =>
        (await _db.StockBatches.AsNoTracking()
            .Where(b => b.ProductId == productId && b.BatchNumber == batch)
            .Select(b => b.Quantity).ToListAsync()).Sum();
}