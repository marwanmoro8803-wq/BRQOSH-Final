using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>
/// CEEF-201 — ترابط الحركة المالية عبر كل الأقسام في سلسلة واحدة متصلة.
///
/// السيناريوهات القائمة (Golden) تفحص كل عملية بمعزل. هذا الملف يفحص ما لا تفحصه:
/// **تطابق الدفاتر المساعدة مع الأستاذ العام** بعد كل خطوة من سلسلة مال حقيقية —
/// وهو الموضع الذي تنكسر فيه أنظمة المحاسبة عملياً (رصيد العميل يقول شيئاً والأستاذ يقول آخر).
///
/// ثوابت تُفحص بعد **كل** خطوة:
///   1. ميزان المراجعة متوازن (مدين = دائن).
///   2. معادلة الميزانية: الأصول = الخصوم + حقوق الملكية.
///   3. دفتر العملاء المساعد = رصيد حساب المدينين في الأستاذ (1104).
///   4. دفتر الموردين المساعد = رصيد حساب الدائنين في الأستاذ (2101).
/// </summary>
public class MoneyFlowInterlockTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _supplierId;
    private long _productId;
    private readonly DateOnly _day = new(2026, 7, 9);

    public MoneyFlowInterlockTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditLogImmutabilityInterceptor())
            .Options);
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
            .InitializeAsync("شركة الترابط", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل الترابط", "311122233300003");
        var supplier = Supplier.Create(_companyId, "مورد الترابط", "312222222200003");
        var product = Product.Create(_companyId, "IL-P", "صنف الترابط", "قطعة", ProductType.Stockable, 200m);
        _db.Customers.Add(customer);
        _db.Suppliers.Add(supplier);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _supplierId = supplier.Id;
        _productId = product.Id;
    }

    /// <summary>
    /// السلسلة الكاملة: شراء ← مخزون ← بيع ← ذمم ← مرتجع ← تحصيل ← سداد مورد.
    /// بعد كل خطوة تُفحص الثوابت الأربعة — أي انفصال بين مساعد وأستاذ يفشل هنا.
    /// </summary>
    [Fact]
    public async Task FullMoneyChain_KeepsSubLedgersTiedToGeneralLedger_AtEveryStep()
    {
        await SetupAsync();
        await AssertAllInvariantsAsync("البداية (قبل أي حركة)");

        // 1) شراء: أمر ← استلام تلقائي ← فاتورة مشتريات مرحّلة
        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, _day);
        order.AddLine("بضاعة للبيع", 20m, 100m, _productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        var purchase = await new PurchaseOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, "SUP-IL-001", _day, vatDeductible: true);
        Assert.Equal(PurchaseInvoiceStatus.Posted, purchase.Status);
        await AssertAllInvariantsAsync("بعد فاتورة المشتريات");

        // المخزون دخل فعلاً
        Assert.Equal(20m, await new InventoryService(_db).GetCompanyOnHandAsync(_companyId, _productId));

        // 2) بيع: فاتورة مبيعات مرحّلة (إيراد + ضريبة مخرجات + تكلفة مبيعات + صرف مخزون)
        var sale = SalesInvoice.CreateDraft(_companyId, _customerId, _day.AddDays(1));
        sale.AddLine("بيع بضاعة", 5m, 300m, _productId);
        await new SalesInvoicePostingService(_db).PostAsync(sale);
        Assert.Equal(SalesInvoiceStatus.Posted, sale.Status);
        await AssertAllInvariantsAsync("بعد فاتورة المبيعات");

        // المخزون خرج فعلاً (20 - 5)
        Assert.Equal(15m, await new InventoryService(_db).GetCompanyOnHandAsync(_companyId, _productId));

        // 3) مرتجع جزئي: إشعار دائن (يعكس الإيراد والضريبة ويعيد المخزون)
        var creditNote = CreditNote.CreateDraft(_companyId, _customerId, sale.Id, _day.AddDays(2), "مرتجع مبيعات");
        creditNote.AddLine("مرتجع بضاعة", 2m, 300m, _productId);
        await new CreditNotePostingService(_db).PostAsync(creditNote);
        await AssertAllInvariantsAsync("بعد إشعار الدائن");

        // 4) تحصيل من العميل: سند قبض بنكي بالمتبقي
        var remaining = (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId)).Balance;
        Assert.True(remaining > 0m, "يجب أن يبقى على العميل رصيد بعد المرتجع");
        var receipt = ReceiptVoucher.CreateDraft(
            _companyId, _customerId, _day.AddDays(3), remaining, ReceiptMethod.Bank, "تحصيل", "TRF-IL-1");
        await new ReceiptVoucherPostingService(_db).PostAsync(receipt);
        await AssertAllInvariantsAsync("بعد سند القبض");

        // ذمة العميل صفر — الدفتر المساعد والأستاذ متفقان على ذلك (يفحصه الثابت 3)
        Assert.Equal(0m, (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId)).Balance);

        // 5) سداد المورد: سند صرف بكامل قيمة فاتورة الشراء
        var payment = PaymentVoucher.CreateDraft(
            _companyId, _supplierId, _day.AddDays(4), purchase.GrandTotal(),
            ReceiptMethod.Bank, "سداد مورد", "TRF-IL-2");
        await new PaymentVoucherPostingService(_db).PostAsync(payment);
        await AssertAllInvariantsAsync("بعد سداد المورد");

        // ذمة المورد صفر
        var supplierBalances = await new SupplierBalanceQuery(_db).AllBalancesAsync(_companyId);
        Assert.Equal(0m, supplierBalances.GetValueOrDefault(_supplierId));
    }

    /// <summary>
    /// إلغاء فاتورة مبيعات مرحّلة يجب أن يعيد الدفتر المساعد والأستاذ معاً إلى ما قبلها —
    /// لا أن يصحح أحدهما دون الآخر.
    /// </summary>
    [Fact]
    public async Task CancellingPostedInvoice_RestoresBothSubLedgerAndGeneralLedger()
    {
        await SetupAsync();

        var order = PurchaseOrder.CreateDraft(_companyId, _supplierId, _day);
        order.AddLine("بضاعة", 10m, 100m, _productId);
        await new PurchaseOrderService(_db).ApproveAsync(order);
        await new PurchaseOrderService(_db).CreateInvoiceFromOrderAsync(
            _companyId, order.Id, "SUP-IL-C1", _day, vatDeductible: true);

        var arBeforeSale = await GlBalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable);

        var sale = SalesInvoice.CreateDraft(_companyId, _customerId, _day.AddDays(1));
        sale.AddLine("بيع", 3m, 250m, _productId);
        await new SalesInvoicePostingService(_db).PostAsync(sale);
        await AssertAllInvariantsAsync("بعد البيع (قبل الإلغاء)");
        Assert.True(await GlBalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable) > arBeforeSale);

        await new SalesInvoiceCancellationService(_db)
            .CancelAsync(_companyId, sale.Id, _day.AddDays(2), "admin");
        await AssertAllInvariantsAsync("بعد إلغاء فاتورة المبيعات");

        // أثر الفاتورة انمحى من الأستاذ ومن الدفتر المساعد معاً
        Assert.Equal(arBeforeSale, await GlBalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable));
        Assert.Equal(0m, (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId)).Balance);
    }

    // ── الثوابت ───────────────────────────────────────────────────────────────

    private async Task AssertAllInvariantsAsync(string step)
    {
        // 1) ميزان المراجعة متوازن
        var rows = await new TrialBalanceQuery(_db).ExecuteAsync(_companyId, _day.AddYears(-1), _day.AddYears(1));
        var debit = rows.Sum(r => r.TotalDebit);
        var credit = rows.Sum(r => r.TotalCredit);
        Assert.True(debit == credit,
            $"[{step}] ميزان المراجعة غير متوازن: مدين {debit} ≠ دائن {credit}");

        // 2) معادلة الميزانية: الأصول = الخصوم + حقوق الملكية
        var bs = await new BalanceSheetQuery(_db).ExecuteAsync(_companyId, _day.AddYears(1));
        Assert.True(bs.IsBalanced,
            $"[{step}] الميزانية غير متوازنة: أصول {bs.TotalAssets} ≠ خصوم+حقوق {bs.TotalLiabilitiesAndEquity}");

        // 3) دفتر العملاء المساعد = حساب المدينين في الأستاذ
        var customerSubLedger = (await new CustomerBalanceQuery(_db).AllBalancesAsync(_companyId))
            .Values.Sum();
        var arGl = await GlBalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsReceivable);
        Assert.True(customerSubLedger == arGl,
            $"[{step}] انفصال دفتر العملاء عن الأستاذ: مساعد {customerSubLedger} ≠ أستاذ {arGl}");

        // 4) دفتر الموردين المساعد = حساب الدائنين في الأستاذ (الدائن موجب)
        var supplierSubLedger = (await new SupplierBalanceQuery(_db).AllBalancesAsync(_companyId))
            .Values.Sum();
        var apGl = -await GlBalanceAsync(SaudiChartOfAccounts.SystemCodes.AccountsPayable);
        Assert.True(supplierSubLedger == apGl,
            $"[{step}] انفصال دفتر الموردين عن الأستاذ: مساعد {supplierSubLedger} ≠ أستاذ {apGl}");
    }

    /// <summary>رصيد حساب في الأستاذ العام (مدين − دائن) من القيود غير المسودة.</summary>
    private async Task<decimal> GlBalanceAsync(string systemCode)
    {
        var accountId = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && a.Code == systemCode)
            .Select(a => a.Id).SingleAsync();

        var lines = await _db.JournalEntries.AsNoTracking()
            .Where(e => e.CompanyId == _companyId && e.Status != JournalEntryStatus.Draft)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountId == accountId)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync();

        return lines.Sum(l => l.Debit - l.Credit);
    }
}
