using System.Windows.Threading;
using Brqosh.Desktop.Views.Pages;
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

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (CEEF-229 · م2-٢٦) <b>الوصلة على جانب الشراء — وهو الأثقل.</b>
///
/// <para>البيعُ الآجل بلا صنفٍ يُضخّم مجمل الربح؛ أمّا الشراءُ بلا صنفٍ فـ<b>لا تدخل البضاعة
/// المستودعَ إطلاقاً</b>. والقيدُ الماليّ يُسجَّل كاملاً (مدين مشتريات، ضريبة مدخلات، دائن
/// الموردون) بينما الكميّة تبقى صفراً إلى الأبد — ثم يصطدم أوّلُ بيعٍ بحارس «الرصيد غير كافٍ —
/// المتوفر 0.00»، فيقف الكاشير أمام العميل عاجزاً ولا يفهم لماذا.</para>
///
/// <para>ولذلك تُقاس السلسلة هنا من <b>صفّ الشبكة إلى الكميّة المتاحة</b>: الشراء يُدخل،
/// ثم البيع الذي كان يُرفض يمرّ.</para>
/// </summary>
public class PurchaseDialogSeamTests : IDisposable
{
    private static readonly Dispatcher UiDispatcher = StartUiThread();

    private readonly SqliteConnection _keepAlive;
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _supplierId;
    private readonly long _productId;
    private readonly long _warehouseId;

    /// <summary>
    /// (CEEF-229 · م2-٦) خيطُ الواجهة مشتركٌ الآن — انظر <see cref="WpfTestHost"/>.
    /// وكان كلُّ صنفٍ يُنشئ خيطاً وتطبيقاً، وWPF لا يسمح إلا بتطبيقٍ واحدٍ في العملية،
    /// فمن سبق ملكه — وضبطُ خصائصه من خيطٍ آخر يُسقط مُضيفَ الاختبارات كلَّه.
    /// </summary>
    private static Dispatcher StartUiThread() => WpfTestHost.Dispatcher;

    public PurchaseDialogSeamTests()
    {
        _keepAlive = new SqliteConnection("Data Source=purchaseseam;Mode=Memory;Cache=Shared");
        _keepAlive.Open();

        _db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, "Data Source=purchaseseam;Mode=Memory;Cache=Shared");
        _db.Database.EnsureCreated();

        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("متجر", "310123456700099", BusinessProfile.Retail)
            .GetAwaiter().GetResult().Id;

        var supplier = Supplier.Create(_companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        var product = Product.Create(_companyId, "P-1", "بضاعة", "قطعة", ProductType.Stockable, 100m);
        _db.Products.Add(product);
        _db.SaveChanges();
        _supplierId = supplier.Id;
        _productId = product.Id;
        _warehouseId = _db.Warehouses.Single(w => w.CompanyId == _companyId).Id;
    }

    private PurchaseInvoice BuildThroughTheDialog(long? productId, decimal qty, decimal cost) =>
        UiDispatcher.Invoke(() =>
        {
            var dialog = new PurchaseInvoiceDialog();
            dialog.RowsForTests.Add(new SalesInvoiceDialog.LineRow
            {
                ProductId = productId,
                Description = "بضاعة",
                Quantity = qty,
                UnitPrice = cost,
            });

            return dialog.BuildInvoiceFromRows(
                _companyId, _supplierId, new DateOnly(2026, 2, 1), "SUP-1", vatDeductible: true);
        });

    /// <summary>
    /// <b>القياس المركزيّ.</b> شراءٌ من الشاشة يُدخل البضاعة فعلاً — ثم يمرّ البيع الذي كان
    /// يُرفض بـ«الرصيد غير كافٍ».
    /// </summary>
    [Fact]
    public async Task APurchaseRowCarryingAProduct_PutsStockIntoTheWarehouse()
    {
        var invoice = BuildThroughTheDialog(_productId, 100m, 60m);

        Assert.Equal(_productId, invoice.Lines.Single().ProductId);

        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var inv = new InventoryService(_db);
        Assert.Equal(100m, await inv.GetOnHandAsync(_companyId, _productId, _warehouseId));

        // والتكلفة صارت معروفة، فالبيعُ التالي يُقيّد تكلفةً حقيقية.
        var product = await _db.Products.SingleAsync(p => p.Id == _productId);
        Assert.Equal(60m, product.WeightedAverageCost);
    }

    /// <summary>
    /// <b>والعطل القديم مقيسٌ عمداً:</b> شراءٌ بلا صنف يُسجَّل ماليّاً بالكامل — ذمّةُ المورّد
    /// وضريبةُ المدخلات — <b>ولا تدخل حبّةٌ واحدة</b>. وهذا ما كان يقف بالكاشير أمام العميل.
    /// </summary>
    [Fact]
    public async Task APurchaseRowWithoutAProduct_RecordsTheMoney_ButNoGoods()
    {
        var invoice = BuildThroughTheDialog(productId: null, 100m, 60m);

        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var inv = new InventoryService(_db);
        Assert.Equal(0m, await inv.GetOnHandAsync(_companyId, _productId, _warehouseId));
        Assert.True(invoice.Subtotal > 0m, "المال سُجّل — والبضاعة لم تدخل.");
    }

    // ── (CEEF-231 · ط1-٣ب) إذنُ الاستلام: الدفعةُ والاستلامُ الجزئيّ ─────────

    /// <summary>
    /// <b>القياسُ الحاكم: صنفٌ متتبَّعٌ يُستلَم من الشاشة بدفعته وصلاحيتها.</b>
    ///
    /// <para><b>ما كان:</b> شاشةُ الاستلام تنسخ سطورَ أمر الشراء حرفاً — <b>ولا حقلَ
    /// لرقم الدفعة</b>. فالمَخنَقُ يرفض الصنفَ المتتبَّع باسمه، <b>ولا تستطيع صيدليةٌ ولا
    /// مطعمٌ إدخالَ بضاعتِه أصلاً</b> — وكلُّ ما بُني من تتبّعِ صلاحيةٍ وصرفِ دواءٍ قائمٌ
    /// خلف بابٍ مقفل.</para>
    /// </summary>
    [Fact]
    public async Task ATrackedProduct_IsReceivedWithItsBatchAndExpiry()
    {
        var tracked = await TrackedProductAsync();
        var order = await ApprovedOrderAsync(tracked, qty: 100m, cost: 12m);

        var receipt = UiDispatcher.Invoke(() =>
        {
            var dialog = new GoodsReceiptDialog();
            dialog.RowsForTests.Add(new GoodsReceiptDialog.LineRow
            {
                ProductId = tracked,
                Description = "دواء",
                OrderedQuantity = 100m,
                ReceivedQuantity = 100m,
                UnitCost = 12m,
                IsTracked = true,
                BatchNumber = "LOT-A",
                ExpiryDate = new DateTime(2027, 6, 30),
            });

            return dialog.BuildReceiptFromRows(_companyId, _supplierId, order,
                _warehouseId, new DateOnly(2026, 2, 1));
        });

        await new GoodsReceiptPostingService(_db).PostAsync(receipt);

        var batch = await _db.StockBatches.SingleAsync(b => b.BatchNumber == "LOT-A");
        Assert.Equal(100m, batch.Quantity);
        Assert.Equal(new DateOnly(2027, 6, 30), batch.ExpiryDate);
    }

    /// <summary>
    /// <b>والاستلامُ الجزئيُّ يُدخل ما وصل وحدَه.</b>
    ///
    /// <para><b>وهو الحالةُ الغالبة</b>: يُطلب مئةٌ فيصل ستّون. <b>وقفلُ المستلَم على
    /// المطلوب يُثبت في المخزون أربعين لم تصل</b> — ثمّ يُباع منها فيقف الكاشيرُ أمام
    /// رفٍّ فارغ.</para>
    /// </summary>
    [Fact]
    public async Task APartialReceipt_BringsInOnlyWhatArrived()
    {
        var order = await ApprovedOrderAsync(_productId, qty: 100m, cost: 60m);

        var receipt = UiDispatcher.Invoke(() =>
        {
            var dialog = new GoodsReceiptDialog();
            dialog.RowsForTests.Add(new GoodsReceiptDialog.LineRow
            {
                ProductId = _productId,
                Description = "بضاعة",
                OrderedQuantity = 100m,
                ReceivedQuantity = 60m,
                UnitCost = 60m,
            });

            return dialog.BuildReceiptFromRows(_companyId, _supplierId, order,
                _warehouseId, new DateOnly(2026, 2, 1));
        });

        await new GoodsReceiptPostingService(_db).PostAsync(receipt);

        Assert.Equal(60m,
            await new InventoryService(_db).GetOnHandAsync(_companyId, _productId, _warehouseId));
    }

    /// <summary>صنفٌ متتبَّعٌ للقياس — يُنشأ مرّةً ويُعاد استعمالُه.</summary>
    private async Task<long> TrackedProductAsync()
    {
        var existing = await _db.Products.FirstOrDefaultAsync(
            p => p.CompanyId == _companyId && p.Code == "DRG-1");
        if (existing is not null) return existing.Id;

        var product = Product.Create(_companyId, "DRG-1", "دواء", "علبة", ProductType.Stockable, 50m);
        product.SetTracking(InventoryTracking.Batch, currentOnHand: 0m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product.Id;
    }

    private async Task<long> ApprovedOrderAsync(long productId, decimal qty, decimal cost)
    {
        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 1, 1));
        order.AddLine("بضاعة", qty, cost, productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();
        return order.Id;
    }

    /// <summary>وحارسٌ للحارس: صفٌّ فارغ لا يُنتج سطراً.</summary>
    [Fact]
    public void EmptyRows_ProduceNoLines()
    {
        var invoice = UiDispatcher.Invoke(() =>
        {
            var dialog = new PurchaseInvoiceDialog();
            dialog.RowsForTests.Add(new SalesInvoiceDialog.LineRow());
            return dialog.BuildInvoiceFromRows(
                _companyId, _supplierId, new DateOnly(2026, 2, 1), "SUP-2", vatDeductible: true);
        });

        Assert.Empty(invoice.Lines);
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        _keepAlive.Dispose();
        GC.SuppressFinalize(this);
    }
}
