using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (CEEF-214) لوحة التحكم يجب ألّا تناقض قائمة الدخل.
///
/// اكتُشف من **لقطة شاشة حيّة**: اللوحة عرضت «صافي الربح −7,187.50» بينما قائمة الدخل
/// على نفس البيانات وفي نفس اللحظة عرضت «صافي الدخل +1,250». السبب أن اللوحة كانت تحسب:
///   الإيرادات = مبيعات **شاملة الضريبة** (والضريبة أمانة للهيئة لا إيراد)
///   المصروفات = مشتريات + سدادات (شراء المخزون **أصل** لا مصروف، والسداد حركة نقد —
///               ومع ذلك يُحتسب الاثنان فيزدوج الرقم)
///   صافي الربح = مبيعات − مشتريات (ليس ربحاً بأي تعريف)
/// صاحب العمل ينظر فيظنّ أنه يخسر وهو رابح.
/// </summary>
public class DashboardAgreesWithIncomeStatementTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _supplierId;
    private long _productId;
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    public DashboardAgreesWithIncomeStatementTests()
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
            .InitializeAsync("شركة اللوحة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل اللوحة", "311122233300003");
        var supplier = Supplier.Create(_companyId, "مورد اللوحة", "312222222200003");
        var product = Product.Create(_companyId, "DB-P", "صنف اللوحة", "قطعة", ProductType.Stockable, 500m);
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _supplierId = supplier.Id;
        _productId = product.Id;
    }

    /// <summary>سيناريو اللقطة: شراء ثم بيع — اللوحة وقائمة الدخل تتفقان.</summary>
    [Fact]
    public async Task PurchaseThenSale_DashboardProfitMatchesIncomeStatement()
    {
        await SetupAsync();

        // شراء بضاعة بـ10,000 (أصل مخزون — ليس مصروفاً)
        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, _today);
        order.AddLine("بضاعة", 20m, 500m, _productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await new PurchaseOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, "SUP-DB-1", _today, vatDeductible: true);

        // بيع 5 وحدات بـ750 = 3,750 صافي (+ ضريبة)
        var sale = SalesInvoice.CreateDraft(_companyId, _customerId, _today);
        sale.AddLine("بيع", 5m, 750m, _productId);
        await new SalesInvoicePostingService(_db).PostAsync(sale);

        var monthStart = new DateOnly(_today.Year, _today.Month, 1);
        var pnl = await new IncomeStatementQuery(_db).ExecuteAsync(_companyId, monthStart, _today);
        var kpis = await new DashboardQuery(_db).ExecuteAsync(_companyId);

        // الثابت الجوهري: لا تناقض بين الشاشتين.
        Assert.Equal(pnl.NetIncome, kpis.NetProfitThisMonth);
        Assert.Equal(pnl.TotalRevenue, kpis.RevenueThisMonth);
        Assert.Equal(pnl.TotalExpenses, kpis.ExpensesThisMonth);

        // ورقمياً: الربح موجب (بيع 3,750 − تكلفة 2,500) لا سالب كما كان يُعرض.
        Assert.True(kpis.NetProfitThisMonth > 0,
            $"الربح يجب أن يكون موجباً — عُرض {kpis.NetProfitThisMonth}");
    }

    /// <summary>الإيراد صافٍ من الضريبة — الضريبة أمانة للهيئة لا دخل للمنشأة.</summary>
    [Fact]
    public async Task Revenue_ExcludesVat()
    {
        await SetupAsync();

        var sale = SalesInvoice.CreateDraft(_companyId, _customerId, _today);
        sale.AddLine("خدمة", 1m, 1000m);      // 1000 صافي + 150 ضريبة = 1150 إجمالي
        await new SalesInvoicePostingService(_db).PostAsync(sale);

        var kpis = await new DashboardQuery(_db).ExecuteAsync(_companyId);

        Assert.Equal(1000m, kpis.RevenueThisMonth);      // لا 1150
        Assert.Equal(1150m, kpis.SalesThisMonth);        // حجم النشاط يبقى شاملاً — مؤشر مختلف
    }

    /// <summary>سداد المورد حركة نقد لا مصروف — ولا يُضاعف الرقم.</summary>
    [Fact]
    public async Task SupplierPayment_DoesNotInflateExpenses()
    {
        await SetupAsync();

        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, _today);
        order.AddLine("بضاعة", 10m, 100m, _productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        var purchase = await new PurchaseOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, "SUP-DB-2", _today, vatDeductible: true);

        var before = await new DashboardQuery(_db).ExecuteAsync(_companyId);

        var voucher = Brqosh.Domain.Treasury.PaymentVoucher.CreateDraft(
            _companyId, _supplierId, _today, purchase.GrandTotal(),
            Brqosh.Domain.Treasury.ReceiptMethod.Bank, "سداد", "TRF-DB-1");
        await new Brqosh.Infrastructure.Treasury.PaymentVoucherPostingService(_db).PostAsync(voucher);

        var after = await new DashboardQuery(_db).ExecuteAsync(_companyId);

        // السداد لم يغيّر المصروفات — شراء المخزون أصل، والسداد تسوية ذمة.
        Assert.Equal(before.ExpensesThisMonth, after.ExpensesThisMonth);
    }
}
