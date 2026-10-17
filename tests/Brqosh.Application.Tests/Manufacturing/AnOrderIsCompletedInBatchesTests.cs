using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Manufacturing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٤٨ · <c>manufacturing#4</c>) <b>الأمرُ يُكمَل بدفعات — والأخيرةُ تُغلقه.</b>
///
/// <para><b>ما كان:</b> إكمالٌ واحدٌ يُغلق الأمر — فخطُّ إنتاجٍ يُسلّم نصفَ الكميّة اليومَ ونصفَها بعد أسبوعٍ إمّا ينتظر
/// فلا يدخل التامُّ المخزونَ ولا يُباع، وإمّا يُغلق الأمرَ بنصفه ويفتح أمراً ثانياً بوصفةٍ وصرفٍ منفصلين.</para>
///
/// <para>الوصفة: ٤ قطعٍ من ٦ «أ» (٢ ريال) و٢ «ب» (١٠ ريالات)؛ والأمر ٨ قطع — فالمعياريُّ ٦٤.</para>
/// </summary>
public class AnOrderIsCompletedInBatchesTests : IDisposable
{
    private const string Wip = SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess;

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _finishedId;
    private long _rawAId;
    private long _rawBId;
    private long _orderId;

    public AnOrderIsCompletedInBatchesTests()
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

    /// <summary><b>دفعتان بالوصفة = إكمالٌ واحدٌ بالوصفة</b> — والأمرُ صادرٌ بعد الأولى ومكتملٌ بعد الثانية.</summary>
    [Fact]
    public async Task TwoBackflushedBatches_EqualOneCompletion()
    {
        await SetupAsync();

        await CompleteAsync(4m, close: false, new DateOnly(2026, 4, 3));
        var afterFirst = await OrderAsync();
        Assert.Equal(WorkOrderStatus.Released, afterFirst.Status);
        Assert.Equal(4m, afterFirst.ProducedQuantity);

        await CompleteAsync(4m, close: true, new DateOnly(2026, 4, 9));

        var order = await OrderAsync();
        Assert.Equal(WorkOrderStatus.Completed, order.Status);
        Assert.Equal(8m, order.ProducedQuantity);
        Assert.Equal(2, order.CompletionCount);
        Assert.Equal((8m, 64m), await FinishedAsync());
        Assert.Equal(88m, await QuantityAsync(_rawAId));
        Assert.Equal(0m, await BalanceAsync(Wip));

        var row = (await new ProductionCostSheetQuery(_db).ExecuteAsync(_companyId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)))
            .Single(r => r.WorkOrderId == _orderId);
        Assert.Equal(64m, row.TotalCost);
        Assert.Equal(new DateOnly(2026, 4, 9), row.CompletedOn);
    }

    /// <summary>
    /// <b>القياسُ الحاكم: الدفعةُ تأخذ نصيبَها من الموادّ المصروفة فعلاً، والإغلاقُ يأخذ الباقي</b> — صرفٌ بثمانيةٍ وستّين،
    /// دفعةُ ٤ من ٨ تنقل ٣٤ ويبقى ٣٤ في WIP، والإغلاقُ بثلاثٍ ينقل ٣٤ — فالتامُّ ٧ بثمانيةٍ وستّين وWIP صفر.
    /// </summary>
    [Fact]
    public async Task BatchesShareTheIssuedCost_AndTheCloseTakesWhatRemains()
    {
        await SetupAsync();
        await new WorkOrderService(_db).IssueMaterialsAsync(_companyId, _orderId,
            [(_rawAId, 14m), (_rawBId, 4m)], "admin", new DateOnly(2026, 4, 2));

        await CompleteAsync(4m, close: false, new DateOnly(2026, 4, 3));
        Assert.Equal((4m, 34m), await FinishedAsync());
        Assert.Equal(34m, await BalanceAsync(Wip));

        await CompleteAsync(3m, close: true, new DateOnly(2026, 4, 9));
        Assert.Equal((7m, 68m), await FinishedAsync());
        Assert.Equal(0m, await BalanceAsync(Wip));
        Assert.Equal(86m, await QuantityAsync(_rawAId));
    }

    /// <summary><b>وإلغاءُ أمرٍ صادرٍ أُكمل جزئيّاً يعكس دفعتَه</b> — التامُّ يخرج والخاماتُ تعود وWIP صفر.</summary>
    [Fact]
    public async Task CancellingAPartlyCompletedOrder_ReversesItsBatch()
    {
        await SetupAsync();
        await CompleteAsync(4m, close: false, new DateOnly(2026, 4, 3));

        await new WorkOrderService(_db).CancelAsync(_companyId, _orderId, "admin", new DateOnly(2026, 4, 6));

        Assert.Equal(WorkOrderStatus.Cancelled, (await OrderAsync()).Status);
        Assert.Equal(0m, await QuantityAsync(_finishedId));
        Assert.Equal(100m, await QuantityAsync(_rawAId));
        Assert.Equal(0m, await BalanceAsync(Wip));
    }

    /// <summary><b>وإلغاءُ أمرٍ مكتملٍ بدفعتين يعكس الدفعتين</b> — لا الأولى وحدَها.</summary>
    [Fact]
    public async Task CancellingAnOrderCompletedInTwoBatches_ReversesBoth()
    {
        await SetupAsync();
        await CompleteAsync(4m, close: false, new DateOnly(2026, 4, 3));
        await CompleteAsync(4m, close: true, new DateOnly(2026, 4, 9));

        await new WorkOrderService(_db).CancelAsync(_companyId, _orderId, "admin", new DateOnly(2026, 4, 10));

        Assert.Equal(0m, await QuantityAsync(_finishedId));
        Assert.Equal(100m, await QuantityAsync(_rawAId));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.Inventory) - 700m);

        // ‏**والأرصدةُ وحدَها لا تكفي شاهداً**: قيدا الدفعة بالوصفة يتقابلان على المخزون وWIP، فدفعةٌ لم تُعكس لا تظهر
        // ‏في رصيد. فيُشترط ألّا يبقى قيدٌ مرحَّلٌ للأمر — أربعةُ قيودٍ (دفعتان × إكمالٌ واستلام) معكوسةٌ كلُّها.
        var keys = new[]
        {
            $"WorkOrderComplete:{_orderId}", $"WorkOrderReceipt:{_orderId}",
            $"WorkOrderComplete:{_orderId}:2", $"WorkOrderReceipt:{_orderId}:2",
        };
        var statuses = await _db.JournalEntries.AsNoTracking()
            .Where(e => e.CompanyId == _companyId && e.SourceDocument != null && keys.Contains(e.SourceDocument))
            .Select(e => e.Status)
            .ToListAsync();
        // ‏والعكسُ قيدٌ يحمل مفتاحَ أصله — فثمانية: أربعةُ أصولٍ «معكوسة» وأربعةُ عكوسٍ مرحَّلة.
        Assert.Equal(4, statuses.Count(s => s == JournalEntryStatus.Reversed));
        Assert.Equal(8, statuses.Count);
    }

    [Fact]
    public void TheShareOfABatch_IsProportional_AndTheCloseTakesTheRest()
    {
        var order = WorkOrder.CreateDraft(1, 1, new DateOnly(2026, 4, 1), 10m);
        typeof(WorkOrder).GetProperty(nameof(WorkOrder.IssuedMaterialCost))!.SetValue(order, 100m);

        Assert.Equal(30m, order.IssuedCostToRelieve(3m, closing: false));
        Assert.Equal(100m, order.IssuedCostToRelieve(3m, closing: true));
        Assert.Equal(100m, order.IssuedCostToRelieve(12m, closing: false));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مصنع", "300000000000148", BusinessProfile.Manufacturing);
        _companyId = company.Id;

        var finished = Product.Create(_companyId, "FG-148", "منتج تامّ", "قطعة", ProductType.Stockable, 0m);
        var rawA = Product.Create(_companyId, "RM-A", "خامة أ", "كجم", ProductType.Stockable, 0m);
        var rawB = Product.Create(_companyId, "RM-B", "خامة ب", "كجم", ProductType.Stockable, 0m);
        _db.Products.AddRange(finished, rawA, rawB);
        await _db.SaveChangesAsync();
        _finishedId = finished.Id;
        _rawAId = rawA.Id;
        _rawBId = rawB.Id;

        var warehouse = await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId);
        var opening = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 1, 1),
            StockMovementType.Opening, warehouse.Id, "افتتاح الخامات");
        opening.AddLine(_rawAId, 100m, 2m);
        opening.AddLine(_rawBId, 50m, 10m);
        await new StockMovementPostingService(_db).PostAsync(opening);

        var bom = BillOfMaterials.Create(_companyId, "BOM-148", "تجميع", _finishedId, outputQuantity: 4m);
        bom.AddLine(_rawAId, 6m);
        bom.AddLine(_rawBId, 2m);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();

        var order = WorkOrder.CreateDraft(_companyId, bom.Id, new DateOnly(2026, 4, 1), 8m);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();
        _orderId = order.Id;
        await new WorkOrderService(_db).ReleaseAsync(_companyId, order.Id);
    }

    private Task CompleteAsync(decimal quantity, bool close, DateOnly on) =>
        new WorkOrderService(_db).CompleteAsync(_companyId, _orderId, quantity, "admin", on, closeOrder: close);

    private Task<WorkOrder> OrderAsync() => _db.WorkOrders.AsNoTracking().SingleAsync(o => o.Id == _orderId);

    private async Task<(decimal Quantity, decimal Value)> FinishedAsync()
    {
        var row = (await new StockBalanceQuery(_db).ExecuteAsync(_companyId)).Single(r => r.ProductId == _finishedId);
        return (row.Quantity, Math.Round(row.TotalValue, 2, MidpointRounding.AwayFromZero));
    }

    private async Task<decimal> QuantityAsync(long productId) =>
        (await new StockBalanceQuery(_db).ExecuteAsync(_companyId)).Where(r => r.ProductId == productId).Sum(r => r.Quantity);

    private async Task<decimal> BalanceAsync(string code) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == code)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync())
        .Sum(l => l.Debit - l.Credit);
}
