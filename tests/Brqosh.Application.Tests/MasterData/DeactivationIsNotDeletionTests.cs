using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.MasterData;

/// <summary>
/// (CEEF-229 · م2-١٧) <b>الإيقاف ليس حذفاً — والإصلاح طُبِّق على كيانٍ واحد وتُرك لسبعة.</b>
///
/// <para>أصلحت الدفعة م0-١٣ هذا العطل في <c>Account</c>: <c>Deactivate()</c> صار يضبط
/// <c>IsActive = false</c> وحده، وأُضيفت <c>Reactivate()</c>، لأن «الإيقاف حالةٌ تشغيلية لا
/// واقعةٌ محاسبية». <b>وبقيت سبعةُ كياناتٍ أخرى بالجسم القديم حرفاً بحرف</b>: الصنف والمستودع
/// والعميل والمورّد ومركز التكلفة والمشروع والعميل المحتمَل وقائمة المواد.</para>
///
/// <para><b>وأثرُه ليس نظرياً:</b> زرُّ الشاشة مكتوبٌ عليه «إيقاف»، ورسالةُ التأكيد تسأل
/// «إيقاف الصنف {الكود}؟» — ثم يضبط الكودُ <c>DeletedAtUtc</c>. ومرشّح الاستعلام العامّ
/// (<c>HasQueryFilter</c>) يُخفي كلَّ ما له تاريخ حذف، <b>فيختفي الصنف من قائمة الأصناف
/// تماماً</b> لا يظهر موقوفاً. ولا <c>Reactivate</c> في أيٍّ منها، فالإيقاف قرارٌ بلا رجعة —
/// والزرُّ لم يَعِد بذلك.</para>
///
/// <para>وهذا ما يقع عند تاجرٍ يوقف صنفاً موسمياً في نهاية موسمه، ثم يطلبه في الموسم التالي.</para>
/// </summary>
public class DeactivationIsNotDeletionTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;

    public DeactivationIsNotDeletionTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _companyId = new DatabaseInitializer(_db)
            .InitializeAsync("منشأة تجارية", "310123456700099", BusinessProfile.General)
            .GetAwaiter().GetResult().Id;
    }

    /// <summary>
    /// <b>القياس المركزيّ للصنف.</b> يوقفه التاجر في نهاية الموسم، فيبقى في قائمته موسوماً
    /// «موقوف» — لا يختفي. ثم يُعيد تفعيله في الموسم التالي.
    /// </summary>
    [Fact]
    public async Task DeactivatedProduct_StaysVisible_AndCanBeReactivated()
    {
        var product = Product.Create(_companyId, "P-1", "مظلات", "قطعة", ProductType.Stockable, 50m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        product.Deactivate();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.Products.SingleOrDefaultAsync(p => p.Id == product.Id);
        Assert.NotNull(stored);                 // لم يُحذف — ما زال في القائمة
        Assert.False(stored!.IsActive);         // لكنه موقوف
        Assert.Null(stored.DeletedAtUtc);       // والإيقاف ليس حذفاً

        stored.Reactivate();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        Assert.True((await _db.Products.SingleAsync(p => p.Id == product.Id)).IsActive);
    }

    /// <summary>والعميل كذلك — يتوقّف عن التعامل ثم يعود.</summary>
    [Fact]
    public async Task DeactivatedCustomer_StaysVisible_AndCanBeReactivated()
    {
        var customer = Customer.Create(_companyId, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        customer.Deactivate();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.Customers.SingleOrDefaultAsync(c => c.Id == customer.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsActive);
        Assert.Null(stored.DeletedAtUtc);

        stored.Reactivate();
        await _db.SaveChangesAsync();
        Assert.True(stored.IsActive);
    }

    /// <summary>والمورّد.</summary>
    [Fact]
    public async Task DeactivatedSupplier_StaysVisible_AndCanBeReactivated()
    {
        var supplier = Supplier.Create(_companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        supplier.Deactivate();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.Suppliers.SingleOrDefaultAsync(s => s.Id == supplier.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsActive);
        Assert.Null(stored.DeletedAtUtc);

        stored.Reactivate();
        await _db.SaveChangesAsync();
        Assert.True(stored.IsActive);
    }

    /// <summary>ومركز التكلفة — يُوقَف عند إقفال نشاطٍ ويُعاد عند استئنافه.</summary>
    [Fact]
    public async Task DeactivatedCostCentre_StaysVisible_AndCanBeReactivated()
    {
        var centre = CostCenter.Create(_companyId, "CC-1", "الفرع الشماليّ");
        _db.CostCenters.Add(centre);
        await _db.SaveChangesAsync();

        centre.Deactivate();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.CostCenters.SingleOrDefaultAsync(c => c.Id == centre.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsActive);
        Assert.Null(stored.DeletedAtUtc);

        stored.Reactivate();
        await _db.SaveChangesAsync();
        Assert.True(stored.IsActive);
    }

    /// <summary>والمستودع.</summary>
    [Fact]
    public async Task DeactivatedWarehouse_StaysVisible_AndCanBeReactivated()
    {
        var warehouse = Warehouse.Create(_companyId, "WH-2", "مستودع ثانٍ");
        _db.Warehouses.Add(warehouse);
        await _db.SaveChangesAsync();

        warehouse.Deactivate();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.Warehouses.SingleOrDefaultAsync(w => w.Id == warehouse.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsActive);
        Assert.Null(stored.DeletedAtUtc);

        stored.Reactivate();
        await _db.SaveChangesAsync();
        Assert.True(stored.IsActive);
    }

    /// <summary>
    /// <b>حارسٌ للحارس — ولولاه لكان «احذف المرشّح» يُمرّر كل ما سبق:</b> الحذف الناعم
    /// الحقيقيّ ما زال يُخفي الصفَّ. فالمرشّح يعمل، والمتغيّر الوحيد أن الإيقاف لم يعد يستدعيه.
    /// </summary>
    [Fact]
    public async Task ASoftDeletedRow_IsStillHiddenByTheQueryFilter()
    {
        var product = Product.Create(_companyId, "P-2", "صنف مؤقّت", "قطعة", ProductType.Service);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        // حذفٌ ناعم صريح — لا عبر Deactivate.
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE products SET DeletedAtUtc = '2026-01-01 00:00:00' WHERE Id = {0}", product.Id);
        _db.ChangeTracker.Clear();

        Assert.Null(await _db.Products.SingleOrDefaultAsync(p => p.Id == product.Id));
        Assert.NotNull(await _db.Products.IgnoreQueryFilters()
            .SingleOrDefaultAsync(p => p.Id == product.Id));
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
