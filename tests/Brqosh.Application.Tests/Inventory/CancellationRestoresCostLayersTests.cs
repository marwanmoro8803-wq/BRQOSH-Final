using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;

using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-229 · م0-٣ / I-2) <b>الإلغاءُ يُرمّم الطبقةَ الأصلية — لا يُنشئ طبقةً جديدة بتاريخه.</b>
///
/// <para><b>ودخل هذا البندُ المسارَ الحرج بقرار المالك:</b> العميلُ التجريبيّ يُنشأ بملفّ
/// <c>Retail</c>، و<c>retail.json</c> يشحن <c>Fifo</c>. فطبقاتُ التكلفة حيّةٌ عنده.</para>
///
/// <para><b>العطل:</b> <c>SalesInvoiceCancellationService</c> يُرحّل <b>حركةَ إرجاعٍ جديدة</b>
/// بتاريخ الإلغاء بدل عكس الأصلية. و<c>InventoryCostLayer.Restore()</c> قائمةٌ في النطاق
/// <b>بصفر مستدعٍ</b>. فالطبقةُ المستهلَكة تبقى صفراً، وتُضاف طبقةٌ جديدة <b>في آخر طابور
/// FIFO</b> — فينقلب ترتيبُ الاستهلاك.</para>
///
/// <para><b>ولا يكشفه قياسُ الكمية:</b> المجموعُ يعود صحيحاً تماماً. **العطلُ في التكلفة
/// لا في العدد**، ولا يظهر إلا عند البيعة التالية.</para>
/// </summary>
public class CancellationRestoresCostLayersTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public CancellationRestoresCostLayersTests()
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

    /// <summary>
    /// <b>القياس المركزيّ — ولا يُقاس بالكمية بل بالمال.</b>
    ///
    /// <para>طبقتان: 100 بـ10 ثمّ 100 بـ20. بيعُ 100 يستهلك الأولى كاملةً ⟵ تكلفةٌ 1,000.
    /// يُلغى البيع. ثمّ تُباع 100 مرّةً أخرى — <b>وهي البيعةُ نفسُها من المخزون نفسِه، فتكلفتُها
    /// 1,000 حتماً.</b></para>
    ///
    /// <para>واليوم تُضاف طبقةُ الإرجاع بتاريخ الإلغاء فتقع <b>بعد</b> طبقة العشرين، والأولى
    /// تبقى مستهلَكة — فتأكل البيعةُ الثانية طبقةَ العشرين وتخرج التكلفةُ <b>2,000</b>.
    /// <b>ضعفُ الصواب، ومجملُ الربح ينهار إلى النصف.</b></para>
    /// </summary>
    [Fact]
    public async Task TheSameSale_CostsTheSame_AfterACancellation()
    {
        var (companyId, warehouseId, productId, customerId) = await SeedTwoLayersAsync();

        var first = await SellAsync(companyId, customerId, productId, quantity: 100m);
        Assert.Equal(1_000m, await CogsOfAsync(first));

        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(companyId, first.Id, new DateOnly(2026, 3, 1), "مدير");

        // ‏الكميةُ تعود — وهذا ما يمرّ على كلّ فحصٍ قائم.
        Assert.Equal(200m, await OnHandAsync(productId, warehouseId));

        var second = await SellAsync(companyId, customerId, productId, quantity: 100m,
            date: new DateOnly(2026, 3, 2));

        Assert.Equal(1_000m, await CogsOfAsync(second));
    }

    /// <summary>
    /// <b>والطبقةُ الأصلية هي التي تعود — لا نظيرةٌ لها.</b> فلو رُمّمت الكميةُ بطبقةٍ جديدة
    /// لَبقي العددُ صحيحاً والترتيبُ خاطئاً. فيُقاس <b>عددُ الطبقات وتواريخُها</b>: طبقتان
    /// بتاريخَي الاستلام، لا ثالثةٌ بتاريخ الإلغاء.
    /// </summary>
    [Fact]
    public async Task NoThirdLayer_IsBornAtCancellationTime()
    {
        var (companyId, _, productId, customerId) = await SeedTwoLayersAsync();

        var invoice = await SellAsync(companyId, customerId, productId, quantity: 100m);
        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(companyId, invoice.Id, new DateOnly(2026, 3, 1), "مدير");

        var layers = await _db.InventoryCostLayers
            .Where(l => l.ProductId == productId)
            .OrderBy(l => l.ReceivedDate).ToListAsync();

        Assert.True(layers.Count == 2,
            $"عددُ الطبقات {layers.Count} لا 2 — أُنشئت طبقةٌ بتاريخ الإلغاء بدل ترميم الأصلية: "
            + string.Join(" · ", layers.Select(l => $"{l.ReceivedDate:yyyy-MM-dd} ×{l.RemainingQuantity}@{l.UnitCost}")));

        Assert.Equal(new DateOnly(2026, 1, 1), layers[0].ReceivedDate);
        Assert.Equal(100m, layers[0].RemainingQuantity);
        Assert.Equal(10m, layers[0].UnitCost);
    }

    /// <summary>
    /// (CEEF-229 · م0-٣ب) <b>المرآة: إلغاءُ الإشعار الدائن يُزيل الطبقةَ التي أنشأها —
    /// لا يأكل أقدمَ طبقةٍ في الطابور.</b>
    ///
    /// <para>الإشعارُ الدائن <b>يُدخل</b> بضاعةً فيخلق طبقة. وإلغاؤه كان يُرحّل حركةَ
    /// <c>Issue</c> جديدة، <b>وهي تستهلك من رأس طابور FIFO</b> — أي أقدمَ طبقةٍ وأرخصَها —
    /// وتترك طبقةَ المرتجع قائمةً في المخزون.</para>
    ///
    /// <para><b>وقيمةُ المخزون الإجمالية لا تكشفه:</b> المجموعُ متطابقٌ في الحالتين. الفرقُ
    /// في <b>أيّ طبقةٍ بقيت</b>، ولا يظهر إلا في تكلفة البيعة التالية.</para>
    /// </summary>
    [Fact]
    public async Task CancellingACreditNote_RemovesItsOwnLayer_NotTheOldestOne()
    {
        var (companyId, warehouseId, productId, customerId) = await SeedTwoLayersAsync();

        // ‏تُباع 100 فتُستهلك الطبقةُ الرخيصة كاملةً، ثمّ تُردّ بإشعارٍ دائن فتعود طبقةً
        // ‏جديدة بتاريخه. والباقي: الغالية 100@20 ومرتجعُ 100@10.
        var invoice = await SellAsync(companyId, customerId, productId, quantity: 100m);
        Assert.Equal(1_000m, await CogsOfAsync(invoice));

        var note = CreditNote.CreateDraft(companyId, customerId, invoice.Id, new DateOnly(2026, 2, 15), "مرتجع");
        note.AddLine("صنف", 100m, 30m, productId);
        var postedNote = await new CreditNotePostingService(_db).PostAsync(note);

        await new CreditNoteCancellationService(_db)
            .CancelAsync(companyId, postedNote.Id, new DateOnly(2026, 3, 1), "مدير");

        // ‏بعد الإلغاء يجب أن يبقى ما كان قبل الإشعار: الغالية وحدها.
        var layers = await _db.InventoryCostLayers
            .Where(l => l.ProductId == productId && l.RemainingQuantity > 0)
            .OrderBy(l => l.ReceivedDate).ToListAsync();

        Assert.True(layers.Count == 1,
            "بقيت طبقاتٌ لا ينبغي بقاؤها بعد إلغاء الإشعار الدائن: "
            + string.Join(" · ", layers.Select(l => $"{l.ReceivedDate:yyyy-MM-dd} ×{l.RemainingQuantity}@{l.UnitCost}")));

        Assert.Equal(20m, layers[0].UnitCost);
        Assert.Equal(100m, await OnHandAsync(productId, warehouseId));
    }

    // ── التهيئة ──────────────────────────────────────────────────────────────

    private async Task<(long CompanyId, long WarehouseId, long ProductId, long CustomerId)> SeedTwoLayersAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة الطبقات", "310123456700003", BusinessProfile.Retail);
        company.SetCostingMethod(InventoryCostingMethod.Fifo);
        await _db.SaveChangesAsync();

        var warehouse = await _db.Warehouses.SingleAsync(w => w.CompanyId == company.Id && w.IsDefault);
        var product = Product.Create(company.Id, "P-1", "صنف", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        var customer = Customer.Create(company.Id, "عميل نقدي");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var inventory = new InventoryService(_db);
        await inventory.PostDocumentMovementAsync(company.Id, warehouse.Id, StockMovementType.Receipt,
            new DateOnly(2026, 1, 1), "دفعة رخيصة", "R1", [(product.Id, 100m, 10m)]);
        await inventory.PostDocumentMovementAsync(company.Id, warehouse.Id, StockMovementType.Receipt,
            new DateOnly(2026, 1, 2), "دفعة غالية", "R2", [(product.Id, 100m, 20m)]);
        await _db.SaveChangesAsync();

        return (company.Id, warehouse.Id, product.Id, customer.Id);
    }

    private async Task<SalesInvoice> SellAsync(
        long companyId, long customerId, long productId, decimal quantity, DateOnly? date = null)
    {
        var invoice = SalesInvoice.CreateDraft(companyId, customerId, date ?? new DateOnly(2026, 2, 1));
        invoice.AddLine("صنف", quantity, 30m, productId);
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<decimal> CogsOfAsync(SalesInvoice invoice)
    {
        var entry = await _db.JournalEntries.Include(e => e.Lines)
            .SingleAsync(e => e.Id == invoice.JournalEntryId);

        return entry.Lines
            .Where(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold)
            .Sum(l => l.Debit);
    }

    private async Task<decimal> OnHandAsync(long productId, long warehouseId) =>
        await _db.InventoryBalances
            .Where(b => b.ProductId == productId && b.WarehouseId == warehouseId)
            .Select(b => b.QuantityOnHand).SingleOrDefaultAsync();
}
