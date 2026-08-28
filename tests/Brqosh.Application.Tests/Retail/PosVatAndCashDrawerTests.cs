using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Retail;

/// <summary>
/// (CEEF-228) نقطة البيع — الضريبة وعهدة الدرج.
///
/// عطلان كانا يعيشان في الكود بلا حارس:
/// 1. `PosSaleService.SellAsync` يستقبل `isVatRegistered` **ولا يستعمله ولو مرّة**.
/// 2. `PosShift.ExpectedCash = OpeningCash + TotalSales` و`TotalSales` يضمّ الشبكة.
/// </summary>
public class PosVatAndCashDrawerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _productId;

    public PosVatAndCashDrawerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("متجر", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Service, salePrice: 100m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _productId = product.Id;
    }

    private async Task<PosShift> OpenShiftAsync(decimal openingCash)
    {
        var shift = PosShift.Open(_companyId, "كاشير", openingCash);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();
        return shift;
    }

    /// <summary>
    /// منشأة بلا رقم ضريبيّ: بيع 100 يخرج 100 — لا 115.
    /// كان الوسيط مُهمَلاً فتُقيَّد 15 ريالاً التزاماً مُختلَقاً تجاه الهيئة عن كل 100 مبيعات.
    /// </summary>
    [Fact]
    public async Task NonVatRegisteredCompany_ChargesNoVat()
    {
        await SetupAsync();
        var shift = await OpenShiftAsync(0m);

        var result = await new PosSaleService(_db).SellAsync(
            _companyId, shift.Id, null, [(_productId, 1m)],
            isVatRegistered: false, PosPaymentMethod.Cash, "كاشير");

        var invoice = await _db.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == result.Invoice.Id);
        Assert.Equal(100m, invoice.Subtotal);
        Assert.Equal(0m, invoice.VatAmount);
    }

    /// <summary>والمسجَّلة تُحصّل 15% كما يجب — الإصلاح انتقائيّ لا شامل.</summary>
    [Fact]
    public async Task VatRegisteredCompany_StillChargesStandardVat()
    {
        await SetupAsync();
        var shift = await OpenShiftAsync(0m);

        var result = await new PosSaleService(_db).SellAsync(
            _companyId, shift.Id, null, [(_productId, 1m)],
            isVatRegistered: true, PosPaymentMethod.Cash, "كاشير");

        var invoice = await _db.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == result.Invoice.Id);
        Assert.Equal(100m, invoice.Subtotal);
        Assert.Equal(15m, invoice.VatAmount);
    }

    /// <summary>
    /// وردية بافتتاحيّ 500 ومبيعات نقد 200 ومبيعات شبكة 1,000:
    /// المتوقّع في الدرج **700** لا 1,700. كان الفرق يُظهر عجزاً وهمياً بقيمة كل مبيعات الشبكة.
    /// </summary>
    [Fact]
    public async Task ExpectedCash_CountsCashSalesOnly_NotCardSales()
    {
        await SetupAsync();
        var shift = await OpenShiftAsync(500m);
        var pos = new PosSaleService(_db);

        await pos.SellAsync(_companyId, shift.Id, null, [(_productId, 2m)],
            isVatRegistered: false, PosPaymentMethod.Cash, "كاشير");
        await pos.SellAsync(_companyId, shift.Id, null, [(_productId, 10m)],
            isVatRegistered: false, PosPaymentMethod.Card, "كاشير");

        var reloaded = await _db.PosShifts.AsNoTracking().SingleAsync(s => s.Id == shift.Id);

        Assert.Equal(1200m, reloaded.TotalSales);   // كل المبيعات
        Assert.Equal(200m, reloaded.CashSales);     // النقد وحده
        Assert.Equal(700m, reloaded.ExpectedCash);  // 500 افتتاحيّ + 200 نقد

        reloaded.Close(700m);
        Assert.Equal(0m, reloaded.CashDifference);  // درجٌ مضبوط، لا عجز وهميّ
    }
}
