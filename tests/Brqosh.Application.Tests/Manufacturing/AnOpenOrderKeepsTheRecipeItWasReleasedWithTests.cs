using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Manufacturing;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Manufacturing;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Manufacturing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٧٧ · <c>manufacturing#5</c>) <b>أمرُ الإنتاج الصادرُ يُكمَل بالوصفة التي صدر بها — وكان يُكمَل
/// بقائمة الموادّ كما هي يومَ الإكمال.</b>
///
/// <para><b>ما كان:</b> <c>WorkOrderService.CompleteAsync</c> يقرأ بنودَ القائمة وتكاليفَ تحويلها وقتَ الإكمال. <b>فتعديلٌ
/// للقائمة بين الإصدار والإكمال يُغيّر ما يُصرَف من المخزون وبكم يدخل التامُّ</b> — بلا أثرٍ في سجلّ التدقيق، لأنّ النافذةَ
/// كانت تحفظ القائمةَ مباشرةً.</para>
///
/// <para><b>والأرقامُ مختارةٌ ليختلف كلُّ طريقٍ خاطئ:</b> خامةٌ ٢٠ بعشرة، وقائمةٌ بثلاثٍ للوحدة وأجرِ خمسةٍ بلا تحميل، وأمرٌ
/// لوحدتين. <b>ثمّ تُعدَّل القائمةُ</b> إلى أربعٍ للوحدة وأجرِ تسعةٍ وتحميلِ ١٠٪. ⟵ بالوصفة المجمَّدة: ستٌّ تُصرَف، والوحدةُ
/// <b>(٦٠ + ١٠) ÷ ٢ = ٣٥</b>، والباقي ١٤. ⟵ بقائمة اليوم: ثمانٍ، والوحدةُ <b>(٨٠ + ١٨) × ١٫١ ÷ ٢ = ٥٣٫٩</b>، والباقي ١٢.</para>
/// </summary>
public class AnOpenOrderKeepsTheRecipeItWasReleasedWithTests : IDisposable
{
    private const string BomEdited = "تعديل قائمة مواد";

    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _rawId;
    private long _finishedId;
    private long _bomId;

    public AnOpenOrderKeepsTheRecipeItWasReleasedWithTests()
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

    /// <summary><b>القياسُ الحاكم:</b> تعديلُ القائمة بعد الإصدار لا يمسّ الأمرَ — ستٌّ لا ثمانٍ، والوحدةُ ٣٥ لا ٥٣٫٩.</summary>
    [Fact]
    public async Task EditingTheBomAfterRelease_DoesNotChangeWhatTheOrderConsumes()
    {
        await SetupAsync();
        var orderId = await DraftAsync();
        await new WorkOrderService(_db).ReleaseAsync(_companyId, orderId);

        await EditBomAsync(perUnit: 4m, labour: 9m, overheadPercent: 10m);
        await CompleteAsync(orderId);

        Assert.Equal(14m, await OnHandAsync(_rawId));
        Assert.Equal(35m, await FinishedUnitCostAsync());
        Assert.Equal(70m, await WipLoadedAsync(orderId));
    }

    /// <summary>
    /// <b>شاهدُ ضبط — والتجميدُ عند الإصدار لا عند الإنشاء:</b> مسودّةٌ تُعدَّل قائمتُها قبل إصدارها تُكمَل بالقائمة المعدَّلة.
    /// فالاختبارُ الحاكمُ لا يمرّ لأنّ الوصفةَ نسخةٌ قديمةٌ من يوم الإنشاء.
    /// </summary>
    [Fact]
    public async Task ADraft_FollowsTheBom_UntilItIsReleased()
    {
        await SetupAsync();
        var orderId = await DraftAsync();

        await EditBomAsync(perUnit: 4m, labour: 9m, overheadPercent: 10m);
        await new WorkOrderService(_db).ReleaseAsync(_companyId, orderId);
        await CompleteAsync(orderId);

        Assert.Equal(12m, await OnHandAsync(_rawId));
        Assert.Equal(53.9m, await FinishedUnitCostAsync());
    }

    /// <summary>
    /// <b>وأمرٌ صدر قبل الترقية</b> — بلا وصفةٍ مجمَّدة — <b>يُجمّده المُرقِّعُ عند الإقلاع من القائمة كما هي لحظتَها</b>،
    /// ولا يمسّ مسودّة؛ ثمّ لا يتبع تعديلاً بعده.
    /// </summary>
    [Fact]
    public async Task AnOrderReleasedBeforeTheUpgrade_IsFrozenAtStartup_FromTheBomAsItStands()
    {
        await SetupAsync();
        var released = await DraftAsync();
        await new WorkOrderService(_db).ReleaseAsync(_companyId, released);
        var draft = await DraftAsync();
        await ForgetRecipeAsync(released);

        await new BrqoshSchemaUpdater(_db).ApplyAsync();
        _db.ChangeTracker.Clear();

        var order = await _db.WorkOrders.SingleAsync(w => w.Id == released);
        Assert.True(order.RecipeFrozen);
        var line = Assert.Single(order.Materials);
        Assert.Equal(_rawId, line.ComponentProductId);
        Assert.Equal(3m, line.Quantity);
        Assert.Equal(5m, order.RecipeLabourCostPerUnit);

        var stillDraft = await _db.WorkOrders.SingleAsync(w => w.Id == draft);
        Assert.False(stillDraft.RecipeFrozen);
        Assert.Empty(stillDraft.Materials);

        await EditBomAsync(perUnit: 4m, labour: 9m, overheadPercent: 10m);
        await CompleteAsync(released);
        Assert.Equal(14m, await OnHandAsync(_rawId));
    }

    /// <summary>
    /// <b>وأمرٌ صادرٌ بقي بلا وصفةٍ يُرفض إكمالُه برسالة</b> — لا يُستكمَل من قائمة اليوم، ولا يُصرَف شيء.
    /// </summary>
    [Fact]
    public async Task AReleasedOrderWithoutARecipe_IsRefused_NotCompletedFromTodaysBom()
    {
        await SetupAsync();
        var orderId = await DraftAsync();
        await new WorkOrderService(_db).ReleaseAsync(_companyId, orderId);
        await ForgetRecipeAsync(orderId);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => CompleteAsync(orderId));
        Assert.Contains("بلا وصفةٍ مجمَّدة", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.Equal(20m, await OnHandAsync(_rawId));
    }

    /// <summary>
    /// <b>وتعديلُ القائمة يُسجَّل بما تغيّر</b> — المكوّنُ من ثلاثٍ إلى أربع، والأجرُ ونسبةُ التحميل، وعددُ الأوامر الصادرة
    /// التي بقيت على وصفتها.
    /// </summary>
    [Fact]
    public async Task EditingABom_IsAudited_WithWhatChanged()
    {
        await SetupAsync();
        var orderId = await DraftAsync();
        await new WorkOrderService(_db).ReleaseAsync(_companyId, orderId);

        await EditBomAsync(perUnit: 4m, labour: 9m, overheadPercent: 10m);

        var row = Assert.Single(await _db.AuditLogEntries
            .Where(a => a.CompanyId == _companyId && a.Action == BomEdited).ToListAsync());
        Assert.Equal("BillOfMaterials", row.EntityType);
        Assert.Equal("BOM-R", row.EntityKey);
        Assert.Contains("RAW: 3 ← 4", row.Details, StringComparison.Ordinal);
        Assert.Contains("أجر الوحدة: 5 ← 9", row.Details, StringComparison.Ordinal);
        Assert.Contains("نسبة التحميل: 0% ← 10%", row.Details, StringComparison.Ordinal);
        Assert.Contains("أوامر صادرة تبقى على وصفتها: 1", row.Details, StringComparison.Ordinal);
    }

    /// <summary><b>شاهدُ ضبط:</b> حفظٌ بلا تغييرٍ لا يُسجَّل — سجلٌّ يمتلئ بما لم يقع يُخفي ما وقع.</summary>
    [Fact]
    public async Task SavingAnUnchangedBom_LeavesNoAuditRow()
    {
        await SetupAsync();

        await EditBomAsync(perUnit: 3m, labour: 5m, overheadPercent: 0m);

        Assert.Empty(await _db.AuditLogEntries
            .Where(a => a.CompanyId == _companyId && a.Action == BomEdited).ToListAsync());
    }

    /// <summary><b>ولا يُصدَر أمرٌ بقائمةٍ غيرِ قائمته</b> — فالوصفةُ المجمَّدةُ وصفتُه هو.</summary>
    [Fact]
    public async Task ReleasingWithAnotherBom_IsRefused()
    {
        await SetupAsync();
        var other = BillOfMaterials.Create(_companyId, "BOM-X", "قائمة أخرى", _finishedId);
        other.AddLine(_rawId, 1m);
        _db.BillsOfMaterials.Add(other);
        await _db.SaveChangesAsync();

        var order = WorkOrder.CreateDraft(_companyId, _bomId, new DateOnly(2026, 1, 5), 2m);

        Assert.Throws<AccountingException>(() => order.Release("WO-X", other));
        Assert.False(order.RecipeFrozen);
        Assert.Equal(WorkOrderStatus.Draft, order.Status);
    }

    /// <summary>
    /// <b>ونافذةُ القائمة تُعدّل عبر الخدمة المسجَّلة</b> — لا تستبدل البنودَ بيدها. فالقياسُ بنيويّ: النافذةُ تفتح دفاترَ
    /// الإنتاج (<c>AppServices.CreateDb</c>) ولا تُشغَّل في اختبار.
    /// </summary>
    [Fact]
    public void TheBomDialog_EditsThroughTheAuditedService()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Brqosh.Desktop", "Views", "Pages", "BomDialog.xaml.cs"));

        Assert.Contains("GetService<BillOfMaterialsService>().UpdateAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".ReplaceLines(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Rename(", source, StringComparison.Ordinal);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مصنع الوصفة", "310123456700044", BusinessProfile.Manufacturing);
        _companyId = company.Id;
        company.SetCostingMethod(InventoryCostingMethod.Fifo);
        await _db.SaveChangesAsync();

        var raw = Product.Create(_companyId, "RAW", "خامة", "كجم", ProductType.Stockable);
        var finished = Product.Create(_companyId, "FG", "منتَج", "قطعة", ProductType.Stockable);
        _db.Products.AddRange(raw, finished);
        await _db.SaveChangesAsync();
        _rawId = raw.Id;
        _finishedId = finished.Id;

        var warehouseId = (await _db.Warehouses.SingleAsync(w => w.CompanyId == _companyId && w.IsDefault)).Id;
        var opening = StockMovement.CreateDraft(_companyId, new DateOnly(2026, 1, 1), StockMovementType.Opening,
            warehouseId, "خامة");
        opening.AddLine(_rawId, 20m, 10m);
        await new StockMovementPostingService(_db).PostAsync(opening);

        var bom = BillOfMaterials.Create(_companyId, "BOM-R", "قائمة الوصفة", _finishedId);
        bom.AddLine(_rawId, 3m);
        bom.SetConversionCosts(5m, 0m);
        _db.BillsOfMaterials.Add(bom);
        await _db.SaveChangesAsync();
        _bomId = bom.Id;
    }

    private async Task<long> DraftAsync()
    {
        var order = WorkOrder.CreateDraft(_companyId, _bomId, new DateOnly(2026, 1, 5), 2m);
        _db.WorkOrders.Add(order);
        await _db.SaveChangesAsync();
        return order.Id;
    }

    private Task EditBomAsync(decimal perUnit, decimal labour, decimal overheadPercent) =>
        new BillOfMaterialsService(_db).UpdateAsync(_companyId, _bomId, "قائمة الوصفة",
            new[] { (_rawId, perUnit) }, labour, overheadPercent, "admin");

    private Task CompleteAsync(long orderId) =>
        new WorkOrderService(_db).CompleteAsync(_companyId, orderId, 2m, "admin", new DateOnly(2026, 1, 6));

    /// <summary>ما كان عليه أمرٌ صدر قبل الترقية: لا بنودَ وصفة، ولا علامةَ تجميد.</summary>
    private async Task ForgetRecipeAsync(long orderId)
    {
        await _db.Database.ExecuteSqlRawAsync(
            "DELETE FROM work_order_materials WHERE work_order_id = {0}", orderId);
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE work_orders SET RecipeFrozen = 0, RecipeLabourCostPerUnit = '0', RecipeOverheadRatePercent = '0' WHERE Id = {0}",
            orderId);
        _db.ChangeTracker.Clear();
    }

    private async Task<decimal> OnHandAsync(long productId) =>
        (await _db.InventoryCostLayers
            .Where(l => l.CompanyId == _companyId && l.ProductId == productId)
            .Select(l => l.RemainingQuantity).ToListAsync())
        .Sum();

    private async Task<decimal> FinishedUnitCostAsync() =>
        Assert.Single(await _db.InventoryCostLayers
            .Where(l => l.CompanyId == _companyId && l.ProductId == _finishedId && l.RemainingQuantity > 0)
            .ToListAsync()).UnitCost;

    private async Task<decimal> WipLoadedAsync(long orderId) =>
        (await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId && e.SourceDocument == $"WorkOrderComplete:{orderId}")
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.ProductionWorkInProcess)
            .Select(l => l.Debit).ToListAsync())
        .Sum();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }
}
