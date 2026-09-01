using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-230 · ط1-٣) <b>تتبّعُ الدفعة والصلاحية والرقم التسلسليّ.</b>
///
/// <para><b>ما كان:</b> صفرُ ذكرٍ للدفعة أو الصلاحية أو الرقم التسلسليّ في النطاق كلِّه.
/// فصيدليّةٌ تبيع دواءً منتهيَ الصلاحية <b>ولا شيء في النظام يعلم</b> — لا لأنّ حارساً
/// عَمِيَ بل لأنّ المعلومةَ نفسَها لم تكن تُخزَّن.</para>
///
/// <para><b>وموضعُ الحراسة قرارٌ لا صدفة:</b> كلُّ تغيّرٍ في كمّية المخزون يمرّ بدالّةٍ
/// خاصّةٍ واحدة في <c>InventoryService</c> لها اثنا عشر مستدعياً. فجُعلت هويّةُ الدفعة
/// <b>وسيطاً بلا قيمةٍ افتراضية</b> — فأجبر المُصرِّفُ الاثني عشر كلَّهم على أن يُقرّروا.
/// <b>وحارسٌ يُفرض في زمن الترجمة لا يُنسى في مسارٍ لم يُفكَّر فيه.</b></para>
/// </summary>
public class BatchTrackingTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;

    public BatchTrackingTests()
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

    // ── ما يمنعه التتبّع ────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ: دواءٌ منتهي الصلاحية لا يُباع.</b>
    ///
    /// <para>وهو السببُ الذي يجعل هذه الميزةَ شرطاً لقطاعَي الصحّة والأغذية لا تحسيناً
    /// فيهما: بيعُ منتهي الصلاحية <b>مسؤوليةٌ نظاميةٌ وجنائيةٌ على المنشأة</b>، ولا يكفي
    /// أن يتذكّره موظّفُ المستودع.</para>
    /// </summary>
    [Fact]
    public async Task AnExpiredBatch_CannotBeSold()
    {
        var product = await TrackedProductAsync(InventoryTracking.Batch);
        await ReceiveAsync(product, 100m, "LOT-A", new DateOnly(2026, 2, 1));

        var invoice = SalesInvoice.CreateDraft(_companyId, await CustomerIdAsync(), new DateOnly(2026, 6, 1));
        invoice.AddLine("دواء", 5m, 20m, product.Id, batchNumber: "LOT-A");

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new SalesInvoicePostingService(_db).PostAsync(invoice));

        Assert.Contains("منتهيةُ الصلاحية", error.Message, StringComparison.Ordinal);
        Assert.Contains("LOT-A", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والاتّجاه الآخر — وهو ما يجعل الحارسَ حارساً لا سدّاً:</b> الدفعةُ السارية تُباع.
    ///
    /// <para>وبغير هذا الاختبار يمرّ حارسٌ يرفض <b>كلَّ</b> بيع، ويُقرأ رفضُه نجاحاً.</para>
    /// </summary>
    [Fact]
    public async Task AValidBatch_IsSoldAndDrawnDown()
    {
        var product = await TrackedProductAsync(InventoryTracking.Batch);
        await ReceiveAsync(product, 100m, "LOT-B", new DateOnly(2027, 1, 1));

        var invoice = SalesInvoice.CreateDraft(_companyId, await CustomerIdAsync(), new DateOnly(2026, 6, 1));
        invoice.AddLine("دواء", 30m, 20m, product.Id, batchNumber: "LOT-B");
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var batch = await _db.StockBatches.SingleAsync(b => b.BatchNumber == "LOT-B");
        Assert.Equal(70m, batch.Quantity);

        LedgerIdentities.Assert(await LedgerIdentities.TrackedBatchesAsync(_db, _companyId));
    }

    /// <summary>
    /// <b>ولا يُباع من دفعةٍ لا وجودَ لها</b> — ولو كان الرصيدُ الكليّ كافياً.
    ///
    /// <para>وهذا ما يفرّق التتبّعَ الحقيقيّ من حقلٍ نصّيٍّ على الشاشة: الرقمُ يُطابَق
    /// بالرصيد، <b>لا يُسجَّل ويُنسى</b>.</para>
    /// </summary>
    [Fact]
    public async Task SellingFromAnUnknownBatch_IsRefused()
    {
        var product = await TrackedProductAsync(InventoryTracking.Batch);
        await ReceiveAsync(product, 100m, "LOT-C", new DateOnly(2027, 1, 1));

        var invoice = SalesInvoice.CreateDraft(_companyId, await CustomerIdAsync(), new DateOnly(2026, 6, 1));
        invoice.AddLine("دواء", 5m, 20m, product.Id, batchNumber: "LOT-MISSING");

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => new SalesInvoicePostingService(_db).PostAsync(invoice));

        Assert.Contains("LOT-MISSING", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وصنفٌ متتبَّعٌ لا يدخل ولا يخرج بلا هويّة.</b>
    ///
    /// <para>وهو الحارسُ الذي يجعل كلَّ مسارٍ لم يُحوَّل بعد <b>يرفض رفضاً مسمّى</b> بدل
    /// أن يبتلع البضاعةَ بلا رقم. <b>والصمتُ هنا أخطرُ من الرفض</b>: المستخدم يظنّ
    /// التتبّعَ عاملاً وهو مثقوب.</para>
    /// </summary>
    [Fact]
    public async Task ATrackedProduct_CannotMoveWithoutABatch()
    {
        var product = await TrackedProductAsync(InventoryTracking.Batch);

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => ReceiveAsync(product, 10m, batchNumber: null, expiry: null));

        Assert.Contains("لا يدخل ولا يخرج بلا رقم", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>والعكس: صنفٌ غيرُ متتبَّعٍ يرفض رقمَ الدفعة.</b>
    ///
    /// <para>لأنّ قيمةً تُدخَل ولا تُخزَّن <b>أسوأُ من قيمةٍ لا تُدخَل</b>: المستخدم يراها
    /// في الشاشة فيحسبها محفوظة، ولا حارسَ يخبره أنّها ذهبت.</para>
    /// </summary>
    [Fact]
    public async Task AnUntrackedProduct_RefusesABatchNumber()
    {
        var product = await TrackedProductAsync(InventoryTracking.None);

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => ReceiveAsync(product, 10m, "LOT-X", null));

        Assert.Contains("لا يُتتبَّع بالدفعة", error.Message, StringComparison.Ordinal);
    }

    // ── الرقمُ التسلسليّ ────────────────────────────────────────────────────

    /// <summary>
    /// <b>الرقمُ التسلسليّ وحدةٌ واحدة</b> — ولا يقبل كمّيةً غيرَها.
    ///
    /// <para>فهو <b>ليس تسميةً أخرى للدفعة</b>: جهازٌ بعينه لا يُستلم منه ثلاثة.</para>
    /// </summary>
    [Fact]
    public async Task ASerialLine_MustBeExactlyOneUnit()
    {
        var product = await TrackedProductAsync(InventoryTracking.Serial);

        var error = await Assert.ThrowsAsync<AccountingException>(
            () => ReceiveAsync(product, 3m, "SN-001", null));

        Assert.Contains("وحدةٌ واحدة", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ولا يتكرّر الرقمُ التسلسليّ ما دامت وحدتُه قائمة.</b>
    ///
    /// <para>وقيدُ التفرّد في القاعدة يمنع التكرار <b>في المستودع الواحد</b> وحدَه —
    /// فالمنعُ على مستوى المنشأة يلزمه فحصٌ صريح، وهو هذا.</para>
    /// </summary>
    [Fact]
    public async Task ASerialNumber_IsNotReusedWhileItsUnitExists()
    {
        var product = await TrackedProductAsync(InventoryTracking.Serial);
        await ReceiveAsync(product, 1m, "SN-777", null);

        var second = await NewWarehouseAsync("W-2");
        var error = await Assert.ThrowsAsync<AccountingException>(
            () => ReceiveAsync(product, 1m, "SN-777", null, second));

        Assert.Contains("لا يتكرّر", error.Message, StringComparison.Ordinal);
    }

    // ── نمطُ التتبّع لا يُبدَّل والمخزونُ قائم ──────────────────────────────

    /// <summary>
    /// <b>ونمطُ التتبّع لا يتغيّر ولصنفٍ رصيد.</b>
    ///
    /// <para>لأنّ المئةَ التي دخلت بلا هويّةٍ لا تكتسبها بقرار: تصير <b>بضاعةً بلا رقمٍ
    /// ولا صلاحية</b>، وتنكسر <c>هـ-٩</c> في اللحظة نفسِها. وهو مذهبُ <c>م1-٢٢</c> بعينه.</para>
    /// </summary>
    [Fact]
    public async Task TrackingMode_CannotChangeWhileStockExists()
    {
        var product = await TrackedProductAsync(InventoryTracking.None);
        await ReceiveAsync(product, 40m, null, null);

        Assert.Throws<AccountingException>(() =>
            product.SetTracking(InventoryTracking.Batch, currentOnHand: 40m));

        product.SetTracking(InventoryTracking.Batch, currentOnHand: 0m);
        Assert.Equal(InventoryTracking.Batch, product.Tracking);
    }

    // ── الهويّة هـ-٩ ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>ودفعتان من صنفٍ واحد: مجموعُهما هو الرصيد.</b>
    ///
    /// <para>وهذه هي <c>هـ-٩</c> على أوسع حالةٍ يمرّ بها المستودع: استلامان بدفعتين
    /// وصرفٌ من إحداهما. <b>وانفصالُهما يعني بضاعةً في المستودع لا يعرف النظامُ متى
    /// تنتهي</b>.</para>
    /// </summary>
    [Fact]
    public async Task TwoBatches_SumToTheStockBalance()
    {
        var product = await TrackedProductAsync(InventoryTracking.Batch);
        await ReceiveAsync(product, 60m, "LOT-1", new DateOnly(2027, 1, 1));
        await ReceiveAsync(product, 40m, "LOT-2", new DateOnly(2028, 1, 1));

        var invoice = SalesInvoice.CreateDraft(_companyId, await CustomerIdAsync(), new DateOnly(2026, 6, 1));
        invoice.AddLine("دواء", 25m, 20m, product.Id, batchNumber: "LOT-2");
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var identity = await LedgerIdentities.TrackedBatchesAsync(_db, _companyId);
        Assert.True(identity.Holds, identity.ToString());
        Assert.Equal(75m, identity.Subsidiary);
    }

    /// <summary>
    /// <b>وإلغاءُ الاستلام يُعيد الدفعةَ بعينها</b> — لا كمّيةً بلا هويّة.
    ///
    /// <para>وهذا هو سببُ بقاء رقم الدفعة على سطر الحركة بعد الترحيل: بدونه لا يُعرف
    /// عند العكس <b>أيَّ دفعةٍ تُنقص</b>، فتُردّ الكمّيةُ إلى المجمل وتنكسر <c>هـ-٩</c>.</para>
    /// </summary>
    [Fact]
    public async Task CancellingAReceipt_ReturnsTheSameBatch()
    {
        var product = await TrackedProductAsync(InventoryTracking.Batch);
        var receipt = await ReceiveAsync(product, 50m, "LOT-R", new DateOnly(2027, 1, 1));

        await new GoodsReceiptCancellationService(_db)
            .CancelAsync(_companyId, receipt.Id, new DateOnly(2026, 1, 5), "admin");

        var batch = await _db.StockBatches.SingleAsync(b => b.BatchNumber == "LOT-R");
        Assert.Equal(0m, batch.Quantity);

        var identity = await LedgerIdentities.TrackedBatchesAsync(_db, _companyId);
        Assert.True(identity.Holds, identity.ToString());
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<Product> TrackedProductAsync(InventoryTracking tracking)
    {
        await SetupAsync();
        var product = Product.Create(_companyId, $"P-{tracking}", "دواء", "علبة",
            ProductType.Stockable, salePrice: 20m);
        product.SetTracking(tracking, currentOnHand: 0m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    private async Task<GoodsReceipt> ReceiveAsync(
        Product product, decimal qty, string? batchNumber, DateOnly? expiry, long? warehouseId = null)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.CompanyId == _companyId);
        if (supplier is null)
        {
            supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
            _db.Suppliers.Add(supplier);
            await _db.SaveChangesAsync();
        }

        var order = PurchaseOrder.CreateDraft(_companyId, supplier.Id, new DateOnly(2026, 1, 1));
        order.AddLine("بضاعة", qty, 10m, product.Id);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();

        var receipt = GoodsReceipt.CreateDraft(_companyId, supplier.Id, order.Id,
            warehouseId ?? _warehouseId, new DateOnly(2026, 1, 2));
        receipt.AddLine("بضاعة", qty, qty, 10m, product.Id, batchNumber, expiry);
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);
        await _db.SaveChangesAsync();
        return receipt;
    }

    private async Task<long> NewWarehouseAsync(string code)
    {
        var warehouse = Warehouse.Create(_companyId, code, "مستودع ثانٍ", isDefault: false);
        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync();
        return warehouse.Id;
    }

    private async Task<long> CustomerIdAsync()
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.CompanyId == _companyId);
        if (customer is null)
        {
            customer = Customer.Create(_companyId, "عميل", "311111111100003");
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
        }
        return customer.Id;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("صيدلية القياس", "310123456700066", BusinessProfile.Healthcare);
        _companyId = company.Id;
        _warehouseId = (await _db.Warehouses.FirstAsync(w => w.CompanyId == _companyId)).Id;
    }
}
