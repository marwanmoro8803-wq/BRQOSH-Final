using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Manufacturing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٠١ · <c>manufacturing#6</c>) <b>الخامةُ المتتبَّعةُ تُصرف بالأقدم انتهاءً، والتامُّ المتتبَّعُ يدخل بدفعته.</b>
///
/// <para>كان <c>CompleteAsync</c> يُرحّل سطورَ الصرف والاستلام بلا هويّة دفعة، والمَخنَقُ يرفض كلَّ سطرٍ متتبَّعٍ بلا هويّة —
/// <b>فلا يمرّ أمرُ إنتاجٍ واحدٌ لخامةٍ أو منتجٍ يُتتبَّع بالدفعة</b>: مصنعُ غذاءٍ أو دواءٍ لا يُكمل أمراً. وقيس على HEAD:
/// «الصنف 'فاكهة' يُتتبَّع بـالدفعة ولا يحمل هذا السطرُ هويّةً» للخامة، ومثلُه للتامّ.</para>
/// </summary>
public class ABatchTrackedOrderDrawsAndYieldsByBatchTests : IDisposable
{
    private static readonly DateOnly CompletedOn = new(2026, 4, 5);

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;

    public ABatchTrackedOrderDrawsAndYieldsByBatchTests()
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
    /// <b>الأقدمُ انتهاءً أوّلاً، وسطرٌ لكلّ دفعة</b> — خمسةَ عشرَ من دفعتين: العشرُ كلُّها من التي تنتهي في يونيو،
    /// والخمسُ الباقيةُ من التي تنتهي في ديسمبر، ولو أُدخلت المتأخّرةُ أوّلاً.
    /// </summary>
    [Fact]
    public async Task TrackedMaterials_AreDrawnByEarliestExpiry_OneLinePerBatch()
    {
        await SetupAsync();
        var raw = await TrackedRawAsync("RM-101", "فاكهة",
            ("B-LATE", 10m, new DateOnly(2026, 12, 31)), ("B-EARLY", 10m, new DateOnly(2026, 6, 30)));
        var finished = await PlainProductAsync("FG-101", "عصير");
        var orderId = await ReleasedOrderAsync(finished, raw, perUnit: 3m, planned: 5m);

        await new WorkOrderService(_db).CompleteAsync(_companyId, orderId, 5m, "admin", CompletedOn);

        var issued = await LinesAsync(orderId, StockMovementType.Issue);
        Assert.Equal(2, issued.Count);
        Assert.Contains(issued, l => l.ProductId == raw && l.BatchNumber == "B-EARLY" && l.Quantity == 10m);
        Assert.Contains(issued, l => l.ProductId == raw && l.BatchNumber == "B-LATE" && l.Quantity == 5m);
        Assert.Equal(0m, await BatchQuantityAsync(raw, "B-EARLY"));
        Assert.Equal(5m, await BatchQuantityAsync(raw, "B-LATE"));
        Assert.Equal(WorkOrderStatus.Completed,
            (await _db.WorkOrders.AsNoTracking().SingleAsync(w => w.Id == orderId)).Status);
    }

    /// <summary>
    /// <b>المنتهيةُ لا تُصرف</b>، ونقصُ الساري يُرفض باسم الخامة — <b>ولا حركةَ ولا قيدَ</b>: يبقى الأمرُ صادراً كما كان.
    /// </summary>
    [Fact]
    public async Task AnExpiredBatch_IsNotDrawn_AndTooLittleUnexpired_IsRefusedByName()
    {
        await SetupAsync();
        var raw = await TrackedRawAsync("RM-102", "حليب",
            ("B-OLD", 10m, new DateOnly(2026, 3, 31)), ("B-NEW", 10m, new DateOnly(2026, 12, 31)));
        var finished = await PlainProductAsync("FG-102", "جبن");
        var orderId = await ReleasedOrderAsync(finished, raw, perUnit: 3m, planned: 5m);

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new WorkOrderService(_db).CompleteAsync(_companyId, orderId, 5m, "admin", CompletedOn));

        Assert.Contains("حليب", ex.Message, StringComparison.Ordinal);
        Assert.Contains("منتهيةُ الصلاحية", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.Empty(await LinesAsync(orderId, StockMovementType.Issue));
        Assert.Equal(10m, await BatchQuantityAsync(raw, "B-OLD"));
        Assert.Equal(10m, await BatchQuantityAsync(raw, "B-NEW"));
        Assert.Equal(WorkOrderStatus.Released,
            (await _db.WorkOrders.AsNoTracking().SingleAsync(w => w.Id == orderId)).Status);
    }

    /// <summary><b>التامُّ المتتبَّعُ يدخل بدفعته وصلاحيتها</b> — دفعةٌ في المستودع بالكمّيّة المنتَجة.</summary>
    [Fact]
    public async Task ATrackedFinishedProduct_IsReceivedIntoItsBatch()
    {
        await SetupAsync();
        var raw = await PlainRawAsync("RM-103", "سكّر", 20m);
        var finished = await TrackedFinishedAsync("FG-103", "مربّى");
        var orderId = await ReleasedOrderAsync(finished, raw, perUnit: 2m, planned: 2m);

        await new WorkOrderService(_db).CompleteAsync(_companyId, orderId, 2m, "admin", CompletedOn,
            outputBatchNumber: " LOT-7 ", outputExpiry: new DateOnly(2027, 1, 31));

        var received = Assert.Single(await LinesAsync(orderId, StockMovementType.Receipt));
        Assert.Equal(finished, received.ProductId);
        Assert.Equal("LOT-7", received.BatchNumber);
        Assert.Equal(new DateOnly(2027, 1, 31), received.ExpiryDate);
        var batch = await _db.StockBatches.AsNoTracking()
            .SingleAsync(b => b.ProductId == finished && b.BatchNumber == "LOT-7");
        Assert.Equal(2m, batch.Quantity);
        Assert.Equal(new DateOnly(2027, 1, 31), batch.ExpiryDate);
        Assert.Equal(_warehouseId, batch.WarehouseId);
        Assert.True(await new WorkOrderService(_db).OutputIsTrackedAsync(_companyId, orderId));
    }

    /// <summary>
    /// <b>تامٌّ متتبَّعٌ بلا دفعةٍ يُرفض باسمه وباسم الأمر — قبل أيّ صرف</b>: لا تُستهلك خامةٌ لإنتاجٍ لا يُستلم.
    /// </summary>
    [Fact]
    public async Task ATrackedFinishedProductWithoutABatch_IsRefusedByName_BeforeAnyMovement()
    {
        await SetupAsync();
        var raw = await PlainRawAsync("RM-104", "دقيق", 20m);
        var finished = await TrackedFinishedAsync("FG-104", "خبز");
        var orderId = await ReleasedOrderAsync(finished, raw, perUnit: 2m, planned: 2m);
        var number = (await _db.WorkOrders.AsNoTracking().SingleAsync(w => w.Id == orderId)).OrderNumber!;

        foreach (var blank in new string?[] { null, "   " })
        {
            var ex = await Assert.ThrowsAsync<AccountingException>(() =>
                new WorkOrderService(_db).CompleteAsync(_companyId, orderId, 2m, "admin", CompletedOn,
                    outputBatchNumber: blank));
            Assert.Contains("خبز", ex.Message, StringComparison.Ordinal);
            Assert.Contains(number, ex.Message, StringComparison.Ordinal);
            Assert.Contains("دفعة الإنتاج", ex.Message, StringComparison.Ordinal);
        }

        _db.ChangeTracker.Clear();
        Assert.Empty(await _db.StockMovements.AsNoTracking()
            .Where(m => m.SourceDocument == $"WorkOrder:{orderId}").ToListAsync());
        Assert.Equal(20m, await OnHandAsync(raw));
    }

    /// <summary>
    /// <b>ودفعةٌ لتامٍّ لا يُتتبَّع تُرفض</b> — فلا تُكتب هويّةٌ لا يحفظها أحد. <b>ويرفضها المَخنَقُ نفسُه</b>
    /// (<c>ApplyBatchDeltaAsync</c>)، والأمرُ يمرّ بها فيبقى الرفضُ؛ حارسٌ ثانٍ في الأمر لم يعضّ فأُزيل.
    /// </summary>
    [Fact]
    public async Task ABatchForAnUntrackedFinishedProduct_IsRefused()
    {
        await SetupAsync();
        var raw = await PlainRawAsync("RM-105", "قطن", 20m);
        var finished = await PlainProductAsync("FG-105", "قماش");
        var orderId = await ReleasedOrderAsync(finished, raw, perUnit: 2m, planned: 2m);

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new WorkOrderService(_db).CompleteAsync(_companyId, orderId, 2m, "admin", CompletedOn,
                outputBatchNumber: "LOT-X"));

        Assert.Contains("قماش", ex.Message, StringComparison.Ordinal);
        Assert.Contains("لا يُتتبَّع", ex.Message, StringComparison.Ordinal);
        Assert.False(await new WorkOrderService(_db).OutputIsTrackedAsync(_companyId, orderId));
    }

    /// <summary>
    /// <b>وإلغاءُ الأمر يُعيد كلَّ دفعةٍ بعينها</b> — الخامةُ إلى دفعتيها، ودفعةُ الإنتاج تفرغ.
    /// </summary>
    [Fact]
    public async Task CancellingTheOrder_ReturnsEachBatch()
    {
        await SetupAsync();
        var raw = await TrackedRawAsync("RM-106", "عنب",
            ("B-LATE", 10m, new DateOnly(2026, 12, 31)), ("B-EARLY", 10m, new DateOnly(2026, 6, 30)));
        var finished = await TrackedFinishedAsync("FG-106", "دبس");
        var orderId = await ReleasedOrderAsync(finished, raw, perUnit: 3m, planned: 5m);
        var service = new WorkOrderService(_db);
        await service.CompleteAsync(_companyId, orderId, 5m, "admin", CompletedOn, outputBatchNumber: "LOT-9");

        await service.CancelAsync(_companyId, orderId, "admin", new DateOnly(2026, 4, 7));

        Assert.Equal(10m, await BatchQuantityAsync(raw, "B-EARLY"));
        Assert.Equal(10m, await BatchQuantityAsync(raw, "B-LATE"));
        Assert.Equal(0m, await BatchQuantityAsync(finished, "LOT-9"));
        Assert.Equal(0m, await OnHandAsync(finished));
    }

    /// <summary>
    /// <b>والشاشةُ تسأل عن دفعة الإنتاج وتُمرّرها</b> — قياسٌ بنيويٌّ على المصدر: الصفحةُ تفتح دفاترَ الإنتاج
    /// (<c>AppServices.CreateDb</c>) ولا تُشغَّل في اختبار، على سُنّة ح-٦٦ وح-٩٧.
    /// </summary>
    [Fact]
    public void TheWorkOrdersPage_AsksForTheProductionBatch()
    {
        var code = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "Brqosh.Desktop", "Views", "Pages", "WorkOrdersPage.xaml.cs"));

        // ‏(ح-١٥٩) ‏الصفحةُ تسأل طريقةَ التتبّع — دفعةً أو رقماً لكلّ وحدة.
        var asks = code.IndexOf("OutputTrackingAsync(", StringComparison.Ordinal);
        var passes = code.IndexOf("outputBatchNumber: batch", StringComparison.Ordinal);
        Assert.True(asks >= 0, "الصفحةُ لا تسأل هل التامُّ متتبَّع");
        Assert.True(passes > asks, "الصفحةُ لا تُمرّر دفعةَ الإنتاج بعد السؤال");
        Assert.Contains("outputExpiry: expiry", code, StringComparison.Ordinal);
        Assert.True(code.IndexOf("outputSerials: serials", StringComparison.Ordinal) > asks,
            "الصفحةُ لا تُمرّر الأرقامَ التسلسليّة بعد السؤال");
    }

    // ── (جاهزيّةُ الإطلاق · ح-١٥٩ · manufacturing#6) التامُّ التسلسليّ: رقمٌ لكلّ وحدة ─────────────

    /// <summary>
    /// <b>ثلاثُ وحداتٍ بثلاثة أرقام — ثلاثةُ سطورٍ بوحدةٍ لكلٍّ</b>. وكان التامُّ التسلسليُّ يُكمَل وحدةً في المرّة: أكثرُ يرفضه المَخنَق.
    /// </summary>
    [Fact]
    public async Task ASerialFinishedProduct_IsReceivedOneLinePerNumber()
    {
        await SetupAsync();
        var raw = await PlainRawAsync("RM-159", "هيكل", 20m);
        var finished = await SerialFinishedAsync("FG-159", "مولّد");
        var orderId = await ReleasedOrderAsync(finished, raw, perUnit: 2m, planned: 3m);

        await new WorkOrderService(_db).CompleteAsync(_companyId, orderId, 3m, "admin", CompletedOn,
            outputSerials: [" SN-1 ", "SN-2", "SN-3"]);

        var received = await LinesAsync(orderId, StockMovementType.Receipt);
        Assert.Equal(3, received.Count);
        Assert.All(received, l => Assert.Equal(1m, l.Quantity));
        Assert.Equal(["SN-1", "SN-2", "SN-3"], received.Select(l => l.BatchNumber).Order().ToArray());
        foreach (var serial in new[] { "SN-1", "SN-2", "SN-3" })
            Assert.Equal(1m, await BatchQuantityAsync(finished, serial));
        Assert.Equal(3m, await OnHandAsync(finished));
    }

    /// <summary><b>وبلا أرقامٍ لأكثرَ من وحدةٍ يُرفض باسمه — قبل أيّ صرف</b>.</summary>
    [Fact]
    public async Task SeveralSerialUnitsWithoutNumbers_AreRefusedByName_BeforeAnyMovement()
    {
        await SetupAsync();
        var raw = await PlainRawAsync("RM-160", "هيكل", 20m);
        var finished = await SerialFinishedAsync("FG-160", "مضخّة");
        var orderId = await ReleasedOrderAsync(finished, raw, perUnit: 2m, planned: 2m);

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            new WorkOrderService(_db).CompleteAsync(_companyId, orderId, 2m, "admin", CompletedOn, outputBatchNumber: "SN-9"));

        Assert.Contains("مضخّة", ex.Message, StringComparison.Ordinal);
        Assert.Contains("رقماً لكلّ وحدة", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.Empty(await _db.StockMovements.AsNoTracking()
            .Where(m => m.SourceDocument == $"WorkOrder:{orderId}").ToListAsync());
        Assert.Equal(20m, await OnHandAsync(raw));
    }

    /// <summary><b>والعددُ يطابق الكمّيّة، والرقمُ لا يتكرّر</b> — وإلا رُفض قبل أيّ حركة.</summary>
    [Fact]
    public async Task TooFewOrRepeatedNumbers_AreRefused()
    {
        await SetupAsync();
        var raw = await PlainRawAsync("RM-161", "هيكل", 20m);
        var finished = await SerialFinishedAsync("FG-161", "محرّك");
        var orderId = await ReleasedOrderAsync(finished, raw, perUnit: 2m, planned: 3m);
        var service = new WorkOrderService(_db);

        var few = await Assert.ThrowsAsync<AccountingException>(() =>
            service.CompleteAsync(_companyId, orderId, 3m, "admin", CompletedOn, outputSerials: ["A", "B"]));
        Assert.Contains("رقمٌ لكلّ وحدة", few.Message, StringComparison.Ordinal);

        var repeated = await Assert.ThrowsAsync<AccountingException>(() =>
            service.CompleteAsync(_companyId, orderId, 3m, "admin", CompletedOn, outputSerials: ["A", "B", "A"]));
        Assert.Contains("«A» مكرَّر", repeated.Message, StringComparison.Ordinal);

        _db.ChangeTracker.Clear();
        Assert.Empty(await _db.StockMovements.AsNoTracking()
            .Where(m => m.SourceDocument == $"WorkOrder:{orderId}").ToListAsync());
    }

    /// <summary><b>وإلغاءُ الأمر يُخرج كلَّ رقمٍ بعينه</b>.</summary>
    [Fact]
    public async Task CancellingASerialOrder_EmptiesEachNumber()
    {
        await SetupAsync();
        var raw = await PlainRawAsync("RM-162", "هيكل", 20m);
        var finished = await SerialFinishedAsync("FG-162", "ضاغط");
        var orderId = await ReleasedOrderAsync(finished, raw, perUnit: 2m, planned: 2m);
        var service = new WorkOrderService(_db);
        await service.CompleteAsync(_companyId, orderId, 2m, "admin", CompletedOn, outputSerials: ["C-1", "C-2"]);

        await service.CancelAsync(_companyId, orderId, "admin", new DateOnly(2026, 4, 7));

        Assert.Equal(0m, await BatchQuantityAsync(finished, "C-1"));
        Assert.Equal(0m, await BatchQuantityAsync(finished, "C-2"));
        Assert.Equal(0m, await OnHandAsync(finished));
        Assert.Equal(20m, await OnHandAsync(raw));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مصنع", "300000000000077", BusinessProfile.Manufacturing);
        _companyId = company.Id;
        _warehouseId = (await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId)).Id;
    }

    private async Task<long> PlainProductAsync(string code, string name)
    {
        var product = Product.Create(_companyId, code, name, "قطعة", ProductType.Stockable, 0m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product.Id;
    }

    private async Task<long> TrackedFinishedAsync(string code, string name)
    {
        var id = await PlainProductAsync(code, name);
        (await _db.Products.SingleAsync(p => p.Id == id)).SetTracking(InventoryTracking.Batch, currentOnHand: 0m);
        await _db.SaveChangesAsync();
        return id;
    }

    private async Task<long> SerialFinishedAsync(string code, string name)
    {
        var id = await PlainProductAsync(code, name);
        (await _db.Products.SingleAsync(p => p.Id == id)).SetTracking(InventoryTracking.Serial, currentOnHand: 0m);
        await _db.SaveChangesAsync();
        return id;
    }

    private async Task<long> PlainRawAsync(string code, string name, decimal quantity)
    {
        var id = await PlainProductAsync(code, name);
        var opening = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 1, 1),
            StockMovementType.Opening, _warehouseId, "افتتاح " + name);
        opening.AddLine(id, quantity, 1m);
        await new StockMovementPostingService(_db).PostAsync(opening);
        return id;
    }

    private async Task<long> TrackedRawAsync(string code, string name,
        params (string Batch, decimal Quantity, DateOnly Expiry)[] batches)
    {
        var id = await TrackedFinishedAsync(code, name);
        var opening = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 1, 1),
            StockMovementType.Opening, _warehouseId, "افتتاح " + name);
        foreach (var (batch, quantity, expiry) in batches)
            opening.AddLine(id, quantity, 2m, batch, expiry);
        await new StockMovementPostingService(_db).PostAsync(opening);
        return id;
    }

    private async Task<long> ReleasedOrderAsync(long finishedId, long rawId, decimal perUnit, decimal planned)
    {
        var bom = BillOfMaterials.Create(_companyId, "BOM-" + finishedId, "وصفة", finishedId, outputQuantity: 1m);
        bom.AddLine(rawId, perUnit);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();
        var order = WorkOrder.CreateDraft(_companyId, bom.Id, new DateOnly(2026, 4, 1), planned);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();
        await new WorkOrderService(_db).ReleaseAsync(_companyId, order.Id);
        return order.Id;
    }

    private async Task<List<StockMovementLine>> LinesAsync(long orderId, StockMovementType type) =>
        (await _db.StockMovements.AsNoTracking()
            .Where(m => m.SourceDocument == $"WorkOrder:{orderId}" && m.Type == type
                        && m.Status == StockMovementStatus.Posted)
            .ToListAsync())
        .SelectMany(m => m.Lines).ToList();

    private async Task<decimal> BatchQuantityAsync(long productId, string batchNumber) =>
        (await _db.StockBatches.AsNoTracking()
            .Where(b => b.ProductId == productId && b.BatchNumber == batchNumber)
            .Select(b => b.Quantity).ToListAsync()).Sum();

    private async Task<decimal> OnHandAsync(long productId) =>
        (await new StockBalanceQuery(_db).ExecuteAsync(_companyId, _warehouseId, includeZeroBalances: true))
        .Where(r => r.ProductId == productId).Sum(r => r.Quantity);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Brqosh.sln غير موجود فوق مجلد الاختبار.");
    }
}