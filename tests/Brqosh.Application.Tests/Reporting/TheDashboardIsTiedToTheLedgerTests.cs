using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · «هل الـKPI حقيقية ومترابطة؟») <b>كلُّ بطاقةٍ في لوحة التحكم تُطابق أستاذَ المنشأة نفسَه</b> — لا نظامَ
/// حسابٍ موازياً. وسبق هذا حارسٌ للإيراد والمصروف والربح (<see cref="DashboardAgreesWithIncomeStatementTests"/>)؛ وهذا يُتمّه
/// لباقي البطاقات: الذممُ والموردون والمخزون وصافي الضريبة والنقد.
/// </summary>
public class TheDashboardIsTiedToTheLedgerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    public TheDashboardIsTiedToTheLedgerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<decimal> NetAsync(string code, bool debitNature)
    {
        var rows = await new TrialBalanceQuery(_db).ExecuteAsync(_companyId, new DateOnly(2000, 1, 1), _today);
        var row = rows.FirstOrDefault(r => r.AccountCode == code);
        if (row is null)
            return 0m;
        return debitNature ? row.TotalDebit - row.TotalCredit : row.TotalCredit - row.TotalDebit;
    }

    [Fact]
    public async Task EveryCard_AgreesWithTheLedgerAfterPurchaseSaleAndPartialCollection()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة اللوحة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل", "311122233300003");
        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        var product = Product.Create(_companyId, "KT-P", "صنف", "قطعة", ProductType.Stockable, 500m);
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        // ‏شراء ٢٠ قطعة بـ٥٠٠، ثم بيع ٥ بـ٧٥٠، ثم تحصيل جزء، ثم سداد جزء للمورد.
        var order = PurchaseOrder.CreateDraft(_companyId, supplier.Id, _today);
        order.AddLine("بضاعة", 20m, 500m, product.Id);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        var purchase = await new PurchaseOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, "SUP-KT-1", _today, vatDeductible: true);

        var sale = SalesInvoice.CreateDraft(_companyId, customer.Id, _today);
        sale.AddLine("بيع", 5m, 750m, product.Id);
        await new SalesInvoicePostingService(_db).PostAsync(sale);

        await new ReceiptVoucherPostingService(_db).PostAsync(
            ReceiptVoucher.CreateDraft(_companyId, customer.Id, _today, 1_000m, ReceiptMethod.Bank, "تحصيل جزئي", "TRF-KT-1"));
        await new PaymentVoucherPostingService(_db).PostAsync(
            PaymentVoucher.CreateDraft(_companyId, supplier.Id, _today, 4_000m, ReceiptMethod.Bank, "سداد جزئي", "TRF-KT-2"));

        var kpis = await new DashboardQuery(_db).ExecuteAsync(_companyId);

        // ‏الذممُ المدينة = حساب العملاء في الأستاذ (وهو ما يطابقه الدفترُ المساعد).
        Assert.Equal(await NetAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable, debitNature: true), kpis.Receivables);
        Assert.True(kpis.Receivables > 0m);

        // ‏الدائنون = حساب الموردين.
        Assert.Equal(await NetAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable, debitNature: false), kpis.Payables);
        Assert.True(kpis.Payables > 0m);

        // ‏المخزون = حساب المخزون.
        Assert.Equal(await NetAsync(SaudiChartOfAccounts.SystemCodes.Inventory, debitNature: true), kpis.InventoryValue);

        // ‏صافي الضريبة = مخرجات − مدخلات (جميعُها مرحَّلةٌ هذا الشهر).
        var output = await NetAsync(SaudiChartOfAccounts.SystemCodes.VatOutput, debitNature: false);
        var input = await NetAsync(SaudiChartOfAccounts.SystemCodes.VatInput, debitNature: true);
        Assert.Equal(output - input, kpis.NetVatThisMonth);

        // ‏التدفق النقدي = التحصيل − السداد، من السندات المرحَّلة.
        Assert.Equal(1_000m, kpis.CollectedThisMonth);
        Assert.Equal(4_000m, kpis.PaidThisMonth);
    }
}
