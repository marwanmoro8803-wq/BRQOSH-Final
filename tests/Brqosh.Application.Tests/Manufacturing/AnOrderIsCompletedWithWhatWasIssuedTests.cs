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
/// (جاهزيّةُ الإطلاق · ح-١٤٧ · <c>manufacturing#4</c> · <c>manufacturing#8</c>) <b>ما صُرف فعلاً هو تكلفةُ الأمر.</b>
///
/// <para><b>ما كان:</b> لا صرفَ قبل الإكمال — الموادُّ تُخصم بالوصفة عند الإكمال، فلا يُسجَّل ما خرج من المستودع فعلاً
/// ولا ما ضاع، <b>والإنتاجُ تحت التشغيل لا يحمل رصيداً أبداً</b>.</para>
///
/// <para>الوصفة: ٤ قطعٍ من ٦ كجم «أ» (٢ ريال) و٢ كجم «ب» (١٠ ريالات)؛ والأمر ٨ قطع — فالمعياريُّ ١٢ «أ» و٤ «ب» = ٦٤.</para>
/// </summary>
public class AnOrderIsCompletedWithWhatWasIssuedTests : IDisposable
{
    private const string Wip = SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess;

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _finishedId;
    private long _rawAId;
    private long _rawBId;
    private long _orderId;

    public AnOrderIsCompletedWithWhatWasIssuedTests()
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

    /// <summary><b>والصرفُ يُحمّل WIP رصيداً بين الصرف والإكمال</b> — ويُخرج الخاماتِ من المستودع يومَ خرجت.</summary>
    [Fact]
    public async Task AnIssue_GivesWorkInProcessABalance_AndTakesTheMaterialsOut()
    {
        await SetupAsync();

        var cost = await IssueAsync((_rawAId, 14m), (_rawBId, 4m));

        Assert.Equal(68m, cost);
        Assert.Equal(68m, await BalanceAsync(Wip));
        Assert.Equal(86m, await QuantityAsync(_rawAId));
        Assert.Equal(46m, await QuantityAsync(_rawBId));
    }

    /// <summary>
    /// <b>القياسُ الحاكم: الإكمالُ بما صُرف لا بالوصفة</b> — التامُّ بثمانيةٍ وستّين لا بأربعةٍ وستّين (الهدرُ في تكلفة
    /// الوحدة)، ولا يُخصم من الخامات شيءٌ ثانٍ، وWIP يعود صفراً؛ والتالفُ يُسجَّل.
    /// </summary>
    [Fact]
    public async Task Completion_UsesWhatWasIssued_NotTheRecipe()
    {
        await SetupAsync();
        await IssueAsync((_rawAId, 14m), (_rawBId, 4m));

        await new WorkOrderService(_db).CompleteAsync(_companyId, _orderId, 8m, "admin",
            new DateOnly(2026, 4, 5), scrapQuantity: 1m);

        var finished = (await new StockBalanceQuery(_db).ExecuteAsync(_companyId)).Single(r => r.ProductId == _finishedId);
        Assert.Equal(8m, finished.Quantity);
        Assert.Equal(68m, Math.Round(finished.TotalValue, 2, MidpointRounding.AwayFromZero));
        Assert.Equal(86m, await QuantityAsync(_rawAId));
        Assert.Equal(0m, await BalanceAsync(Wip));

        var order = await _db.WorkOrders.SingleAsync(o => o.Id == _orderId);
        Assert.Equal(1m, order.ScrapQuantity);
        Assert.Equal(100m, order.YieldPercent);
    }

    /// <summary>
    /// <b>وكشفُ تكلفة الإنتاج يقرأ الصرفَ الفعليّ</b> — كان يقرأ الموادَّ من قيد الإكمال وحدَه، فأمرٌ صُرف له
    /// كان سيظهر بلا موادّ (ولا قيدَ إكمالٍ أصلاً حين لا تحويل).
    /// </summary>
    [Fact]
    public async Task TheCostSheet_ReadsTheActualIssues()
    {
        await SetupAsync();
        await IssueAsync((_rawAId, 14m), (_rawBId, 4m));
        await new WorkOrderService(_db).CompleteAsync(_companyId, _orderId, 8m, "admin", new DateOnly(2026, 4, 5));

        var row = (await new ProductionCostSheetQuery(_db).ExecuteAsync(_companyId, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)))
            .Single(r => r.WorkOrderId == _orderId);

        Assert.Equal(68m, row.MaterialCost);
        Assert.Equal(68m, row.TotalCost);
        Assert.Equal(8.5m, row.UnitCost);
    }

    /// <summary><b>وأمرٌ بلا صرفٍ يُكمَل بالوصفة كما كان</b> — لا يتغيّر شيءٌ لمن لا يصرف.</summary>
    [Fact]
    public async Task WithoutAnIssue_TheRecipeIsBackflushedAsBefore()
    {
        await SetupAsync();

        await new WorkOrderService(_db).CompleteAsync(_companyId, _orderId, 8m, "admin", new DateOnly(2026, 4, 5));

        var finished = (await new StockBalanceQuery(_db).ExecuteAsync(_companyId)).Single(r => r.ProductId == _finishedId);
        Assert.Equal(64m, Math.Round(finished.TotalValue, 2, MidpointRounding.AwayFromZero));
        Assert.Equal(88m, await QuantityAsync(_rawAId));
    }

    [Fact]
    public async Task AMaterialOutsideTheRecipe_IsRefused()
    {
        await SetupAsync();
        var other = Product.Create(_companyId, "RM-X", "خامة خارج الوصفة", "كجم", ProductType.Stockable, 0m);
        _db.Products.Add(other);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(() => IssueAsync((other.Id, 1m)));

        Assert.Contains("ليس في وصفة الأمر", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0m, await BalanceAsync(Wip));
    }

    /// <summary><b>وإلغاءُ أمرٍ صادرٍ صُرف له يُعيد الموادَّ ويُفرّغ WIP</b> — وإلا بقي رصيدٌ بلا أمر.</summary>
    [Fact]
    public async Task CancellingAReleasedOrder_ReturnsWhatWasIssued()
    {
        await SetupAsync();
        await IssueAsync((_rawAId, 14m), (_rawBId, 4m));

        await new WorkOrderService(_db).CancelAsync(_companyId, _orderId, "admin", new DateOnly(2026, 4, 6));

        Assert.Equal(100m, await QuantityAsync(_rawAId));
        Assert.Equal(50m, await QuantityAsync(_rawBId));
        Assert.Equal(0m, await BalanceAsync(Wip));
    }

    /// <summary><b>وإلغاءُ أمرٍ مكتملٍ صُرف له يعكس الصرفَ مع الإكمال.</b></summary>
    [Fact]
    public async Task CancellingACompletedOrder_ReversesTheIssuesToo()
    {
        await SetupAsync();
        await IssueAsync((_rawAId, 14m), (_rawBId, 4m));
        await new WorkOrderService(_db).CompleteAsync(_companyId, _orderId, 8m, "admin", new DateOnly(2026, 4, 5));

        await new WorkOrderService(_db).CancelAsync(_companyId, _orderId, "admin", new DateOnly(2026, 4, 6));

        Assert.Equal(100m, await QuantityAsync(_rawAId));
        Assert.Equal(0m, await QuantityAsync(_finishedId));
        Assert.Equal(0m, await BalanceAsync(Wip));
        Assert.Equal(0m, await BalanceAsync(SaudiChartOfAccounts.SystemCodes.Inventory) - 700m);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مصنع", "300000000000147", BusinessProfile.Manufacturing);
        _companyId = company.Id;

        var finished = Product.Create(_companyId, "FG-147", "منتج تامّ", "قطعة", ProductType.Stockable, 0m);
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

        var bom = BillOfMaterials.Create(_companyId, "BOM-147", "تجميع", _finishedId, outputQuantity: 4m);
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

    private Task<decimal> IssueAsync(params (long ProductId, decimal Quantity)[] issues) =>
        new WorkOrderService(_db).IssueMaterialsAsync(_companyId, _orderId, issues, "admin", new DateOnly(2026, 4, 2));

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
