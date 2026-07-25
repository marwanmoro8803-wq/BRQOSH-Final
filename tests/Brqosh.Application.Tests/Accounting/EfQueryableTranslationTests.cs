using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// يضمن أن استعلامات الصفحات الحرجة تُترجم إلى SQL — لا خصائص محسوبة في LINQ.
/// </summary>
public class EfQueryableTranslationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public EfQueryableTranslationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;

        var supplier = Supplier.Create(_companyId, "مورد");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var order = PurchaseOrder.CreateDraft(_companyId, supplier.Id, new DateOnly(2026, 7, 9));
        order.AddLine("مواد", 1m, 100m);
        order.Approve("PO-T");
        _db.PurchaseOrders.Add(order);

        var product = Product.Create(_companyId, "STK-1", "صنف مخزني", "قطعة", ProductType.Stockable);
        _db.Products.Add(product);

        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task PurchaseOrders_SumGrandTotalAsync_translates()
    {
        await SetupAsync();
        var total = await _db.PurchaseOrders
            .Where(o => o.CompanyId == _companyId)
            .SumGrandTotalAsync();
        Assert.Equal(115m, total);
    }

    [Fact]
    public async Task Products_WhereStockable_translates()
    {
        await SetupAsync();
        var count = await _db.Products
            .WhereActiveStockable(_companyId)
            .CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreditNotes_SumGrandTotalAsync_on_empty_set_translates()
    {
        await SetupAsync();
        var total = await _db.CreditNotes
            .Where(n => n.CompanyId == _companyId && n.Status == CreditNoteStatus.Posted)
            .SumGrandTotalAsync();
        Assert.Equal(0m, total);
    }
}
