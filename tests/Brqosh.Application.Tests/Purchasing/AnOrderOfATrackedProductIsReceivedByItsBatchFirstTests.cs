using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٠٦ · <c>order-to-cash-procure-to-pay-inventory#7</c>) <b>الاستلامُ الآليُّ من أمر الشراء لا يخترع دفعة.</b>
///
/// <para><c>PurchaseOrderService.CreateInvoiceFromOrderAsync</c> يُنشئ إذنَ استلامٍ آليّاً حين لا إذنَ للأمر — وسطرُه بلا دفعةٍ ولا
/// انتهاء، فكان المَخنَقُ يرفض الصنفَ المتتبَّع «ولا يحمل هذا السطرُ هويّةً» <b>دون أن يقول ما العمل</b>. ورقمُ الدفعة لا يُخترع:
/// <b>فيُرفض باسم الصنف ويُدَلّ على إذن الاستلام</b>، وهو يُسجّل الدفعةَ وانتهاءها ثمّ تُنشأ الفاتورة من الأمر.</para>
/// </summary>
public sealed class AnOrderOfATrackedProductIsReceivedByItsBatchFirstTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;
    private long _supplierId;

    public AnOrderOfATrackedProductIsReceivedByItsBatchFirstTests()
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

    /// <summary><b>القياسُ الحاكم:</b> أمرٌ لدواءٍ متتبَّعٍ بلا إذن ⟵ رفضٌ يُسمّي الصنفَ ويُدَلّ على إذن الاستلام، ولا إذنَ ولا فاتورة.</summary>
    [Fact]
    public async Task WithoutAReceipt_TheTrackedProductIsRefusedAndPointedToTheReceipt()
    {
        var order = await ApprovedOrderAsync(tracked: true);

        var ex = await Assert.ThrowsAsync<AccountingException>(() => new PurchaseOrderService(_db)
            .CreateInvoiceFromOrderAsync(_companyId, order.Id, "SUP-1", new DateOnly(2026, 2, 1), vatDeductible: true));

        Assert.Contains("دواء", ex.Message, StringComparison.Ordinal);
        Assert.Contains("بإذن استلامٍ", ex.Message, StringComparison.Ordinal);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.GoodsReceipts.AnyAsync());
        Assert.False(await _db.PurchaseInvoices.AnyAsync(i => i.Status == PurchaseInvoiceStatus.Posted));
    }

    /// <summary><b>والطريقُ الذي يُدَلّ عليه يعمل</b> — إذنٌ بدفعته ثمّ الفاتورةُ من الأمر: تُرحَّل، والدفعةُ عشر.</summary>
    [Fact]
    public async Task AfterAReceiptWithItsBatch_TheOrderIsInvoiced()
    {
        var order = await ApprovedOrderAsync(tracked: true);
        var receipt = GoodsReceipt.CreateDraft(_companyId, _supplierId, order.Id, _warehouseId, new DateOnly(2026, 1, 2));
        receipt.AddLine("دواء", 10m, 10m, 10m, order.Lines[0].ProductId, "LOT-A", new DateOnly(2027, 1, 1));
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);
        await _db.SaveChangesAsync();

        var invoice = await new PurchaseOrderService(_db)
            .CreateInvoiceFromOrderAsync(_companyId, order.Id, "SUP-1", new DateOnly(2026, 2, 1), vatDeductible: true);

        Assert.Equal(PurchaseInvoiceStatus.Posted, invoice.Status);
        Assert.Equal(10m, (await _db.StockBatches.AsNoTracking().SingleAsync(b => b.BatchNumber == "LOT-A")).Quantity);
    }

    /// <summary><b>وغيرُ المتتبَّع يُستلم آليّاً كما كان.</b></summary>
    [Fact]
    public async Task AnUntrackedOrder_IsStillReceivedAutomatically()
    {
        var order = await ApprovedOrderAsync(tracked: false);

        var invoice = await new PurchaseOrderService(_db)
            .CreateInvoiceFromOrderAsync(_companyId, order.Id, "SUP-1", new DateOnly(2026, 2, 1), vatDeductible: true);

        Assert.Equal(PurchaseInvoiceStatus.Posted, invoice.Status);
        Assert.True(await _db.GoodsReceipts.AnyAsync(g => g.PurchaseOrderId == order.Id && g.Status == GoodsReceiptStatus.Posted));
    }

    private async Task<PurchaseOrder> ApprovedOrderAsync(bool tracked)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("صيدلية", "310123456700066", BusinessProfile.Healthcare);
        _companyId = company.Id;
        _warehouseId = (await _db.Warehouses.FirstAsync(w => w.CompanyId == _companyId && w.IsDefault)).Id;
        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        var product = Product.Create(_companyId, "P-LOT", "دواء", "علبة", ProductType.Stockable, salePrice: 20m);
        if (tracked)
            product.SetTracking(InventoryTracking.Batch, currentOnHand: 0m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;

        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 1, 1));
        order.AddLine("دواء", 10m, 10m, product.Id);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();
        return order;
    }
}
