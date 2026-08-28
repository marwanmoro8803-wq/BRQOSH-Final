using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-229 · م0-١) إلغاء حركة مخزنية يعكس <b>الكمية والتكلفة</b> معاً.
///
/// <para><b>العطل:</b> <c>StockMovementCancellationService</c> كان يعكس الكمية وحدها، ثم يصفّر
/// المتوسط المرجّح في حالةٍ منحلّة واحدة — أن يصير الرصيد الكليّ صفراً. فإلغاء حركةٍ واردة مع
/// بقاء رصيد يترك المتوسط <b>مسموماً بتكلفة حركةٍ أُلغيت</b>.</para>
///
/// <para><b>ولماذا هذا أكثر إلغاءٍ يقع فعلاً:</b> تصحيح الرصيد الافتتاحيّ في أسبوع العميل الأول.
/// يُدخل الرصيد بتكلفة خاطئة، يكتشفها، فيُلغي الحركة — ويبقى متوسطه فاسداً إلى الأبد.</para>
///
/// <para>والحارس يقيس <b>الرقم</b> ويؤكّد الهوية هـ-٣ — لا يكتفي بأن العملية «لم ترمِ».</para>
/// </summary>
public class StockMovementCancellationCostTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _warehouseId;
    private readonly long _productId;
    private readonly long _supplierId;

    public StockMovementCancellationCostTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";

        var company = new DatabaseInitializer(_db)
            .InitializeAsync("شركة تجارية", "310123456700003", BusinessProfile.General)
            .GetAwaiter().GetResult();
        _companyId = company.Id;

        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        var supplier = Supplier.Create(_companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        _db.SaveChanges();

        _warehouseId = _db.Warehouses.Single(w => w.CompanyId == _companyId).Id;
        _productId = product.Id;
        _supplierId = supplier.Id;
    }

    private async Task BuyAsync(decimal qty, decimal unitPrice, string reference)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 1, 5), reference, true);
        invoice.AddLine("توريد", qty, unitPrice, _productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    /// <summary>
    /// حركة يدوية بلا مستند مصدر — وهي وحدها القابلة للإلغاء من شاشة الحركات؛ حركاتُ
    /// المستندات تُصحَّح من مستندها.
    /// </summary>
    private async Task<StockMovement> PostManualAsync(
        StockMovementType type, string description, (long ProductId, decimal Qty, decimal Cost)[] lines)
    {
        var movement = StockMovement.CreateDraft(
            _companyId, new DateOnly(2026, 1, 20), type, _warehouseId, description);
        foreach (var (productId, qty, cost) in lines)
            movement.AddLine(productId, qty, cost);
        var posted = await new StockMovementPostingService(_db).PostAsync(movement);
        await _db.SaveChangesAsync();
        return posted;
    }

    private async Task<decimal> WacAsync() =>
        (await _db.Products.SingleAsync(p => p.Id == _productId)).WeightedAverageCost;

    private async Task<decimal> OnHandAsync() =>
        (await _db.InventoryBalances.Where(b => b.CompanyId == _companyId && b.ProductId == _productId)
            .ToListAsync()).Sum(b => b.QuantityOnHand);

    /// <summary>
    /// <b>القياس الأساسيّ.</b> رصيد 100 بتكلفة 10، ثم حركة واردة 100 بتكلفة 50 ⟵ المتوسط 30.
    /// إلغاء الحركة يجب أن يُعيد المتوسط إلى <b>10</b> بالضبط. قبل الإصلاح كان يبقى 30.
    /// </summary>
    [Fact]
    public async Task CancellingInboundMovement_RestoresWeightedAverageCost()
    {
        await BuyAsync(100m, 10m, "PI-1");
        Assert.Equal(10m, await WacAsync());

        var movement = await PostManualAsync(
            StockMovementType.AdjustmentIn, "رصيد افتتاحيّ بتكلفة خاطئة", [(_productId, 100m, 50m)]);

        Assert.Equal(30m, await WacAsync());       // (1,000 + 5,000) ÷ 200
        Assert.Equal(200m, await OnHandAsync());

        await new StockMovementCancellationService(_db)
            .CancelAsync(_companyId, movement.Id, new DateOnly(2026, 1, 21), "admin");

        Assert.Equal(100m, await OnHandAsync());
        Assert.Equal(10m, await WacAsync());       // كان 30 قبل الإصلاح
    }

    /// <summary>
    /// إلغاء حركة صادرة لا يمسّ المتوسط — الصرف بالمتوسط لا يغيّره، وعكسُه كذلك.
    /// حارسٌ للحارس: لو صار المسار الموحّد يعبث بالتكلفة في كل الأحوال لسقط هذا.
    /// </summary>
    [Fact]
    public async Task CancellingOutboundMovement_LeavesCostUntouched()
    {
        await BuyAsync(100m, 20m, "PI-2");

        var movement = await PostManualAsync(
            StockMovementType.AdjustmentOut, "تلف", [(_productId, 30m, 20m)]);

        Assert.Equal(70m, await OnHandAsync());
        Assert.Equal(20m, await WacAsync());

        await new StockMovementCancellationService(_db)
            .CancelAsync(_companyId, movement.Id, new DateOnly(2026, 1, 21), "admin");

        Assert.Equal(100m, await OnHandAsync());
        Assert.Equal(20m, await WacAsync());
    }

    /// <summary>
    /// الحالة المنحلّة التي كانت وحدها معالَجة: الرصيد يعود صفراً ⟵ المتوسط صفر.
    /// تبقى صحيحة بعد التوحيد — الإصلاح لا يكسر ما كان يعمل.
    /// </summary>
    [Fact]
    public async Task CancellingTheOnlyReceipt_ZeroesCost()
    {
        var movement = await PostManualAsync(
            StockMovementType.AdjustmentIn, "رصيد افتتاحيّ", [(_productId, 40m, 25m)]);

        Assert.Equal(25m, await WacAsync());

        await new StockMovementCancellationService(_db)
            .CancelAsync(_companyId, movement.Id, new DateOnly(2026, 1, 21), "admin");

        Assert.Equal(0m, await OnHandAsync());
        Assert.Equal(0m, await WacAsync());
    }

    /// <summary>
    /// حركة بسطرين لصنفين مختلفين: كلٌّ يُعكس بتكلفته. عكسُ سطرٍ واحد وترك الآخر كان ممكناً
    /// لو بُني المسار على «أوّل سطر» كما في عيوب أخرى في هذا المستودع.
    /// </summary>
    [Fact]
    public async Task MultiLineMovement_ReversesEachLineCost()
    {
        var second = Product.Create(_companyId, "P-2", "صنف ثانٍ", "قطعة", ProductType.Stockable);
        _db.Products.Add(second);
        await _db.SaveChangesAsync();

        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 1, 5), "PI-3", true);
        invoice.AddLine("صنف", 100m, 10m, _productId);
        invoice.AddLine("صنف ثانٍ", 100m, 20m, second.Id);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var movement = await PostManualAsync(
            StockMovementType.AdjustmentIn, "تسوية خاطئة", [(_productId, 100m, 50m), (second.Id, 100m, 80m)]);

        await new StockMovementCancellationService(_db)
            .CancelAsync(_companyId, movement.Id, new DateOnly(2026, 1, 21), "admin");

        Assert.Equal(10m, await WacAsync());
        Assert.Equal(20m, (await _db.Products.SingleAsync(p => p.Id == second.Id)).WeightedAverageCost);
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
