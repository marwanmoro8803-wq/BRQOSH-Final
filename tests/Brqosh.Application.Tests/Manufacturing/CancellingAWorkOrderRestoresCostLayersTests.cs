using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Manufacturing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٣٠) <b>إلغاءُ أمرِ إنتاجٍ مكتملٍ كان يُفسد تقييمَ المخزون بدل
/// أن يعكسه.</b>
///
/// <para>كان يُرحّل <b>حركتين مقابلتين</b>: وارِداً مقابلَ صرف المواد وصادراً مقابلَ
/// استلام التامّ. <b>فالرصيدُ يعود صحيحاً — والتكلفةُ لا تعود:</b></para>
///
/// <list type="bullet">
/// <item><b>الوارِدُ المقابل يُنشئ طبقةً جديدةً بتكلفةٍ مخلوطة</b> بدل أن يُرمّم الطبقاتِ
/// التي استهلكها الصرفُ <b>بأعيانها</b> — وقد تكون طبقتين بسعرين.</item>
/// <item><b>والصادرُ المقابل يستهلك بالوارد-أوّلاً</b>، فيأكل <b>طبقةً أقدمَ من طبقة
/// التامّ التي أنشأها هذا الأمر</b> — فتُحذف تكلفةٌ ليست تكلفتَه.</item>
/// </list>
///
/// <para><b>فينشقّ مجموعُ الطبقات عن الرصيد، ويخرج كلُّ بيعٍ لاحقٍ بتكلفةٍ خاطئة.</b></para>
///
/// <para><b>والمسارُ الصحيحُ قائمٌ وموثَّق</b> — <c>ReverseDocumentMovementAsync</c>
/// (CEEF-229 · م0-٣ و م0-٣ب) — <b>وخمسُ خدماتِ إلغاءٍ تستعمله وهذه لم تكن منها</b>.</para>
/// </summary>
public class CancellingAWorkOrderRestoresCostLayersTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private long _rawId;
    private long _finishedId;

    public CancellingAWorkOrderRestoresCostLayersTests()
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

    // ── الهويّةُ الحاكمة ────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم:</b> بعد الإلغاء، <b>مجموعُ الطبقات الباقية يطابق الرصيد
    /// مضروباً في تكلفته</b> لكلّ صنف — وهي الهويّةُ التي كانت تنشقّ.
    /// </summary>
    [Fact]
    public async Task AfterCancelling_TheCostLayersAgreeWithTheBalance()
    {
        await SetupAsync();
        var order = await CompleteAWorkOrderAsync();

        await new WorkOrderService(_db).CancelAsync(_companyId, order.Id, "admin");

        await AssertLayersMatchBalanceAsync(_rawId);
        await AssertLayersMatchBalanceAsync(_finishedId);
    }

    /// <summary>
    /// <b>والمادّةُ تعود بطبقتها الأصليّة</b>: استُلمت عشرٌ بعشرة، فصُرف منها أربع،
    /// فبعد الإلغاء <b>تعود عشراً بقيمة مئة</b> — <b>لا طبقةٌ جديدةٌ فوق الباقي</b>.
    /// </summary>
    [Fact]
    public async Task TheRawMaterialLayer_IsRestored_NotRecreated()
    {
        await SetupAsync();
        var order = await CompleteAWorkOrderAsync();

        await new WorkOrderService(_db).CancelAsync(_companyId, order.Id, "admin");

        var layers = await _db.InventoryCostLayers
            .Where(l => l.CompanyId == _companyId && l.ProductId == _rawId && l.RemainingQuantity > 0)
            .ToListAsync();

        Assert.Single(layers);
        Assert.Equal(10m, layers[0].RemainingQuantity);
    }

    /// <summary>
    /// <b>وطبقةُ التامّ تُزال بعينها</b> — فلا يبقى منتَجٌ تامٌّ في الطبقات بعد إلغاء
    /// أمرِ إنتاجه.
    /// </summary>
    [Fact]
    public async Task TheFinishedGoodsLayer_IsRemoved()
    {
        await SetupAsync();
        var order = await CompleteAWorkOrderAsync();

        await new WorkOrderService(_db).CancelAsync(_companyId, order.Id, "admin");

        var remaining = await _db.InventoryCostLayers
            .Where(l => l.CompanyId == _companyId && l.ProductId == _finishedId && l.RemainingQuantity > 0)
            .SumAsync(l => l.RemainingQuantity);

        Assert.Equal(0m, remaining);
    }

    // ── شاهدُ الضبط ─────────────────────────────────────────────────────────

    /// <summary>
    /// <b>وشاهدُ ضبط — الأرصدةُ تعود كما كانت:</b> المادّةُ عشرٌ والتامُّ صفر.
    /// <b>فالإصلاحُ صحّح التكلفةَ ولم يكسر الكمّية</b> — وهي التي كانت صحيحةً أصلاً.
    /// </summary>
    [Fact]
    public async Task TheQuantities_StillReturnToWhereTheyStarted()
    {
        await SetupAsync();
        var order = await CompleteAWorkOrderAsync();

        await new WorkOrderService(_db).CancelAsync(_companyId, order.Id, "admin");

        var inv = new InventoryService(_db);
        Assert.Equal(10m, await inv.GetOnHandAsync(_companyId, _rawId, _warehouseId));
        Assert.Equal(0m, await inv.GetOnHandAsync(_companyId, _finishedId, _warehouseId));
    }

    /// <summary>
    /// <b>والمتوسّطُ المرجّح كذلك</b> — والتدقيقُ يسمّي الطريقتين معاً. المادّةُ دخلت
    /// بعشرة، فبعد إنتاجٍ وإلغائه <b>يعود متوسّطُها عشرةً</b>.
    ///
    /// <para><b>ولا طبقاتِ هنا أصلاً</b> — فالقياسُ على التكلفة نفسِها لا على الطبقات.</para>
    /// </summary>
    [Fact]
    public async Task UnderWeightedAverage_TheUnitCostReturnsToWhereItStarted()
    {
        await SetupAsync(InventoryCostingMethod.WeightedAverage);
        var order = await CompleteAWorkOrderAsync();

        await new WorkOrderService(_db).CancelAsync(_companyId, order.Id, "admin");

        var raw = await _db.Products.SingleAsync(p => p.Id == _rawId);
        Assert.Equal(10m, raw.WeightedAverageCost);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task AssertLayersMatchBalanceAsync(long productId)
    {
        var layered = await _db.InventoryCostLayers
            .Where(l => l.CompanyId == _companyId && l.ProductId == productId)
            .SumAsync(l => l.RemainingQuantity);

        var onHand = await new InventoryService(_db).GetOnHandAsync(_companyId, productId, _warehouseId);

        Assert.True(layered == onHand,
            $"‏الطبقات {layered} والرصيد {onHand} — انشقّ مجموعُ الطبقات عن الرصيد، "
            + "فكلُّ بيعٍ لاحقٍ يخرج بتكلفةٍ خاطئة.");
    }

    private async Task SetupAsync(InventoryCostingMethod method = InventoryCostingMethod.Fifo)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مصنع", "310123456700033", BusinessProfile.General);
        _companyId = company.Id;

        // ‏**وطبقاتُ التكلفة لا تُنشأ إلّا في الوارد-أوّلاً** — والافتراضُ متوسّطٌ مرجّح.
        // ‏فقياسُ الطبقات على منشأةٍ بالمتوسّط يقيس العدمَ ويمرّ خضراءَ بلا معنى،
        // ‏**وقد وقع ذلك في أوّل صياغةٍ لهذا الملفّ**.
        company.SetCostingMethod(method);
        await _db.SaveChangesAsync();

        var raw = Product.Create(_companyId, "RAW", "مادّة", "كجم", ProductType.Stockable);
        var finished = Product.Create(_companyId, "FG", "منتَج", "قطعة", ProductType.Stockable);
        _db.Products.AddRange(raw, finished);
        await _db.SaveChangesAsync();

        _rawId = raw.Id;
        _finishedId = finished.Id;
        _warehouseId = (await _db.Warehouses.SingleAsync(w => w.CompanyId == _companyId && w.IsDefault)).Id;

        // ‏عشرُ وحداتٍ بعشرة — طبقةٌ واحدةٌ معلومةُ القيمة.
        var opening = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 8, 1),
            StockMovementType.Opening, _warehouseId, "افتتاح خامة");
        opening.AddLine(_rawId, 10m, 10m);
        await new StockMovementPostingService(_db).PostAsync(opening);
    }

    private async Task<WorkOrder> CompleteAWorkOrderAsync()
    {
        var bom = BillOfMaterials.Create(_companyId, "BOM-1", "قائمة", _finishedId);
        bom.AddLine(_rawId, 4m);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();

        var order = WorkOrder.CreateDraft(_companyId, bom.Id, new DateOnly(2026, 8, 5), 1m);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();

        var service = new WorkOrderService(_db);
        await service.ReleaseAsync(_companyId, order.Id);
        await service.CompleteAsync(_companyId, order.Id, 1m, "admin");
        return order;
    }
}
