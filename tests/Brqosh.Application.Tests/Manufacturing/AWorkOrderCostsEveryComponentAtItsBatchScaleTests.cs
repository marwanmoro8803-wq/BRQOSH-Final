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
/// (جاهزيّةُ الإطلاق · ح-٩٤ · <c>manufacturing#11</c>) <b>أمرُ الإنتاج يُكلِّف كلَّ مكوّنٍ بمقياس دفعته — وكانت اختباراتُ التصنيع لا تقيس ذلك.</b>
///
/// <para><b>مسحُ طفراتٍ على مال <c>WorkOrderService</c></b> (إحدى عشرة طفرةً تكسر كلٌّ منها قاعدةً ماليّةً واحدة) نجت منه اثنتان: <b>إهمالُ كمّيّة
/// دفعة القائمة</b> (<c>scale = producedQuantity</c>) <b>وتكلفةُ الموادّ آخرُ مكوّنٍ لا مجموعُها</b> (<c>materialCost = totalCost</c>) — فكلُّ قوائم
/// الاختبارات القائمة <b>دفعتُها وحدةٌ ومكوّنُها واحد</b>، فالطفرتان والأصلُ سواءٌ عليها. وصنعٌ بدفعةٍ من أربع وحدتين يُخطئ المخزونَ والتكلفةَ معاً.</para>
///
/// <para>قائمةٌ تُخرج <b>٤ وحداتٍ</b> من <b>٦ خامة «أ» (٢٫٠٠) و٢ خامة «ب» (١٠٫٠٠)</b>؛ ويُصنع <b>٨</b> — دفعتان: ١٢ «أ» (٢٤) و٤ «ب» (٤٠) ⟵ <b>٦٤</b>،
/// والوحدةُ <b>٨٫٠٠</b>.</para>
/// </summary>
public class AWorkOrderCostsEveryComponentAtItsBatchScaleTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _finishedId;
    private long _rawAId;
    private long _rawBId;
    private long _orderId;

    public AWorkOrderCostsEveryComponentAtItsBatchScaleTests()
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
    /// <b>القياسُ الحاكم: التامُّ ٨ وحداتٍ بـ٦٤، والخامتان نقصتا بمقياس الدفعة</b> — ١٢ «أ» و٤ «ب»، لا ٤٨ و١٦ (بلا قسمةٍ على الدفعة).
    /// </summary>
    [Fact]
    public async Task TheFinishedGoods_CostEveryComponent_AtTheBatchScale()
    {
        await SetupAndCompleteAsync();

        var rows = await new StockBalanceQuery(_db).ExecuteAsync(_companyId);
        var finished = rows.Single(r => r.ProductId == _finishedId);
        Assert.Equal(8m, finished.Quantity);
        Assert.Equal(64m, Math.Round(finished.TotalValue, 2, MidpointRounding.AwayFromZero));
        Assert.Equal(88m, rows.Single(r => r.ProductId == _rawAId).Quantity);
        Assert.Equal(46m, rows.Single(r => r.ProductId == _rawBId).Quantity);
    }

    /// <summary>
    /// <b>والأستاذُ ينقل المالَ نفسَه</b> — قيدُ الإكمال يُدين WIP بـ٦٤ ويُدان المخزونُ بها، <b>لا بتكلفة آخر مكوّنٍ وحدَه (٤٠)</b>.
    /// </summary>
    [Fact]
    public async Task TheCompletionEntry_MovesTheCostOfEveryComponent()
    {
        await SetupAndCompleteAsync();

        var inventory = SaudiChartOfAccounts.SystemCodes.Inventory;
        var lines = await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.SourceDocument == $"WorkOrderComplete:{_orderId}")
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == inventory)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync();
        Assert.Equal(64m, lines.Sum(l => l.Credit - l.Debit));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAndCompleteAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مصنع", "300000000000077", BusinessProfile.Manufacturing);
        _companyId = company.Id;

        var finished = Product.Create(_companyId, "FG-94", "منتج تامّ", "قطعة", ProductType.Stockable, 0m);
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

        var bom = BillOfMaterials.Create(_companyId, "BOM-94", "تجميع بالدفعة", _finishedId, outputQuantity: 4m);
        bom.AddLine(_rawAId, 6m);
        bom.AddLine(_rawBId, 2m);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();

        var order = WorkOrder.CreateDraft(_companyId, bom.Id, new DateOnly(2026, 4, 1), 8m);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();
        _orderId = order.Id;

        var service = new WorkOrderService(_db);
        await service.ReleaseAsync(_companyId, order.Id);
        await service.CompleteAsync(_companyId, order.Id, 8m, "admin");
    }
}
