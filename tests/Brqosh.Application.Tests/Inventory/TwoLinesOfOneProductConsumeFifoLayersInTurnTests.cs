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
/// ‏(جاهزيّةُ الإطلاق · ح-١٠٣ · كشفه العمل) <b>سطران للصنف نفسِه في فاتورةٍ واحدةٍ تحت FIFO يستهلكان الطبقاتِ بالتتابع — وكانت
/// الفاتورةُ تُرفض متى استنفد السطرُ الأوّلُ طبقة.</b>
///
/// <para>‏<c>ConsumeFifoAsync</c> يقرأ الطبقاتِ بشرط «المتبقّي موجب» <b>من القاعدة</b>، والسطرُ الأوّلُ استهلك طبقتَه <b>في
/// الذاكرة</b> ولم يُحفظ بعد — فتعود الطبقةُ نفسُها للسطر الثاني (الكيانُ المتتبَّعُ برصيده الصفر)، ويُطلب منها صفرٌ
/// فيرمي <c>Consume</c> «كمية الاستهلاك يجب أن تكون موجبة». <b>فلا تُرحَّل فاتورةٌ بالصنف على سطرين</b> — وهو شكلُ كلّ بيعٍ
/// لصنفٍ متتبَّعٍ من دفعتين، وكلّ سطرٍ مكرَّرٍ بسعرٍ أو خصمٍ آخر. كشفه اختبارُ تكلفة المرتجع في ح-١٠٢.</para>
/// </summary>
public sealed class TwoLinesOfOneProductConsumeFifoLayersInTurnTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public TwoLinesOfOneProductConsumeFifoLayersInTurnTests()
    {
        TestActor.Accountant();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// <b>القياسُ الحاكم:</b> طبقتان (١٠ بمئة و١٠ بمئتين)، وفاتورةٌ بسطرين للصنف (١٠ ثمّ ٥) ⟵ الأوّلُ يستنفد الطبقةَ الأولى
    /// (١٬٠٠٠) والثاني يأخذ من الثانية (١٬٠٠٠) — تكلفةُ المبيعات ٢٬٠٠٠، والباقي خمسٌ بمئتين ومتوسّطُها مئتان.
    /// </summary>
    [Fact]
    public async Task TheSecondLine_TakesFromTheNextLayer_WhenTheFirstExhaustsOne()
    {
        var (company, product, customer) = await TwoLayersAsync();

        var invoice = SalesInvoice.CreateDraft(company, customer, new DateOnly(2026, 1, 10));
        invoice.AddLine("صنف", 10m, 300m, product);
        invoice.AddLine("صنف", 5m, 280m, product);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var entry = await _db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == invoice.JournalEntryId);
        Assert.Equal(2000m, entry.Lines
            .Where(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold).Sum(l => l.Debit));

        _db.ChangeTracker.Clear();
        var layers = await _db.InventoryCostLayers.Where(l => l.ProductId == product).ToListAsync();
        Assert.Equal(0m, layers.Single(l => l.UnitCost == 100m).RemainingQuantity);
        Assert.Equal(5m, layers.Single(l => l.UnitCost == 200m).RemainingQuantity);
        Assert.Equal(200m, (await _db.Products.SingleAsync(p => p.Id == product)).WeightedAverageCost);
    }

    /// <summary><b>شاهدُ ضبط</b> — سطرٌ واحدٌ يعبر الطبقتين كما كان: ١٥ ⟵ ١٬٠٠٠ + ١٬٠٠٠.</summary>
    [Fact]
    public async Task OneLineAcrossBothLayers_IsUnchanged()
    {
        var (company, product, customer) = await TwoLayersAsync();

        var invoice = SalesInvoice.CreateDraft(company, customer, new DateOnly(2026, 1, 10));
        invoice.AddLine("صنف", 15m, 300m, product);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var entry = await _db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == invoice.JournalEntryId);
        Assert.Equal(2000m, entry.Lines
            .Where(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold).Sum(l => l.Debit));
    }

    private async Task<(long Company, long Product, long Customer)> TwoLayersAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        company.SetCostingMethod(InventoryCostingMethod.Fifo);
        await _db.SaveChangesAsync();

        var wh = await _db.Warehouses.SingleAsync(w => w.CompanyId == company.Id && w.IsDefault);
        var product = Product.Create(company.Id, "P-FIFO", "صنف", "قطعة", ProductType.Stockable);
        var customer = Customer.Create(company.Id, "عميل");
        _db.Products.Add(product);
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var inv = new InventoryService(_db);
        await inv.PostDocumentMovementAsync(company.Id, wh.Id, StockMovementType.Receipt,
            new DateOnly(2026, 1, 1), "دفعة 1", "R1", [(product.Id, 10m, 100m)]);
        await inv.PostDocumentMovementAsync(company.Id, wh.Id, StockMovementType.Receipt,
            new DateOnly(2026, 1, 2), "دفعة 2", "R2", [(product.Id, 10m, 200m)]);
        await _db.SaveChangesAsync();
        return (company.Id, product.Id, customer.Id);
    }
}
