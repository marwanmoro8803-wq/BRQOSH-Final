using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Hospitality;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Hospitality;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-231 · ط1-٣ج) <b>الصرفُ بالأقدم انتهاءً — والمطبخُ كان يرفض كلَّ مكوّنٍ متتبَّع.</b>
///
/// <para><b>ما كان:</b> بندٌ مُعلَنٌ مفتوحاً منذ <c>ط1-٣</c>، <b>ويرفض بسببه
/// <c>KitchenIssueService</c> كلَّ وصفةٍ فيها مكوّنٌ متتبَّع</b>. فمطعمٌ يتتبّع صلاحيةَ
/// لحمه — وهو ما يفعله كلُّ مطعمٍ جادّ — <b>لا يستطيع تشغيلَ وصفةٍ واحدة</b>، وكلُّ ما
/// بُني في <c>ط3-٣</c> من تفجير الوصفات معطَّلٌ عنده.</para>
///
/// <para><b>ولماذا الانتهاءُ لا الورود:</b> <c>FIFO</c> يُخرج الأقدمَ <b>وروداً</b> وهو
/// صوابٌ للتكلفة، <b>والصلاحيةُ لا تتبع الورود</b>: شحنةٌ وصلت اليوم قد تنتهي قبل شحنةٍ
/// وصلت الشهرَ الماضي. <b>فالصرفُ بالورود يُبقي الأقربَ انتهاءً على الرفّ حتّى يتلف.</b></para>
///
/// <para><b>وهويّةُ هـ-٢١:</b> مجموعُ ما خُصّص = المطلوبُ بالضبط، ولا دفعةَ تُعطي أكثرَ
/// ممّا فيها، والترتيبُ بالانتهاء.</para>
/// </summary>
public class FefoAllocationTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private int _seq;

    public FefoAllocationTests()
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

    // ── هـ-٢١: الأقربُ انتهاءً أوّلاً ───────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم: الأقربُ انتهاءً يخرج أوّلاً — ولو وصل أخيراً.</b>
    ///
    /// <para><b>والمثالُ مقلوبٌ عمداً</b>: الدفعةُ التي وصلت <b>ثانيةً</b> تنتهي أوّلاً.
    /// <b>فترتيبٌ بالورود يُخرج الخطأَ ويبدو صحيحاً في مثالٍ متوافق</b> — ولا يُفرّق إلا
    /// حين يفترق الترتيبان.</para>
    /// </summary>
    [Fact]
    public async Task TheEarliestExpiring_IsDrawnFirst_EvenIfItArrivedLast()
    {
        var product = await TrackedProductAsync();
        await ReceiveAsync(product, 50m, "LOT-LATE", new DateOnly(2028, 1, 1));
        await ReceiveAsync(product, 30m, "LOT-SOON", new DateOnly(2026, 12, 1));

        var draws = await new BatchAllocationService(_db).AllocateAsync(
            _companyId, product, _warehouseId, 40m, new DateOnly(2026, 6, 1));

        Assert.Equal(2, draws.Count);
        Assert.Equal("LOT-SOON", draws[0].BatchNumber);
        Assert.Equal(30m, draws[0].Quantity);
        Assert.Equal("LOT-LATE", draws[1].BatchNumber);
        Assert.Equal(10m, draws[1].Quantity);

        // ‏هـ-٢١: المجموعُ هو المطلوبُ بالضبط.
        Assert.Equal(40m, draws.Sum(d => d.Quantity));
    }

    /// <summary>
    /// <b>والمنتهيةُ لا تُخصَّص أصلاً — ولا تُقترَح ثمّ تُرفض.</b>
    /// </summary>
    [Fact]
    public async Task AnExpiredBatch_IsNeverAllocated()
    {
        var product = await TrackedProductAsync();
        await ReceiveAsync(product, 100m, "LOT-DEAD", new DateOnly(2026, 1, 1));
        await ReceiveAsync(product, 20m, "LOT-OK", new DateOnly(2027, 1, 1));

        var draws = await new BatchAllocationService(_db).AllocateAsync(
            _companyId, product, _warehouseId, 20m, new DateOnly(2026, 6, 1));

        Assert.Equal("LOT-OK", draws.Single().BatchNumber);
    }

    /// <summary>
    /// <b>ونقصُ الساري يُرفض باسمه — ويُسمّي المنتهيةَ إن وُجدت.</b>
    ///
    /// <para><b>وتخصيصٌ ناقصٌ بصمتٍ يُصرَف فيُنقص المخزونَ بأقلَّ ممّا خرج</b>، ويظهر
    /// الفرقُ عجزَ جردٍ آخرَ السنة. <b>والرسالةُ تقول لماذا</b>: رصيدٌ موجودٌ ولا يُصرف
    /// لأنّه منتهٍ يُربك المستخدمَ إن لم يُقَل.</para>
    /// </summary>
    [Fact]
    public async Task InsufficientUnexpiredStock_IsRefusedAndNamesTheExpired()
    {
        var product = await TrackedProductAsync();
        await ReceiveAsync(product, 100m, "LOT-DEAD", new DateOnly(2026, 1, 1));
        await ReceiveAsync(product, 5m, "LOT-OK", new DateOnly(2027, 1, 1));

        var error = await Assert.ThrowsAsync<AccountingException>(() =>
            new BatchAllocationService(_db).AllocateAsync(
                _companyId, product, _warehouseId, 50m, new DateOnly(2026, 6, 1)));

        Assert.Contains("منتهيةُ الصلاحية", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وما لا صلاحيةَ له آخِرُ الطابور.</b>
    ///
    /// <para><b>وتقديمُه يُبقي المؤرَّخَ على الرفّ حتّى يتلف</b> — وهو عينُ ما بُني هذا
    /// لمنعه.</para>
    /// </summary>
    [Fact]
    public async Task ABatchWithoutAnExpiry_IsDrawnLast()
    {
        var product = await TrackedProductAsync();
        await ReceiveAsync(product, 40m, "LOT-NODATE", expiry: null);
        await ReceiveAsync(product, 40m, "LOT-DATED", new DateOnly(2027, 5, 1));

        var draws = await new BatchAllocationService(_db).AllocateAsync(
            _companyId, product, _warehouseId, 50m, new DateOnly(2026, 6, 1));

        Assert.Equal("LOT-DATED", draws[0].BatchNumber);
        Assert.Equal("LOT-NODATE", draws[1].BatchNumber);
    }

    // ── المطبخُ صار يصرف المتتبَّع ──────────────────────────────────────────

    /// <summary>
    /// <b>ووصفةٌ بمكوّنٍ متتبَّعٍ تُصرَف — وكانت تُرفض كلُّها.</b>
    ///
    /// <para><b>وهذا هو أثرُ البند كما يراه المطعم</b>: يتتبّع صلاحيةَ لحمه، <b>فيتعطّل
    /// عنده كلُّ ما بُني من تفجير الوصفات</b> — ويعود إلى بيعٍ بلا تكلفةِ مبيعات.</para>
    /// </summary>
    [Fact]
    public async Task ARecipeWithATrackedIngredient_IsIssuedFromTheEarliestExpiringBatch()
    {
        var beef = await TrackedProductAsync();
        await ReceiveAsync(beef, 10m, "LOT-LATE", new DateOnly(2028, 1, 1));
        await ReceiveAsync(beef, 4m, "LOT-SOON", new DateOnly(2026, 12, 1));

        var item = MenuItem.Create(_companyId, "BURGER", "برجر", MenuSection.Main, 30m);
        item.AddComponent(beef, 2m);
        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync();

        await new KitchenIssueService(_db).IssueForSalesAsync(
            _companyId, new DateOnly(2026, 6, 1), [(item.Id, 3m)], "Probe:FEFO");

        // ‏ثلاثُ حصصٍ × ٢ = ٦: أربعٌ من الأقرب انتهاءً واثنتان من الأبعد.
        Assert.Equal(0m, (await _db.StockBatches.SingleAsync(b => b.BatchNumber == "LOT-SOON")).Quantity);
        Assert.Equal(8m, (await _db.StockBatches.SingleAsync(b => b.BatchNumber == "LOT-LATE")).Quantity);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<long> TrackedProductAsync()
    {
        await SetupAsync();
        var product = Product.Create(_companyId, $"TRK-{++_seq}", $"مكوّن {_seq}", "كيلو",
            ProductType.Stockable, salePrice: 0m);
        product.SetTracking(InventoryTracking.Batch, currentOnHand: 0m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product.Id;
    }

    private async Task ReceiveAsync(long productId, decimal qty, string batchNumber, DateOnly? expiry)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.CompanyId == _companyId);
        if (supplier is null)
        {
            supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
            _db.Suppliers.Add(supplier);
            await _db.SaveChangesAsync();
        }

        var order = PurchaseOrder.CreateDraft(_companyId, supplier.Id, new DateOnly(2026, 1, 1));
        order.AddLine("مكوّن", qty, 10m, productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();

        var receipt = GoodsReceipt.CreateDraft(_companyId, supplier.Id, order.Id,
            _warehouseId, new DateOnly(2026, 1, 2));
        receipt.AddLine("مكوّن", qty, qty, 10m, productId, batchNumber, expiry);
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);
        await _db.SaveChangesAsync();
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;

        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("مطعم القياس", "310123456700044", BusinessProfile.Hospitality);
        _companyId = company.Id;
        _warehouseId = (await _db.Warehouses.FirstAsync(w => w.CompanyId == _companyId)).Id;
    }
}
