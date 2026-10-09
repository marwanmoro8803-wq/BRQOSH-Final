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
/// (جاهزيّةُ الإطلاق · ح-١٠٤ · <c>order-to-cash-procure-to-pay-inventory#7</c>) <b>أمرُ البيع لصنفٍ متتبَّعٍ يسحب الأقدمَ انتهاءً.</b>
///
/// <para>كان <c>SalesOrderService.CreateInvoiceFromOrderAsync</c> يبني سطرَ الفاتورة بلا هويّة دفعة، والمَخنَقُ يرفض كلَّ سطرٍ
/// متتبَّعٍ بلاها — <b>فلا يُفوتَر دواءٌ ولا غذاءٌ من أمرِ بيعٍ أبداً</b>.</para>
///
/// <para><b>والحلّ:</b> التخصيصُ بالأقدم انتهاءً (<c>BatchAllocationService</c>) لكلّ صنفٍ مرّةً بمجموع كمّياته، ثمّ توزيعُه على سطور
/// الأمر بالترتيب. <b>والمنتهيةُ لا تُخصَّص، ونقصُ الساري يُرفض بلا فاتورةٍ ولا وسمٍ على الأمر.</b></para>
/// </summary>
public sealed class ASalesOrderOfATrackedProductDrawsTheEarliestExpiringBatchesTests : IDisposable
{
    private static readonly DateOnly InvoiceDate = new(2026, 6, 15);

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private long _customerId;
    private long _supplierId;

    public ASalesOrderOfATrackedProductDrawsTheEarliestExpiringBatchesTests()
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
    /// <b>الأمرُ يفوق أقدمَ دفعة</b>: A تنتهي أبكر (٥) وB لاحقاً (١٠)، والأمرُ ٨ ⟵ سطرا صرفٍ A=٥ وB=٣، والرصيدان ٠ و٧.
    /// </summary>
    [Fact]
    public async Task AnOrderBeyondTheEarliestBatch_DrawsItFullyThenTheNext()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        await ReceiveAsync(product, 5m, "LOT-A", new DateOnly(2026, 9, 1));
        await ReceiveAsync(product, 10m, "LOT-B", new DateOnly(2027, 1, 1));
        var order = await ConfirmedOrderAsync((product, 8m));

        var invoice = await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(_companyId, order.Id, InvoiceDate);

        var lines = await IssueLinesAsync(invoice);
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.BatchNumber == "LOT-A" && l.Quantity == 5m);
        Assert.Contains(lines, l => l.BatchNumber == "LOT-B" && l.Quantity == 3m);
        Assert.Equal(0m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(7m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary>
    /// <b>والمنتهيةُ تُتخطّى ولو كانت الأكثرَ رصيداً</b> — X انتهت قبل تاريخ الفاتورة وفيها ٥٠، فلا تُمسّ، والأمرُ كلُّه من Y.
    /// </summary>
    [Fact]
    public async Task AnExpiredBatch_IsSkippedEvenWhenItHoldsTheMostStock()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        await ReceiveAsync(product, 50m, "LOT-X", new DateOnly(2026, 3, 1));
        await ReceiveAsync(product, 10m, "LOT-Y", new DateOnly(2027, 1, 1));
        var order = await ConfirmedOrderAsync((product, 8m));

        var invoice = await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(_companyId, order.Id, InvoiceDate);

        var line = Assert.Single(await IssueLinesAsync(invoice));
        Assert.Equal("LOT-Y", line.BatchNumber);
        Assert.Equal(8m, line.Quantity);
        Assert.Equal(50m, await BatchAsync(product, "LOT-X"));
        Assert.Equal(2m, await BatchAsync(product, "LOT-Y"));
    }

    /// <summary>
    /// <b>ونقصُ الساري يُرفض بلا أثر</b> — لا فاتورةَ مُرحَّلة، والأمرُ يبقى «مؤكَّداً» قابلاً للفوترة، والدفعاتُ كما كانت.
    /// </summary>
    [Fact]
    public async Task InsufficientUnexpiredStock_IsRefusedAndLeavesNoTrace()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        await ReceiveAsync(product, 5m, "LOT-A", new DateOnly(2027, 1, 1));
        await ReceiveAsync(product, 40m, "LOT-X", new DateOnly(2026, 3, 1));
        var order = await ConfirmedOrderAsync((product, 8m));

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new SalesOrderService(_db).CreateInvoiceFromOrderAsync(_companyId, order.Id, InvoiceDate));

        // ‏من المُخصِّص لا من المَخنَق: رفضٌ باسم نقص الساري، لا باسم «بلا هويّة».
        Assert.Contains("دفعاتٌ ساريةٌ غير كافية", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.SalesInvoices.AnyAsync(i => i.Status == SalesInvoiceStatus.Posted));
        var stored = await _db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(SalesOrderStatus.Confirmed, stored.Status);
        Assert.Null(stored.SalesInvoiceId);
        Assert.Equal(5m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(40m, await BatchAsync(product, "LOT-X"));
    }

    /// <summary>
    /// <b>وسطران لصنفٍ واحدٍ لا يسحبان الدفعةَ مرّتين</b> — ٣ و٤ على A(٥) وB(١٠) ⟵ A=٥ وB=٢ في المجموع، والرصيدان ٠ و٨.
    /// </summary>
    [Fact]
    public async Task TwoOrderLinesOfOneProduct_DoNotDrawTheSameBatchTwice()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        await ReceiveAsync(product, 5m, "LOT-A", new DateOnly(2026, 9, 1));
        await ReceiveAsync(product, 10m, "LOT-B", new DateOnly(2027, 1, 1));
        var order = await ConfirmedOrderAsync((product, 3m), (product, 4m));

        var invoice = await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(_companyId, order.Id, InvoiceDate);

        var lines = await IssueLinesAsync(invoice);
        Assert.Equal(5m, lines.Where(l => l.BatchNumber == "LOT-A").Sum(l => l.Quantity));
        Assert.Equal(2m, lines.Where(l => l.BatchNumber == "LOT-B").Sum(l => l.Quantity));
        Assert.Equal(0m, await BatchAsync(product, "LOT-A"));
        Assert.Equal(8m, await BatchAsync(product, "LOT-B"));
    }

    /// <summary>
    /// <b>وتحت FIFO، بخلطٍ مع غير المتتبَّع، تُكلَّف كلُّ دفعةٍ من طبقتها وتطابق الفاتورةُ الأمر</b> — A خمسٌ بعشرة وB عشرٌ باثني عشر،
    /// وضمادةٌ عشرٌ بعشرة؛ أمرٌ بثمانٍ من الدواء واثنتين من الضمادة ⟵ تكلفةُ المبيعات ٥٠ + ٣٦ + ٢٠ = ١٠٦، والوعاءُ ٢٠٠ كالأمر،
    /// والأمرُ «مُفوتَر» بفاتورته. <b>ويمرّ السطرُ الثاني للدواء على طبقةٍ استنفدها الأوّل</b> — وذاك ح-١٠٣.
    /// </summary>
    [Fact]
    public async Task UnderFifo_AMixedOrder_CostsEachBatchFromItsLayer_AndMatchesTheOrder()
    {
        await SetupAsync();
        (await _db.Companies.SingleAsync(c => c.Id == _companyId)).SetCostingMethod(InventoryCostingMethod.Fifo);
        await _db.SaveChangesAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        var plain = await ProductAsync("P-PLAIN", "ضمادة", tracked: false);
        await ReceiveAsync(product, 5m, "LOT-A", new DateOnly(2026, 9, 1), unitCost: 10m);
        await ReceiveAsync(product, 10m, "LOT-B", new DateOnly(2027, 1, 1), unitCost: 12m);
        await ReceiveAsync(plain, 10m, null, null, unitCost: 10m);
        var order = await ConfirmedOrderAsync((product, 8m), (plain, 2m));

        var invoice = await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(_companyId, order.Id, InvoiceDate);

        Assert.Equal(106m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold));
        Assert.Equal(order.Subtotal, invoice.Subtotal);
        _db.ChangeTracker.Clear();
        var stored = await _db.SalesOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(SalesOrderStatus.Invoiced, stored.Status);
        Assert.Equal(invoice.Id, stored.SalesInvoiceId);
    }

    /// <summary>
    /// <b>وخدمةٌ موسومةٌ بالتتبّع لا تُخصَّص</b> — الترحيلُ لا يصرفها لأنّها لا تُخزَّن، فالتخصيصُ لها يرفض أمراً كان يُفوتَر:
    /// «دفعاتٌ ساريةٌ غير كافية» لما لا دفعاتَ له أصلاً.
    /// </summary>
    [Fact]
    public async Task ATrackedServiceProduct_IsNotAllocated()
    {
        await SetupAsync();
        var service = Product.Create(_companyId, "S-LOT", "استشارة", "جلسة", ProductType.Service, salePrice: 20m);
        service.SetTracking(InventoryTracking.Batch, currentOnHand: 0m);
        _db.Products.Add(service);
        await _db.SaveChangesAsync();
        var order = await ConfirmedOrderAsync((service.Id, 2m));

        var invoice = await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(_companyId, order.Id, InvoiceDate);

        var line = Assert.Single(invoice.Lines);
        Assert.Null(line.BatchNumber);
        Assert.Equal(2m, line.Quantity);
    }

    /// <summary>
    /// <b>ونقصٌ دون ٠٫٠٠٠١ يُرفض باسم الصنف</b> — المُخصِّصُ يقبله (هامشُه ٠٫٠٠٠١) فتنفد الدفعاتُ قبل أن يكتمل السطر، وكان يرمي
    /// خطأَ فهرسٍ إنجليزيّاً خامّاً تعرضه الشاشة. ولا فاتورةَ ولا وسم.
    /// </summary>
    [Fact]
    public async Task AShortfallBelowTheAllocatorsTolerance_IsRefusedByName()
    {
        await SetupAsync();
        var product = await ProductAsync("P-LOT", "دواء", tracked: true);
        await ReceiveAsync(product, 8m, "LOT-A", new DateOnly(2027, 1, 1));
        var order = await ConfirmedOrderAsync((product, 8.0001m));

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new SalesOrderService(_db).CreateInvoiceFromOrderAsync(_companyId, order.Id, InvoiceDate));

        Assert.Contains("دواء", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.SalesInvoices.AnyAsync(i => i.Status == SalesInvoiceStatus.Posted));
        Assert.Equal(8m, await BatchAsync(product, "LOT-A"));
    }

    /// <summary><b>وغيرُ المتتبَّع يبقى كما كان</b> — سطرٌ واحدٌ بلا دفعة، ولا دفعةَ تُنشأ.</summary>
    [Fact]
    public async Task AnUntrackedProduct_IsInvoicedAsBefore()
    {
        await SetupAsync();
        var product = await ProductAsync("P-PLAIN", "ضمادة", tracked: false);
        await ReceiveAsync(product, 10m, null, null);
        var order = await ConfirmedOrderAsync((product, 4m));

        var invoice = await new SalesOrderService(_db).CreateInvoiceFromOrderAsync(_companyId, order.Id, InvoiceDate);

        var line = Assert.Single(await IssueLinesAsync(invoice));
        Assert.Null(line.BatchNumber);
        Assert.Equal(4m, line.Quantity);
        Assert.False(await _db.StockBatches.AnyAsync(b => b.ProductId == product));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

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

    private async Task<SalesOrder> ConfirmedOrderAsync(params (long Product, decimal Qty)[] lines)
    {
        var order = SalesOrder.CreateDraft(_companyId, _customerId, new DateOnly(2026, 6, 1));
        foreach (var (product, qty) in lines)
            order.AddLine("صنف", qty, 20m, product);
        return await new SalesOrderService(_db).ConfirmAsync(order);
    }

    private async Task<List<StockMovementLine>> IssueLinesAsync(SalesInvoice invoice) =>
        (await _db.StockMovements.AsNoTracking()
            .Where(m => m.SourceDocument == $"SalesInvoice:{invoice.InvoiceNumber}" && m.Type == StockMovementType.Issue)
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
