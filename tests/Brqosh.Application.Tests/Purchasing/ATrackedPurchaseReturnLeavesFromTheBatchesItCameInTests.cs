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

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٠٥ · <c>order-to-cash-procure-to-pay-inventory#7</c>) <b>المرتجعُ المتتبَّعُ إلى المورّد يخرج من الدفعات التي دخل بها.</b>
///
/// <para>كان <c>DebitNotePostingService</c> يُرحّل سطرَ الصرف بلا هويّة دفعة، والمَخنَقُ يرفض كلَّ سطرٍ متتبَّعٍ بلاها —
/// <b>فلا يُرجَع دواءٌ ولا غذاءٌ متتبَّعٌ إلى مورّده بإشعارٍ مدينٍ أبداً</b>.</para>
///
/// <para><b>ومن أين يعرف الإشعارُ الدفعة؟</b> من سطور حركات الاستلام (إذونُ استلام أمر الشراء) ناقصاً ما أرجعته الإشعاراتُ
/// قبله، بترتيب الاستلام — <b>ثمّ مقيَّداً بما بقي ماديّاً</b> من الدفعة في المستودع: فبعضُها قد بيع، وهذا يخالف مرتجعَ العميل.</para>
///
/// <para>السيناريو الأساس: أمرُ شراءٍ بعشرين، استُلمت عشرةٌ منها بدفعة LOT-A (تنتهي ٢٠٢٧-٠١) ثمّ عشرةٌ بدفعة LOT-B (٢٠٢٧-٠٦)،
/// وفُوتِرت العشرون بسعر ١٠.</para>
/// </summary>
public sealed class ATrackedPurchaseReturnLeavesFromTheBatchesItCameInTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private long _customerId;
    private long _supplierId;
    private long _invoiceId;
    private int _supplierReferences;

    public ATrackedPurchaseReturnLeavesFromTheBatchesItCameInTests()
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
    /// <b>بترتيب الاستلام</b>: مرتجعٌ بأربعَ عشرة ⟵ عشرٌ من LOT-A وأربعٌ من LOT-B — سطران بهويّتيهما، وتبقى LOT-A صفراً
    /// وLOT-B ستّاً.
    /// </summary>
    [Fact]
    public async Task AReturn_LeavesFromTheBatchesItCameIn_InReceiptOrder()
    {
        var product = await ReceivedAcrossTwoBatchesAsync();

        var note = await ReturnAsync(product, 14m);

        var lines = await ReturnLinesAsync(note);
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.BatchNumber == "LOT-A" && l.Quantity == 10m);
        Assert.Contains(lines, l => l.BatchNumber == "LOT-B" && l.Quantity == 4m);
        Assert.Equal(0m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(6m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary><b>والإشعارُ الثاني يبدأ حيث وقف الأوّل</b>: ستٌّ ثمّ ستٌّ ⟵ الأولى من LOT-A (يبقى أربعٌ)، والثانية أربعٌ من LOT-A واثنتان من LOT-B.</summary>
    [Fact]
    public async Task ASecondReturn_ContinuesWhereTheFirstLeftOff()
    {
        var product = await ReceivedAcrossTwoBatchesAsync();
        await ReturnAsync(product, 6m);

        var second = await ReturnAsync(product, 6m);

        var lines = await ReturnLinesAsync(second);
        Assert.Equal(4m, lines.Where(l => l.BatchNumber == "LOT-A").Sum(l => l.Quantity));
        Assert.Equal(2m, lines.Where(l => l.BatchNumber == "LOT-B").Sum(l => l.Quantity));
        Assert.Equal(0m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(8m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary>
    /// <b>والمُرجَعُ سلفاً يُطرح من المستلَم لا يكتفي برصيد المستودع</b>: استُلمت عشرٌ أخرى من LOT-A بأمرِ شراءٍ آخر فصار رصيدُها عشرين
    /// ماديّاً، والفاتورةُ الأولى لم تستلم منها إلا عشراً. مرتجعٌ بستٍّ ثمّ بستٍّ ⟵ الثاني أربعٌ من LOT-A واثنتان من LOT-B، <b>لا ستٌّ من LOT-A</b>
    /// (فالرصيدُ وحدَه كان يسمح بها، وهي ممّا لم تستلمه هذه الفاتورة).
    /// </summary>
    [Fact]
    public async Task ASecondReturn_NeverTakesMoreOfABatchThanTheInvoiceReceived()
    {
        var product = await ReceivedAcrossTwoBatchesAsync();
        var invoiceId = _invoiceId;
        await PurchaseAsync(product, 10m, (10m, "LOT-A", new DateOnly(2027, 1, 1)));
        _invoiceId = invoiceId;
        await ReturnAsync(product, 6m);

        var second = await ReturnAsync(product, 6m);

        var lines = await ReturnLinesAsync(second);
        Assert.Equal(4m, lines.Where(l => l.BatchNumber == "LOT-A").Sum(l => l.Quantity));
        Assert.Equal(2m, lines.Where(l => l.BatchNumber == "LOT-B").Sum(l => l.Quantity));
        Assert.Equal(10m, await BatchAsync(product, "LOT-A"));
    }

    /// <summary>
    /// <b>والصنفُ نفسُه على سطرين لا يُخرج الدفعةَ الواحدةَ مرّتين</b>: ثمانيةٌ وثمانيةٌ ⟵ LOT-A عشرٌ وLOT-B ستٌّ، لا ستَّ عشرةَ من LOT-A
    /// (وهي لا تحمل إلا عشراً).
    /// </summary>
    [Fact]
    public async Task TheSameProductOnTwoLines_DoesNotOverdrawOneBatch()
    {
        var product = await ReceivedAcrossTwoBatchesAsync();

        var note = DebitNote.CreateDraft(_companyId, _supplierId, _invoiceId, new DateOnly(2026, 6, 5), "مرتجع");
        note.AddLine("دواء", 8m, 10m, product);
        note.AddLine("دواء", 8m, 10m, product);
        await new DebitNotePostingService(_db).PostAsync(note);

        var lines = await ReturnLinesAsync(note);
        Assert.Equal(10m, lines.Where(l => l.BatchNumber == "LOT-A").Sum(l => l.Quantity));
        Assert.Equal(6m, lines.Where(l => l.BatchNumber == "LOT-B").Sum(l => l.Quantity));
        Assert.Equal(0m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(4m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary>
    /// <b>ودفعةٌ بيع بعضُها تُقيَّد بما بقي ماديّاً</b>: بيع سبعٌ من LOT-A فبقي ثلاثٌ؛ مرتجعٌ بخمسٍ ⟵ ثلاثٌ من LOT-A (لا خمسٌ، وهي لا تحمل)
    /// واثنتان من LOT-B — فتبقى LOT-A صفراً وLOT-B ثمانياً.
    /// </summary>
    [Fact]
    public async Task ABatchPartlySold_IsCappedByWhatIsStillOnHand()
    {
        var product = await ReceivedAcrossTwoBatchesAsync();
        await SellAsync(product, 7m, "LOT-A");

        var note = await ReturnAsync(product, 5m);

        var lines = await ReturnLinesAsync(note);
        Assert.Equal(3m, lines.Where(l => l.BatchNumber == "LOT-A").Sum(l => l.Quantity));
        Assert.Equal(2m, lines.Where(l => l.BatchNumber == "LOT-B").Sum(l => l.Quantity));
        Assert.Equal(0m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(8m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary>
    /// <b>وما لا تغطّيه الدفعاتُ الباقيةُ يُرفض باسم الصنف</b>: بيعت عشرٌ من LOT-A وتسعٌ من LOT-B فبقيت واحدةٌ؛ مرتجعٌ بثلاثٍ ⟵ رفضٌ
    /// يسمّي «دواء»، <b>ولا إشعارَ ولا حركةَ صرف</b>.
    /// </summary>
    [Fact]
    public async Task AReturnTheRemainingBatchesCannotCover_IsRefusedByName()
    {
        var product = await ReceivedAcrossTwoBatchesAsync();
        await SellAsync(product, 10m, "LOT-A");
        await SellAsync(product, 9m, "LOT-B");

        var ex = await Assert.ThrowsAsync<AccountingException>(() => ReturnAsync(product, 3m));

        // ‏نصُّ الخدمة نفسِها — فرصيدُ المستودع يرفض الطلبَ نفسَه باسم الصنف، وكان التأكيدُ يمرّ بلا الحارس (ح-١٠٥ · Fable).
        Assert.Contains("لا يبقى من دفعات 'دواء'", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.DebitNotes.AnyAsync(n => n.Status == DebitNoteStatus.Posted));
        Assert.False(await _db.StockMovements.AnyAsync(m => m.SourceDocument!.StartsWith("DebitNote:")));
        Assert.Equal(1m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary>
    /// <b>والدفعةُ التي يسمّيها السطرُ يخرج منها وحدَها</b>: مرتجعٌ بأربعٍ من LOT-B ⟵ سطرٌ واحدٌ باسمها، وLOT-A لا تُمسّ (عشر)
    /// وLOT-B تصير ستّاً — لا يبدأ بأقدم ما استُلم.
    /// </summary>
    [Fact]
    public async Task TheBatchNamedOnTheLine_IsTheOneItLeavesFrom()
    {
        var product = await ReceivedAcrossTwoBatchesAsync();

        var note = await ReturnAsync(product, 4m, "LOT-B");

        var line = Assert.Single(await ReturnLinesAsync(note));
        Assert.Equal("LOT-B", line.BatchNumber);
        Assert.Equal(4m, line.Quantity);
        Assert.Equal(10m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(6m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary>
    /// <b>ودفعةٌ مسمّاةٌ لا يبقى منها ما يستوعب المرتجعَ تُرفض باسمها</b> — بيعت سبعٌ من LOT-A فبقي ثلاثٌ، ومرتجعٌ بخمسٍ منها
    /// يُرفض ولا يُكمَل من LOT-B.
    /// </summary>
    [Fact]
    public async Task ANamedBatchThatCannotHoldTheReturn_IsRefusedByName()
    {
        var product = await ReceivedAcrossTwoBatchesAsync();
        await SellAsync(product, 7m, "LOT-A");

        var ex = await Assert.ThrowsAsync<AccountingException>(() => ReturnAsync(product, 5m, "LOT-A"));

        Assert.Contains("LOT-A", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.StockMovements.AnyAsync(m => m.SourceDocument!.StartsWith("DebitNote:")));
        Assert.Equal(3m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(10m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary>
    /// <b>وما استُلم قبل أن يُتتبَّع الصنفُ يُرفض مرتجعُه باسمه</b> — لا تُعرف دفعتُه ولا تُخمَّن: استُلمت خمسٌ بلا دفعة، وبيعت كلُّها،
    /// ثمّ تُتُبِّع الصنف؛ ومرتجعٌ بإثنتين ⟵ رفضٌ يسمّي «شراب» ويقول «قبل أن يُتتبَّع».
    /// </summary>
    [Fact]
    public async Task AProductReceivedBeforeItWasTracked_IsRefusedByName()
    {
        await SetupAsync();
        var product = await ProductAsync("P-OLD", "شراب", tracked: false);
        await PurchaseAsync(product, 5m, (5m, null, null));
        await SellAsync(product, 5m, null);
        (await _db.Products.SingleAsync(p => p.Id == product)).SetTracking(InventoryTracking.Batch, currentOnHand: 0m);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() => ReturnAsync(product, 2m));

        Assert.Contains("شراب", ex.Message, StringComparison.Ordinal);
        Assert.Contains("قبل أن يُتتبَّع", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.DebitNotes.AnyAsync(n => n.Status == DebitNoteStatus.Posted));
    }

    /// <summary><b>وغيرُ المتتبَّع يخرج كما كان</b> — سطرٌ بلا هويّة، ولا دفعةَ تُنشأ، والرصيدُ ينقص بالكمّية.</summary>
    [Fact]
    public async Task AnUntrackedProduct_ReturnsAsBefore()
    {
        await SetupAsync();
        var product = await ProductAsync("P-PLAIN", "ضمادة", tracked: false);
        await PurchaseAsync(product, 10m, (10m, null, null));

        var note = await ReturnAsync(product, 3m);

        var line = Assert.Single(await ReturnLinesAsync(note));
        Assert.Null(line.BatchNumber);
        Assert.Equal(3m, line.Quantity);
        Assert.False(await _db.StockBatches.AnyAsync(b => b.ProductId == product));
        Assert.Equal(7m, (await _db.InventoryBalances.SingleAsync(b => b.ProductId == product)).QuantityOnHand);
    }

    /// <summary>
    /// <b>والدفعةُ المنتهيةُ تعود إلى مورّدها</b> — وهو أشيعُ مرتجعات الصيدليّة: LOT-X انتهت في فبراير، ومرتجعُها في يونيو يخرج
    /// منها؛ <b>وكان المَخنَقُ يرفضه «لا تُباع. أخرِجْها بتسويةِ إتلاف»</b> فيضيع رصيدُ المورّد.
    /// </summary>
    [Fact]
    public async Task AnExpiredBatch_GoesBackToItsSupplier()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        await PurchaseAsync(product, 10m, (10m, "LOT-X", new DateOnly(2026, 2, 15)));

        var note = await ReturnAsync(product, 4m, "LOT-X");

        var line = Assert.Single(await ReturnLinesAsync(note));
        Assert.Equal("LOT-X", line.BatchNumber);
        Assert.Equal(6m, await BatchAsync(product, "LOT-X"));
    }

    /// <summary><b>شاهدُ ضبط</b> — والمنتهيةُ ما تزال لا تُباع: الإذنُ للمرتجع إلى المورّد وحدَه.</summary>
    [Fact]
    public async Task AnExpiredBatch_StillCannotBeSold()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        await PurchaseAsync(product, 10m, (10m, "LOT-X", new DateOnly(2026, 2, 15)));

        var ex = await Assert.ThrowsAsync<AccountingException>(() => SellAsync(product, 1m, "LOT-X"));

        Assert.Contains("منتهيةُ الصلاحية", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وصفُّ ما قبل التتبّع لا يحجب ما تغطّيه الدفعات</b> — خمسٌ استُلمت قبل التتبّع وبيعت، ثمّ عشرٌ من LOT-B على الأمر نفسِه،
    /// والفاتورةُ بالخمس عشرة؛ مرتجعٌ بثلاثٍ بلا اسمٍ يخرج من LOT-B — <b>وكان يُرفض «قبل أن يُتتبَّع»</b> لأوّل صفٍّ يلقاه.
    /// </summary>
    [Fact]
    public async Task APreTrackingReceipt_DoesNotBlockWhatTheBatchesCover()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LATE", "شراب", tracked: false);
        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 1, 1));
        order.AddLine("بضاعة", 15m, 10m, product);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();
        var first = GoodsReceipt.CreateDraft(_companyId, _supplierId, order.Id, _warehouseId, new DateOnly(2026, 1, 2));
        first.AddLine("بضاعة", 15m, 5m, 10m, product, null, null);
        await new GoodsReceiptPostingService(_db).PostAsync(first);
        await _db.SaveChangesAsync();
        await SellAsync(product, 5m, null);
        (await _db.Products.SingleAsync(x => x.Id == product)).SetTracking(InventoryTracking.Batch, currentOnHand: 0m);
        await _db.SaveChangesAsync();
        var second = GoodsReceipt.CreateDraft(_companyId, _supplierId, order.Id, _warehouseId, new DateOnly(2026, 3, 2));
        second.AddLine("بضاعة", 15m, 10m, 10m, product, "LOT-B", new DateOnly(2027, 6, 1));
        await new GoodsReceiptPostingService(_db).PostAsync(second);
        await _db.SaveChangesAsync();
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 3, 5), $"SUP-{product}-{++_supplierReferences}", vatDeductible: true);
        invoice.LinkPurchaseOrder(order.Id);
        invoice.AddLine("بضاعة", 15m, 10m, product);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
        _invoiceId = invoice.Id;

        var note = await ReturnAsync(product, 3m);

        var line = Assert.Single(await ReturnLinesAsync(note));
        Assert.Equal("LOT-B", line.BatchNumber);
        Assert.Equal(7m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary>
    /// <b>والشاشةُ لا تُخمِّن</b> — باني المسودّة (ممرُّ الشاشة الوحيد) يرفض صنفاً متتبَّعاً بلا دفعةٍ مسمّاةٍ وأمامه دفعتان،
    /// <b>ويُسمّيهما بكمّيّتيهما</b> فيعرف المستخدمُ ما يكتب؛ وكان المرتجعُ يخرج من أقدم ما استُلم مهما كان الذي رُدّ فعلاً.
    /// </summary>
    [Fact]
    public async Task TheScreenPath_RefusesAnUnnamedReturnBetweenTwoBatches_NamingThem()
    {
        var product = await ReceivedAcrossTwoBatchesAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() => new DebitNoteDraftBuilder(_db).BuildAsync(
            _companyId, _invoiceId, new DateOnly(2026, 6, 5), "مرتجع", [new RequestedReturn(0, 3m)]));

        Assert.Contains("حدِّد الدفعةَ المُرجَعة", ex.Message, StringComparison.Ordinal);
        Assert.Contains("LOT-A (", ex.Message, StringComparison.Ordinal);
        Assert.Contains("LOT-B (", ex.Message, StringComparison.Ordinal);
    }

    /// <summary><b>ودفعةٌ واحدةٌ باقيةٌ لا تُسأل</b> — بيعت LOT-A كلُّها فلا التباس، والمرتجعُ بلا اسمٍ يخرج من LOT-B.</summary>
    [Fact]
    public async Task TheScreenPath_WithOneBatchLeft_NeedsNoName()
    {
        var product = await ReceivedAcrossTwoBatchesAsync();
        await SellAsync(product, 10m, "LOT-A");

        var draft = await new DebitNoteDraftBuilder(_db).BuildAsync(
            _companyId, _invoiceId, new DateOnly(2026, 6, 5), "مرتجع", [new RequestedReturn(0, 3m)]);
        var note = await new DebitNotePostingService(_db).PostAsync(draft);

        var line = Assert.Single(await ReturnLinesAsync(note));
        Assert.Equal("LOT-B", line.BatchNumber);
        Assert.Equal(7m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary><b>والدفعةُ المسمّاةُ في الشاشة تبلغ الإشعار</b> — LOT-B تُكتب فتخرج منها، وLOT-A لا تُمسّ.</summary>
    [Fact]
    public async Task TheScreenPath_CarriesTheNamedBatchToTheNote()
    {
        var product = await ReceivedAcrossTwoBatchesAsync();

        var draft = await new DebitNoteDraftBuilder(_db).BuildAsync(
            _companyId, _invoiceId, new DateOnly(2026, 6, 5), "مرتجع", [new RequestedReturn(0, 3m, BatchNumber: "LOT-B")]);
        var note = await new DebitNotePostingService(_db).PostAsync(draft);

        Assert.Equal("LOT-B", Assert.Single(note.Lines).BatchNumber);
        Assert.Equal(10m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(7m, await BatchAsync(product, "LOT-B"));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<long> ReceivedAcrossTwoBatchesAsync()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        await PurchaseAsync(product, 20m,
            (10m, "LOT-A", new DateOnly(2027, 1, 1)), (10m, "LOT-B", new DateOnly(2027, 6, 1)));
        return product;
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("صيدلية", "310123456700066", BusinessProfile.Healthcare);
        _companyId = company.Id;
        _warehouseId = (await _db.Warehouses.FirstAsync(w => w.CompanyId == _companyId && w.IsDefault)).Id;
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

    /// <summary>أمرُ شراءٍ بـ<paramref name="orderedQty"/> يُستلم على إذونٍ متتالية، ثمّ فاتورةٌ مربوطةٌ بالأمر بالكمّية كلِّها بسعر ١٠.</summary>
    private async Task PurchaseAsync(long productId, decimal orderedQty,
        params (decimal Qty, string? Batch, DateOnly? Expiry)[] receipts)
    {
        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 1, 1));
        order.AddLine("بضاعة", orderedQty, 10m, productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();
        foreach (var (qty, batch, expiry) in receipts)
        {
            var receipt = GoodsReceipt.CreateDraft(_companyId, _supplierId, order.Id, _warehouseId, new DateOnly(2026, 1, 2));
            receipt.AddLine("بضاعة", orderedQty, qty, 10m, productId, batch, expiry);
            await new GoodsReceiptPostingService(_db).PostAsync(receipt);
            await _db.SaveChangesAsync();
        }

        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 2, 1), $"SUP-{productId}-{++_supplierReferences}", vatDeductible: true);
        invoice.LinkPurchaseOrder(order.Id);
        invoice.AddLine("بضاعة", receipts.Sum(r => r.Qty), 10m, productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
        _invoiceId = invoice.Id;
    }

    private async Task SellAsync(long productId, decimal qty, string? batch)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 1));
        invoice.AddLine("صنف", qty, 20m, productId, batchNumber: batch);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<DebitNote> ReturnAsync(long productId, decimal qty, string? batch = null)
    {
        var note = DebitNote.CreateDraft(_companyId, _supplierId, _invoiceId, new DateOnly(2026, 6, 5), "مرتجع");
        note.AddLine("صنف", qty, 10m, productId, batchNumber: batch);
        return await new DebitNotePostingService(_db).PostAsync(note);
    }

    private async Task<List<StockMovementLine>> ReturnLinesAsync(DebitNote note) =>
        (await _db.StockMovements.AsNoTracking()
            .Where(m => m.SourceDocument == $"DebitNote:{note.NoteNumber}" && m.Type == StockMovementType.Issue)
            .ToListAsync())
        .SelectMany(m => m.Lines).ToList();

    private async Task<decimal> BatchAsync(long productId, string batch) =>
        (await _db.StockBatches.AsNoTracking()
            .Where(b => b.ProductId == productId && b.BatchNumber == batch)
            .Select(b => b.Quantity).ToListAsync()).Sum();
}
