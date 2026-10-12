using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٦ · ‏`order-to-cash-procure-to-pay-inventory#15`) <b>رحلةُ ر-١٤: الشراءُ حتى الدفع، والمخزونُ والمرتجعات —
/// ‏تُقاس بالمال حيث يظهر، لا بشاشةٍ تُفتح.</b>
///
/// <para>‏معيارُ الإطلاق كان بلا رحلةٍ للشراء ولا للمخزون ولا للمرتجعات، و«٩٧٪» لم تقِس هذه المسارات من أوّلها إلى آخرها. وكانت
/// ‏<c>GoldenPathPurchaseCycleTests</c> تقف عند السداد. فهنا الدورةُ كلُّها على منشأةٍ واحدة: أمرُ شراءٍ ⟵ فاتورتُه باستلامٍ آليّ ⟵
/// ‏سدادُ المورّد ⟵ بيعٌ ⟵ مرتجعُ مبيعات ⟵ مرتجعُ مشتريات ⟵ جردٌ بعجز.</para>
///
/// <para>‏<b>والأرقامُ محسوبةٌ قبل التشغيل:</b> عشرٌ بعشرين (٢٠٠ + ضريبة ٣٠ = ٢٣٠) تُسدَّد كاملة؛ وتُباع أربعٌ بخمسين (٢٠٠ + ٣٠)؛
/// ‏ويعود واحدٌ من العميل (٥٠ + ٧٫٥٠)؛ ويُردّ اثنان إلى المورّد (٤٠ + ٦ = ٤٦)؛ والجردُ يجد أربعاً والنظامُ خمساً.</para>
/// <list type="bullet">
/// <item>المخزون: ١٠ − ٤ + ١ − ٢ − ١ = <b>٤</b> بعشرين = <b>٨٠</b> — في الطبقات وفي ١١٠٧ معاً.</item>
/// <item>تكلفةُ المبيعات (٥١٠١): ٤ × ٢٠ − ١ × ٢٠ = <b>٦٠</b>.</item>
/// <item>المورّد: ٢٣٠ − ٢٣٠ − ٤٦ = <b>−٤٦</b> لنا عنده — في دفتره المساعد وفي ٢١٠١ معاً.</item>
/// <item>الضريبة: مخرجاتٌ ٣٠ − ٧٫٥٠ = <b>٢٢٫٥٠</b>، ومدخلاتٌ قابلةٌ للخصم ٣٠ − ٦ = <b>٢٤</b>.</item>
/// </list>
/// </summary>
public class TheProcureToPayJourneyReconcilesEveryLedgerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly DateOnly _day = new(2026, 7, 9);
    private long _companyId;
    private long _supplierId;
    private long _customerId;
    private long _productId;
    private long _warehouseId;

    public TheProcureToPayJourneyReconcilesEveryLedgerTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task The_whole_cycle_leaves_stock_cost_payables_and_vat_where_the_numbers_say()
    {
        await JourneyAsync();

        Assert.Equal(4m, await new InventoryService(_db).GetCompanyOnHandAsync(_companyId, _productId));
        var stock = (await new StockBalanceQuery(_db).ExecuteAsync(_companyId)).Single(r => r.ProductId == _productId);
        Assert.Equal(80m, Math.Round(stock.TotalValue, 2, MidpointRounding.AwayFromZero));

        Assert.Equal(80m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.Inventory));
        Assert.Equal(60m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.CostOfGoodsSold));

        Assert.Equal(-46m, (await new SupplierBalanceQuery(_db).ForSupplierAsync(_companyId, _supplierId)).Balance);
        // ‏٢١٠١ بطبيعته الدائنة: رصيدٌ مدينٌ بستّةٍ وأربعين يُقرأ −٤٦ — مطابقاً للدفتر المساعد.
        Assert.Equal(-46m, await LedgerAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable));

        var vat = await new VatReturnQuery(_db).ExecuteAsync(_companyId, _day.AddDays(-1), _day.AddDays(10));
        Assert.Equal(22.5m, vat.NetOutputVat);
        Assert.Equal(24m, vat.NetDeductibleInputVat);
    }

    /// <summary>‏وكلُّ دفترٍ مساعدٍ يطابق حسابَه في الأستاذ — الذممُ المدينةُ والدائنةُ والمخزونُ وما استُلم ولم يُفوتَر.</summary>
    [Fact]
    public async Task Every_subledger_agrees_with_its_ledger_account_at_the_end()
    {
        await JourneyAsync();

        await LedgerIdentities.AssertAllAsync(_db, _companyId);
    }

    private async Task JourneyAsync()
    {
        await SetupAsync();

        // ‏١) أمرُ شراءٍ ⟵ فاتورتُه باستلامٍ آليّ: ١٠ × ٢٠ = ٢٠٠ + ٣٠.
        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, _day);
        order.AddLine("توريد", 10m, 20m, _productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        var purchase = await new PurchaseOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, "SUP-R14", _day, vatDeductible: true);

        // ‏٢) سدادُ المورّد كاملاً.
        var voucher = PaymentVoucher.CreateDraft(_companyId, _supplierId, _day.AddDays(1),
            purchase.GrandTotal(), ReceiptMethod.Bank, "سداد ر-١٤", "TRF-R14");
        await new PaymentVoucherPostingService(_db).PostAsync(voucher);

        // ‏٣) بيعُ أربعٍ بخمسين.
        var sale = SalesInvoice.CreateDraft(_companyId, _customerId, _day.AddDays(2));
        sale.AddLine("بيع", 4m, 50m, _productId);
        await new SalesInvoicePostingService(_db).PostAsync(sale);

        // ‏٤) مرتجعُ مبيعات: يعود واحد.
        var credit = CreditNote.CreateDraft(_companyId, _customerId, sale.Id, _day.AddDays(3), "مرتجع عميل");
        credit.AddLine("مرتجع", 1m, 50m, _productId);
        await new CreditNotePostingService(_db).PostAsync(credit);

        // ‏٥) مرتجعُ مشتريات: يُردّ اثنان إلى المورّد بعشرين.
        var debit = DebitNote.CreateDraft(_companyId, _supplierId, purchase.Id, _day.AddDays(4), "مرتجع للمورّد");
        debit.AddLine("مرتجع", 2m, 20m, _productId);
        await new DebitNotePostingService(_db).PostAsync(debit);

        // ‏٦) جردٌ: النظامُ خمسٌ والمعدودُ أربع.
        await new StockCountAdjustmentService(_db).PostVariancesAsync(
            _companyId, _warehouseId, _day.AddDays(5), [(_productId, null, 5m, 4m, null)], "admin");
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة ر-١٤", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var supplier = Supplier.Create(_companyId, "مورد ر-١٤", "312222222200003");
        var customer = Customer.Create(_companyId, "عميل ر-١٤", "311122233300003");
        var product = Product.Create(_companyId, "P-R14", "صنف ر-١٤", "قطعة", ProductType.Stockable);
        _db.Suppliers.Add(supplier);
        _db.Customers.Add(customer);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;
        _customerId = customer.Id;
        _productId = product.Id;
        _warehouseId = (await new InventoryService(_db).GetDefaultWarehouseAsync(_companyId)).Id;
    }

    /// <summary>رصيدٌ موقَّعٌ بطبيعة الحساب: المدينُ للأصول والمصروفات، والدائنُ للالتزامات.</summary>
    private async Task<decimal> LedgerAsync(string code)
    {
        var rows = await new TrialBalanceQuery(_db)
            .ExecuteAsync(_companyId, new DateOnly(2000, 1, 1), new DateOnly(2100, 12, 31));
        var row = rows.FirstOrDefault(r => r.AccountCode == code);
        if (row is null) return 0m;
        var debitNature = code.StartsWith('1') || code.StartsWith('5');
        return Math.Round(debitNature ? row.TotalDebit - row.TotalCredit : row.TotalCredit - row.TotalDebit,
            2, MidpointRounding.AwayFromZero);
    }
}
