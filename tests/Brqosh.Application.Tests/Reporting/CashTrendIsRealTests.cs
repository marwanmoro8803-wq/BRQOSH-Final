using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-216) اتجاه النقد في لوحة التحكم يجب أن يكون من القاعدة.
///
/// كان يُصطنع: أول نقطتين من خمس هما رقم الشهر الجاري مضروباً في 0.7 و0.85،
/// ويُعرض تحت عنوان «اتجاه النقد · شهري» كأنه تاريخ فعليّ. بيانات مالية ملفّقة
/// على لوحة يُتّخذ عليها قرار.
/// </summary>
public class CashTrendIsRealTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public CashTrendIsRealTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة الاتجاه", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل الاتجاه", "311122233300003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
    }

    /// <summary>السلسلة تغطي ستة أشهر متتابعة تنتهي بالشهر الجاري.</summary>
    [Fact]
    public async Task Trend_CoversSixConsecutiveMonths_EndingThisMonth()
    {
        await SetupAsync();
        var kpis = await new DashboardQuery(_db).ExecuteAsync(_companyId);

        Assert.NotNull(kpis.CashTrend);
        Assert.Equal(6, kpis.CashTrend!.Count);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var last = kpis.CashTrend[^1];
        Assert.Equal(today.Year, last.Year);
        Assert.Equal(today.Month, last.Month);

        for (var i = 1; i < kpis.CashTrend.Count; i++)
        {
            var prev = new DateOnly(kpis.CashTrend[i - 1].Year, kpis.CashTrend[i - 1].Month, 1);
            var cur = new DateOnly(kpis.CashTrend[i].Year, kpis.CashTrend[i].Month, 1);
            Assert.Equal(prev.AddMonths(1), cur);      // بلا فجوات ولا تكرار
        }
    }

    /// <summary>بلا حركة: كل النقاط أصفار — لا أرقام مصطنعة.</summary>
    [Fact]
    public async Task NoActivity_YieldsZeros_NotFabricatedValues()
    {
        await SetupAsync();
        var kpis = await new DashboardQuery(_db).ExecuteAsync(_companyId);

        Assert.All(kpis.CashTrend!, p =>
        {
            Assert.Equal(0m, p.Collected);
            Assert.Equal(0m, p.Paid);
        });
    }

    /// <summary>تحصيل فعليّ يظهر في شهره — والأشهر الأخرى تبقى صفراً.</summary>
    [Fact]
    public async Task RealReceipt_AppearsInItsOwnMonth_Only()
    {
        await SetupAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, today);
        invoice.AddLine("خدمة", 1m, 5000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var receipt = ReceiptVoucher.CreateDraft(
            _companyId, _customerId, today, 1500m, ReceiptMethod.Cash, "تحصيل");
        await new ReceiptVoucherPostingService(_db).PostAsync(receipt);

        var kpis = await new DashboardQuery(_db).ExecuteAsync(_companyId);
        var current = kpis.CashTrend!.Single(p => p.Year == today.Year && p.Month == today.Month);

        Assert.Equal(1500m, current.Collected);
        Assert.Equal(1500m, current.Net);

        // لا تسرّب إلى الأشهر السابقة — وهو بالضبط ما كان يفعله الاصطناع.
        Assert.All(kpis.CashTrend!.Where(p => !(p.Year == today.Year && p.Month == today.Month)),
            p => Assert.Equal(0m, p.Collected));
    }

    /// <summary>الصافي = تحصيل − سداد.</summary>
    [Fact]
    public async Task Net_IsCollectedMinusPaid()
    {
        await SetupAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, today);
        invoice.AddLine("خدمة", 1m, 9000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        await new ReceiptVoucherPostingService(_db).PostAsync(ReceiptVoucher.CreateDraft(
            _companyId, _customerId, today, 4000m, ReceiptMethod.Cash, "تحصيل"));

        var kpis = await new DashboardQuery(_db).ExecuteAsync(_companyId);
        var current = kpis.CashTrend!.Single(p => p.Year == today.Year && p.Month == today.Month);

        Assert.Equal(current.Collected - current.Paid, current.Net);
        Assert.Equal(4000m, current.Net);
    }
}
