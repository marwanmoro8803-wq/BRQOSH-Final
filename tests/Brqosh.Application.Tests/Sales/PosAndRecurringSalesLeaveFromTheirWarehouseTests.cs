using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١١٤ · <c>order-to-cash-procure-to-pay-inventory#12</c>) <b>نقطةُ البيع والفوترةُ الدوريّة تصرفان من مستودعهما — لا الافتراضيِّ أبداً.</b>
///
/// <para>بعد ح-١١٠ صارت الفاتورةُ تصرف من <c>WarehouseId</c>، لكنّ المسارين الآليّين اللذين يبنيان الفاتورةَ بأنفسهما (<c>PosSaleService</c> و
/// <c>RecurringInvoiceService</c>) لا يضبطانه — <b>فمتجرٌ بفرعين يبيع من كاشير فرعه الثاني ويُنقص مخزنَ الأوّل</b>. الوردية تحمل المستودعَ والخطةُ
/// تحمله، <b>والفارغُ هو الافتراضيّ كما كان</b>.</para>
/// </summary>
public sealed class PosAndRecurringSalesLeaveFromTheirWarehouseTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _aId;      // الافتراضيّ
    private long _bId;      // الفرع الثاني
    private long _customerId;
    private long _supplierId;
    private long _productId;

    public PosAndRecurringSalesLeaveFromTheirWarehouseTests()
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

    /// <summary><b>بيعُ وردية فُتحت على B ينقص B لا A</b> — ١٠ في كلٍّ، وبيع ٣ ⟵ B=٧ وA=١٠.</summary>
    [Fact]
    public async Task APosSaleOnAShiftOpenedOnB_ReducesB_AndLeavesA()
    {
        await SetupAsync();
        var shift = await OpenShiftAsync(_bId);

        await SellAsync(shift);

        Assert.Equal(7m, await OnHandAsync(_bId));
        Assert.Equal(10m, await OnHandAsync(_aId));
    }

    /// <summary><b>ووردية بلا مستودعٍ تبقى على الافتراضيّ</b> — بيع ٣ ⟵ A=٧ وB=١٠.</summary>
    [Fact]
    public async Task APosSaleOnAShiftWithoutAWarehouse_StaysOnTheDefault()
    {
        await SetupAsync();
        var shift = await OpenShiftAsync(null);

        await SellAsync(shift);

        Assert.Equal(7m, await OnHandAsync(_aId));
        Assert.Equal(10m, await OnHandAsync(_bId));
    }

    /// <summary><b>وخطةٌ دوريّة على B تُولّد فاتورةً تُنقص B</b> — توليدُ مستحقٍّ واحد (٣) ⟵ B=٧ وA=١٠.</summary>
    [Fact]
    public async Task ARecurringPlanOnB_GeneratesAnInvoiceThatReducesB()
    {
        await SetupAsync();
        await PlanAsync("R-B", _bId);

        var result = await new RecurringInvoiceService(_db).GenerateDueAsync(_companyId, new DateOnly(2026, 1, 5));

        Assert.True(result.InvoicesCreated == 1, string.Join("\n", result.Refusals));
        Assert.Equal(7m, await OnHandAsync(_bId));
        Assert.Equal(10m, await OnHandAsync(_aId));
        Assert.Equal(_bId, (await _db.SalesInvoices.AsNoTracking().SingleAsync(i => i.CompanyId == _companyId)).WarehouseId);
    }

    /// <summary><b>وخطةٌ بلا مستودعٍ تبقى على الافتراضيّ</b> — ⟵ A=٧ وB=١٠.</summary>
    [Fact]
    public async Task ARecurringPlanWithoutAWarehouse_StaysOnTheDefault()
    {
        await SetupAsync();
        await PlanAsync("R-A", null);

        var result = await new RecurringInvoiceService(_db).GenerateDueAsync(_companyId, new DateOnly(2026, 1, 5));

        Assert.True(result.InvoicesCreated == 1, string.Join("\n", result.Refusals));
        Assert.Equal(7m, await OnHandAsync(_aId));
        Assert.Equal(10m, await OnHandAsync(_bId));
    }

    /// <summary><b>والمجالُ</b>: معرّفٌ غيرُ موجبٍ يُرفض في الوردية والخطة، والخطةُ المنتهيةُ لا تُعدَّل.</summary>
    [Fact]
    public void TheDomain_RefusesABadWarehouse_AndAnEndedPlanChange()
    {
        Assert.Throws<AccountingException>(() => PosShift.Open(1, "كاشير", 0m, 0));
        Assert.Equal(5, PosShift.Open(1, "كاشير", 0m, 5).WarehouseId);
        Assert.Null(PosShift.Open(1, "كاشير").WarehouseId);

        var plan = RecurringInvoicePlan.Create(1, 1, "R", "خطة", RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1));
        Assert.Throws<AccountingException>(() => plan.SetWarehouse(0));
        plan.SetWarehouse(7);
        Assert.Equal(7, plan.WarehouseId);
        plan.End();
        Assert.Throws<AccountingException>(() => plan.SetWarehouse(null));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("متجر", "310123456700066", BusinessProfile.Retail);
        _companyId = company.Id;
        _aId = (await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId)).Id;
        var b = Warehouse.Create(_companyId, "WH-B", "فرع ثانٍ");
        _db.Warehouses.Add(b);
        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        var product = Product.Create(_companyId, "P-1", "ضمادة", "علبة", ProductType.Stockable, salePrice: 20m);
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _bId = b.Id;
        _customerId = customer.Id;
        _supplierId = supplier.Id;
        _productId = product.Id;
        await ReceiveAsync(10m, _aId);
        await ReceiveAsync(10m, _bId);
    }

    private async Task ReceiveAsync(decimal qty, long warehouseId)
    {
        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, new DateOnly(2025, 12, 1));
        order.AddLine("بضاعة", qty, 10m, _productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();
        var receipt = GoodsReceipt.CreateDraft(_companyId, _supplierId, order.Id, warehouseId, new DateOnly(2025, 12, 2));
        receipt.AddLine("بضاعة", qty, qty, 10m, _productId);
        await new GoodsReceiptPostingService(_db).PostAsync(receipt);
        await _db.SaveChangesAsync();
    }

    private async Task<PosShift> OpenShiftAsync(long? warehouseId)
    {
        var shift = PosShift.Open(_companyId, "كاشير", 0m, warehouseId);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();
        return shift;
    }

    private Task SellAsync(PosShift shift) =>
        new PosSaleService(_db).SellAsync(_companyId, shift.Id, null, [(_productId, 3m)],
            isVatRegistered: true, PosPaymentMethod.Cash, "كاشير");

    private async Task PlanAsync(string code, long? warehouseId)
    {
        var plan = RecurringInvoicePlan.Create(_companyId, _customerId, code, "اشتراك",
            RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1));
        plan.AddLine("صنف", 3m, 20m, _productId);
        plan.SetWarehouse(warehouseId);
        _db.RecurringInvoicePlans.Add(plan);
        await _db.SaveChangesAsync();
    }

    private Task<decimal> OnHandAsync(long warehouseId) =>
        new InventoryService(_db).GetOnHandAsync(_companyId, _productId, warehouseId);
}
