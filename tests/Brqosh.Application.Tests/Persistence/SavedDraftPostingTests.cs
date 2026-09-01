using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Persistence;

/// <summary>
/// (CEEF-230 · ط2-٠) <b>مستندٌ حُفظ مسودّةً ثمّ رُحِّل — الاصطلاحُ الآخر.</b>
///
/// <para><b>ما كشفه القياس:</b> في المستودع <b>اصطلاحان لاستدعاء خدمات الترحيل ولا شيءَ
/// يُعلنهما</b>: أكثرُ الشاشات تبني مسودّةً وتُرحّلها مباشرةً، <b>وشاشةُ الرواتب وحدَها
/// تحفظ ثمّ تُرحّل</b>. والخدماتُ كانت تُضيف المستندَ بلا شرط — <b>فسقطت شاشةُ الرواتب
/// بـ<c>23505</c> ولم تُرحّل شيئاً قطّ</b>.</para>
///
/// <para><b>وهذا الاختبارُ يحرس الصنفَ لا الحالة:</b> يُثبت أنّ المسارَ الثاني يعمل في
/// خدماتٍ مختلفة، <b>فلا يعود اختيارُ الاصطلاح مصيدةً لمن يكتب شاشةً غداً</b>.</para>
///
/// <para><b>وقِيس أنّ الرواتب كانت الوحيدةَ المصابة</b> اليوم — لا استنتاجاً بل بمسحٍ
/// لكلّ شاشةٍ تحفظ مستنداً قبل ترحيله. <b>لكنّ إصلاحَ الحالة وحدها يترك الفخَّ منصوباً.</b></para>
/// </summary>
public class SavedDraftPostingTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _warehouseId;

    public SavedDraftPostingTests()
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

    /// <summary><b>فاتورةُ مبيعاتٍ حُفظت ثمّ رُحِّلت.</b></summary>
    [Fact]
    public async Task ASavedSalesInvoice_Posts()
    {
        await SetupAsync();
        var invoice = SalesInvoice.CreateDraft(_companyId, await CustomerAsync(), new DateOnly(2026, 3, 1));
        invoice.AddLine("خدمة", 1m, 500m);
        _db.SalesInvoices.Add(invoice);
        await _db.SaveChangesAsync();

        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(SalesInvoiceStatus.Posted, invoice.Status);
        Assert.Equal(1, await _db.SalesInvoices.CountAsync(i => i.CompanyId == _companyId));
    }

    /// <summary><b>وفاتورةُ مشترياتٍ كذلك.</b></summary>
    [Fact]
    public async Task ASavedPurchaseInvoice_Posts()
    {
        await SetupAsync();
        var invoice = PurchaseInvoice.CreateDraft(_companyId, await SupplierAsync(),
            new DateOnly(2026, 3, 1), "SUP-7", vatDeductible: true);
        invoice.AddLine("خدمة", 1m, 400m);
        _db.PurchaseInvoices.Add(invoice);
        await _db.SaveChangesAsync();

        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(1, await _db.PurchaseInvoices.CountAsync(i => i.CompanyId == _companyId));
    }

    /// <summary><b>وسندُ قبضٍ كذلك.</b></summary>
    [Fact]
    public async Task ASavedReceiptVoucher_Posts()
    {
        await SetupAsync();
        var voucher = ReceiptVoucher.CreateDraft(_companyId, await CustomerAsync(),
            new DateOnly(2026, 3, 1), 300m, ReceiptMethod.Cash, "تحصيل");
        _db.ReceiptVouchers.Add(voucher);
        await _db.SaveChangesAsync();

        await new ReceiptVoucherPostingService(_db).PostAsync(voucher, []);

        Assert.Equal(1, await _db.ReceiptVouchers.CountAsync(v => v.CompanyId == _companyId));
    }

    /// <summary><b>وإذنُ استلامٍ كذلك.</b></summary>
    [Fact]
    public async Task ASavedGoodsReceipt_Posts()
    {
        await SetupAsync();
        var supplierId = await SupplierAsync();
        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var order = PurchaseOrder.CreateDraft(_companyId, supplierId, new DateOnly(2026, 3, 1));
        order.AddLine("بضاعة", 5m, 20m, product.Id);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await _db.SaveChangesAsync();

        var receipt = GoodsReceipt.CreateDraft(_companyId, supplierId, order.Id,
            _warehouseId, new DateOnly(2026, 3, 2));
        receipt.AddLine("بضاعة", 5m, 5m, 20m, product.Id);
        _db.GoodsReceipts.Add(receipt);
        await _db.SaveChangesAsync();

        await new GoodsReceiptPostingService(_db).PostAsync(receipt);

        Assert.Equal(1, await _db.GoodsReceipts.CountAsync(g => g.CompanyId == _companyId));
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task<long> CustomerAsync()
    {
        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.CompanyId == _companyId);
        if (existing is not null) return existing.Id;

        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer.Id;
    }

    private async Task<long> SupplierAsync()
    {
        var existing = await _db.Suppliers.FirstOrDefaultAsync(s => s.CompanyId == _companyId);
        if (existing is not null) return existing.Id;

        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        return supplier.Id;
    }

    private async Task SetupAsync()
    {
        if (_companyId != 0) return;
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة المسارين", "310123456700066", BusinessProfile.Retail);
        _companyId = company.Id;
        _warehouseId = (await _db.Warehouses.FirstAsync(w => w.CompanyId == _companyId)).Id;
    }
}
