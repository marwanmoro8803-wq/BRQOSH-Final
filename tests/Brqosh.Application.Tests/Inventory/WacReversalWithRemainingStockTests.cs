using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

/// <summary>
/// (CEEF-228) عكس تكلفة الاستلام مع بقاء رصيد.
///
/// `WacCalculationTests.CancelPurchaseInvoice_ReversesWac` كان الاختبار الوحيد على هذا المسار،
/// وهو يُلغي شراءً **وحيداً** فينتهي الرصيد إلى صفر ويمرّ عبر الفرع المنحلّ `remainingQty &lt;= 0`
/// الذي يضبط التكلفة صفراً بغضّ النظر عن المعادلة. فكان يُخفي العطل بدل كشفه.
///
/// هنا شراءان بتكلفتين مختلفتين، ويُلغى الثاني وحده — فيمرّ التنفيذ من المعادلة نفسها.
/// </summary>
public class WacReversalWithRemainingStockTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _productId;
    private readonly long _supplierId;

    public WacReversalWithRemainingStockTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);

        var company = new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail)
            .GetAwaiter().GetResult();
        _companyId = company.Id;

        // قالب قطاع التجزئة يضبط طريقة التكلفة من الإعداد؛ نُثبّتها هنا على المتوسط المرجّح
        // لأن هذا هو المسار الذي يقرأ Product.WeightedAverageCost عند الصرف.
        // (في FIFO تأتي تكلفة الصرف من الطبقات، فيبقى الضرر محصوراً في تقييم المخزون.)
        company.SetCostingMethod(InventoryCostingMethod.WeightedAverage);

        _db.Warehouses.Add(Warehouse.Create(_companyId, "WH-1", "رئيسي", isDefault: true));
        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        var supplier = Supplier.Create(_companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        _db.SaveChanges();

        _productId = product.Id;
        _supplierId = supplier.Id;
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }

    private async Task<PurchaseInvoice> PostPurchaseAsync(string number, decimal qty, decimal unitCost, int day)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 3, day), number, true);
        invoice.AddLine("صنف", qty, unitCost, _productId);
        return await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<decimal> WacAsync()
    {
        var product = await _db.Products.AsNoTracking().SingleAsync(p => p.Id == _productId);
        return product.WeightedAverageCost;
    }

    /// <summary>
    /// 100@10 ثم 100@20 => المتوسط 15 ورصيد 200.
    /// بإلغاء الشراء الثاني يعود المخزون إلى 100 حبة اشتُريت بـ10 — فالمتوسط الصحيح **10**.
    /// المعادلة المكسورة كانت تعطي ‎-5‎ فتُقصّ إلى **صفر**.
    /// </summary>
    [Fact]
    public async Task CancellingSecondPurchase_RestoresFirstPurchaseCost_NotZero()
    {
        await PostPurchaseAsync("PI-1", 100m, 10m, 1);
        Assert.Equal(10m, await WacAsync());

        var second = await PostPurchaseAsync("PI-2", 100m, 20m, 2);
        Assert.Equal(15m, await WacAsync());

        await new PurchaseInvoiceCancellationService(_db).CancelAsync(
            _companyId, second.Id, new DateOnly(2026, 3, 3), "admin");

        Assert.Equal(10m, await WacAsync());
    }

    /// <summary>
    /// الأثر الذي يراه المحاسب: تكلفة صفر تجعل `SalesInvoicePostingService` يُسقط سطرَي
    /// تكلفة المبيعات والمخزون كلياً، فيخرج قيدٌ بإيرادٍ بلا تكلفة ومجملُ ربحٍ مضخَّم.
    /// هذا الاختبار يقيس **القيد المُرحَّل** لا قيمة الحقل — الأثر لا السبب.
    /// </summary>
    [Fact]
    public async Task SaleAfterCancellation_StillPostsCostOfGoodsSold()
    {
        await PostPurchaseAsync("PI-1", 100m, 10m, 1);
        var second = await PostPurchaseAsync("PI-2", 100m, 20m, 2);
        await new PurchaseInvoiceCancellationService(_db).CancelAsync(
            _companyId, second.Id, new DateOnly(2026, 3, 3), "admin");

        var customer = Customer.Create(_companyId, "عميل", "");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var sale = SalesInvoice.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 3, 4));
        sale.AddLine("صنف", 50m, 60m, _productId);
        var posted = await new SalesInvoicePostingService(_db).PostAsync(sale);

        var entry = await _db.JournalEntries.Include(e => e.Lines)
            .SingleAsync(e => e.Id == posted.JournalEntryId);

        // 50 حبة بتكلفة 10 = 500. مع العطل كانت التكلفة صفراً فلا يوجد سطر COGS إطلاقاً.
        var cogs = entry.Lines.Single(
            l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold);
        Assert.Equal(500m, cogs.Debit);
    }
}
