using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Manufacturing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٣٤) <b>إكمالُ أمرِ الإنتاج كان يُؤرَّخ بيوم مسوّدة الأمر.</b>
///
/// <para><b>وسبعةُ مواضعَ تأخذ <c>order.OrderDate</c></b>: حركتا المخزون، وحارسُ الفترة،
/// والقيدان، وترقيمُهما. <b>وأمرٌ يُسوَّد في مارس ويُكمَل في يونيو يُدخل إنتاجَ يونيو في
/// مارس</b> — <b>فتكلفةُ المبيعات وقيمةُ المخزون تقعان في الفترة الخطأ</b>، والمصنعُ
/// يقرأ ربحاً لشهرٍ لم يُنتج فيه.</para>
///
/// <para><b>وبعد ح-١ صار أشدّ</b>: أمرٌ من شهرٍ أُقفل <b>لا يُكمَل أبداً</b> — يرفضه حارسُ
/// الفترة بحقّ، <b>والخطأُ في التاريخ المُغذَّى إليه</b>. <b>فتبقى بضاعةٌ مُنتَجةٌ لا تدخل
/// الدفاتر وموادُّ لا تُصرَف.</b></para>
///
/// <para><b>والنمطُ الصحيحُ في هذا الملفّ نفسِه</b>: <c>CancelAsync</c> تأخذ
/// <c>DateOnly? cancellationDate = null</c> ثمّ <c>?? DateOnly.FromDateTime(DateTime.Today)</c>.
/// <b>فهو الصنفُ نفسُه المسجَّل في ح-٢٣…ح-٣٣ — والصوابُ على بُعد اثنَي عشرَ سطراً.</b></para>
/// </summary>
public class CompletingAWorkOrderIsDatedWhenItHappensTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private long _rawId;
    private long _finishedId;

    public CompletingAWorkOrderIsDatedWhenItHappensTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── القياسُ الحاكم ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>القيدان والحركتان تحمل يومَ الإكمال</b> — والأمرُ مُسوَّدٌ في مارس.
    /// </summary>
    [Fact]
    public async Task TheCompletion_PostsOnTheDayItHappened()
    {
        await SetupAsync();
        var order = await ReleasedOrderAsync(new DateOnly(2026, 3, 1));

        await new WorkOrderService(_db).CompleteAsync(
            _companyId, order.Id, 1m, "admin", new DateOnly(2026, 6, 20));

        Assert.All(await EntryDatesAsync(order.Id), d => Assert.Equal(new DateOnly(2026, 6, 20), d));
        Assert.All(await MovementDatesAsync(order.Id), d => Assert.Equal(new DateOnly(2026, 6, 20), d));
    }

    /// <summary>
    /// <b>والأثرُ المحاسبيّ: لا شيءَ من هذا الإنتاج يقع في مارس.</b>
    ///
    /// <para><b>ويُقاس على ميزان المراجعة لا على حقلٍ في صفّ</b> — فهو ما يُقرأ.
    /// <b>وحسابُ الإنتاج تحت التشغيل يعود صفراً في الحالين</b> (يُفتح ويُغلق في القيدين
    /// معاً)، <b>فالقياسُ على المخزون</b>.</para>
    /// </summary>
    [Fact]
    public async Task NoneOfIt_LandsInTheMonthTheOrderWasDrafted()
    {
        await SetupAsync();
        var order = await ReleasedOrderAsync(new DateOnly(2026, 3, 1));
        var inventoryBefore = await InventoryAsync(new DateOnly(2026, 3, 31));

        await new WorkOrderService(_db).CompleteAsync(
            _companyId, order.Id, 1m, "admin", new DateOnly(2026, 6, 20));

        Assert.Equal(inventoryBefore, await InventoryAsync(new DateOnly(2026, 3, 31)));
        Assert.NotEqual(inventoryBefore, await InventoryAsync(new DateOnly(2026, 6, 30)));
    }

    /// <summary>
    /// <b>وأمرٌ من شهرٍ مقفلٍ يبقى قابلاً للإكمال</b> — <b>وكان يصير حبيسَ الإقفال</b>،
    /// فتبقى بضاعةٌ مُنتَجةٌ خارج الدفاتر.
    /// </summary>
    [Fact]
    public async Task AnOrderFromAClosedMonth_CanStillBeCompleted()
    {
        await SetupAsync();
        var order = await ReleasedOrderAsync(new DateOnly(2026, 3, 1));
        await ClosePeriodAsync(new DateOnly(2026, 3, 1));

        await new WorkOrderService(_db).CompleteAsync(
            _companyId, order.Id, 1m, "admin", new DateOnly(2026, 6, 20));

        var reloaded = await _db.WorkOrders.SingleAsync(w => w.Id == order.Id);
        Assert.Equal(WorkOrderStatus.Completed, reloaded.Status);
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل — والوسيطُ اختياريّ:</b> بلا تاريخٍ يُكمَل اليوم.
    /// <b>فكلُّ موضعِ نداءٍ قائمٍ يبقى يعمل ويُصحَّح سلوكُه معاً</b> — وهو عينُ ما تفعله
    /// <c>CancelAsync</c> أسفلَها.
    /// </summary>
    [Fact]
    public async Task WithNoDateGiven_ItCompletesToday()
    {
        await SetupAsync();
        var order = await ReleasedOrderAsync(new DateOnly(2026, 3, 1));

        await new WorkOrderService(_db).CompleteAsync(_companyId, order.Id, 1m, "admin");

        var today = DateOnly.FromDateTime(DateTime.Today);
        Assert.All(await EntryDatesAsync(order.Id), d => Assert.Equal(today, d));
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ — تركيبُ التكلفة لم يتغيّر (ح-١٣):</b> الموادُّ والأجرُ
    /// والتحميلُ الصناعيُّ تصل المنتَجَ التامَّ كما كانت. <b>فالإصلاحُ مسّ التاريخَ
    /// لا المبالغ.</b>
    /// </summary>
    [Fact]
    public async Task TheCostComposition_IsUnchanged()
    {
        await SetupAsync();
        var order = await ReleasedOrderAsync(new DateOnly(2026, 3, 1));

        await new WorkOrderService(_db).CompleteAsync(
            _companyId, order.Id, 1m, "admin", new DateOnly(2026, 6, 20));

        // ‏موادُ 4×10 = ٤٠، وأجرٌ 5×1 = ٥، وتحميلٌ ٣٪ من ٤٥ = ١٫٣٥.
        // ‏**والمعدّلُ نسبةٌ لا مبلغٌ** — وقد حُسِبت من المصدر لا من تشغيلةٍ سابقةٍ تُفصّل عليها الدعوى.
        var finished = await _db.Products.SingleAsync(p => p.Id == _finishedId);
        Assert.Equal(46.35m, finished.WeightedAverageCost);
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثالث — الكمّيّاتُ تصل كما كانت:</b> المادّةُ تنقص أربعاً
    /// والتامُّ يزيد واحداً.
    /// </summary>
    [Fact]
    public async Task TheQuantities_AreUnchanged()
    {
        await SetupAsync();
        var order = await ReleasedOrderAsync(new DateOnly(2026, 3, 1));

        await new WorkOrderService(_db).CompleteAsync(
            _companyId, order.Id, 1m, "admin", new DateOnly(2026, 6, 20));

        var inv = new InventoryService(_db);
        Assert.Equal(6m, await inv.GetOnHandAsync(_companyId, _rawId, _warehouseId));
        Assert.Equal(1m, await inv.GetOnHandAsync(_companyId, _finishedId, _warehouseId));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<List<DateOnly>> EntryDatesAsync(long orderId) =>
        await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId
                        && (e.SourceDocument == $"WorkOrderComplete:{orderId}"
                            || e.SourceDocument == $"WorkOrderReceipt:{orderId}"))
            .Select(e => e.EntryDate)
            .ToListAsync();

    private async Task<List<DateOnly>> MovementDatesAsync(long orderId) =>
        await _db.StockMovements
            .Where(m => m.CompanyId == _companyId && m.SourceDocument == $"WorkOrder:{orderId}")
            .Select(m => m.MovementDate)
            .ToListAsync();

    private async Task<decimal> InventoryAsync(DateOnly asOf)
    {
        var rows = await new Brqosh.Infrastructure.Reporting.TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2026, 1, 1), asOf);

        var row = rows.FirstOrDefault(r => r.AccountCode == SaudiChartOfAccounts.SystemCodes.Inventory);
        return row is null ? 0m : Math.Round(row.TotalDebit - row.TotalCredit, 2, MidpointRounding.AwayFromZero);
    }

    private async Task ClosePeriodAsync(DateOnly inside)
    {
        // ‏الفترةُ تُنشأ عند أوّل ترحيلٍ فيها — فتُستحضَر قبل إقفالها (درسُ ح-٣٢).
        await new JournalPostingService(_db).EnsurePeriodOpenAsync(_companyId, inside);

        var period = await _db.FiscalPeriods.SingleAsync(
            p => p.CompanyId == _companyId && p.Code == FiscalPeriod.CodeFor(inside));
        await new FiscalPeriodService(_db).CloseAsync(_companyId, period.Id, "admin");
    }

    private async Task<WorkOrder> ReleasedOrderAsync(DateOnly orderDate)
    {
        var bom = BillOfMaterials.Create(_companyId, "BOM-1", "قائمة", _finishedId);
        bom.AddLine(_rawId, 4m);
        bom.SetConversionCosts(5m, 3m);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();

        var order = WorkOrder.CreateDraft(_companyId, bom.Id, orderDate, 1m);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();

        await new WorkOrderService(_db).ReleaseAsync(_companyId, order.Id);
        return order;
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مصنع", "310123456700136", BusinessProfile.General);
        _companyId = company.Id;

        var raw = Product.Create(_companyId, "RAW", "مادّة", "كجم", ProductType.Stockable);
        var finished = Product.Create(_companyId, "FG", "منتَج", "قطعة", ProductType.Stockable);
        _db.Products.AddRange(raw, finished);
        await _db.SaveChangesAsync();

        _rawId = raw.Id;
        _finishedId = finished.Id;
        _warehouseId = (await _db.Warehouses.SingleAsync(w => w.CompanyId == _companyId && w.IsDefault)).Id;

        var opening = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 1, 5),
            StockMovementType.Opening, _warehouseId, "افتتاح خامة");
        opening.AddLine(_rawId, 10m, 10m);
        await new StockMovementPostingService(_db).PostAsync(opening);
    }
}
