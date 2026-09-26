using System.Diagnostics;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Inventory;

public class InventoryPerformanceTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _warehouseId;
    private readonly long _productId;
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public InventoryPerformanceTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
        _db =BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var company = new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "300000000000001", BusinessProfile.Manufacturing).GetAwaiter().GetResult();
        _companyId = company.Id;

        var product = Product.Create(_companyId, "PERF", "صنف أداء", "PCS", ProductType.Stockable);
        _db.Products.Add(product);
        _db.SaveChanges();
        _productId = product.Id;

        var wh = Warehouse.Create(_companyId, "WH-P", "مستودع", isDefault: true);
        _db.Warehouses.Add(wh);
        _db.SaveChanges();
        _warehouseId = wh.Id;
    }

    [Theory]
    [InlineData(1_000)]
    public async Task On_hand_query_scales_with_balance_table(int movementCount)
    {
        var inv = new InventoryService(_db);
        var date = new DateOnly(2026, 1, 1);

        for (var i = 0; i < movementCount; i++)
        {
            await inv.PostDocumentMovementAsync(_companyId, _warehouseId,
                StockMovementType.Receipt, date, $"حركة {i}", $"M{i}",
                [(_productId, 1m, 1m)]);
        }
        await _db.SaveChangesAsync();

        var sw = Stopwatch.StartNew();
        var onHand = await inv.GetOnHandAsync(_companyId, _productId, _warehouseId);
        sw.Stop();

        Assert.Equal(movementCount, onHand);
        // ‏(ح-٦٥) ‏الرقمُ يُطبع عند النجاح أيضاً — وكان لا يظهر إلّا في رسالة السقوط، فنشرت
        // ‏PERFORMANCE_BUDGETS «ناجحٌ ضمن الحدّ» بلا رقمٍ للاستعلام وحده.
        _output.WriteLine($"[أداء · رصيد المخزون] {movementCount} حركةً · الاستعلام وحده {sw.Elapsed.TotalMilliseconds:N2}ms");
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"استعلام الرصيد استغرق {sw.ElapsedMilliseconds}ms لـ {movementCount} حركة — المتوقع أقل من 500ms");
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
