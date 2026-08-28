using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Retail;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (CEEF-229 · م0-٩) إشعار مرتجع نقطة البيع لا يُنقص ذمم العميل.
///
/// <para><b>العطل — عدمُ تماثلٍ بين طرفَي المعادلة:</b> فواتير نقاط البيع مستثناة من الذمم
/// (<c>PosShiftId == null</c>) لأنها نقدية ولا تمرّ بحساب العملاء. لكن <b>إشعاراتها</b> لم تكن
/// مستثناة — فيُطرح من الذمم ما لم يُضَف إليها قط. و<c>CreditNotePostingService</c> يوجّه إشعار
/// فاتورة نقطة البيع إلى <b>الصندوق</b> بحقّ، فالبيع كان نقدياً.</para>
///
/// <para><b>الأثر:</b> عميل مسجَّل يشتري من الكاشير نقداً ثم يُرجع البضاعة ⟵ الأستاذ يقول ذمم
/// العميل صفر، والدفتر المساعد يقول <b>سالباً</b>. رصيدٌ دائنٌ وهميّ في كشف الحساب وتقرير
/// الأعمار، ومؤشّر «المدينون» أقلّ من الحقيقة. ويصيب قطاع التجزئة — الشريحة المستهدفة الأولى.</para>
/// </summary>
public class PosCreditNoteReceivableTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly long _companyId;
    private readonly long _customerId;
    private readonly long _productId;
    private readonly long _supplierId;

    public PosCreditNoteReceivableTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";

        var company = new DatabaseInitializer(_db)
            .InitializeAsync("متجر تجزئة", "310123456700003", BusinessProfile.General)
            .GetAwaiter().GetResult();
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميل مسجَّل", "311111111100003");
        _db.Customers.Add(customer);
        var product = Product.Create(_companyId, "P-1", "صنف", "قطعة", ProductType.Stockable, salePrice: 100m);
        _db.Products.Add(product);
        var supplier = Supplier.Create(_companyId, "مورد", "312233344400003");
        _db.Suppliers.Add(supplier);
        _db.SaveChanges();

        _customerId = customer.Id;
        _productId = product.Id;
        _supplierId = supplier.Id;

        StockUpAsync(200m, 40m).GetAwaiter().GetResult();
    }

    private async Task StockUpAsync(decimal qty, decimal cost)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            _companyId, _supplierId, new DateOnly(2026, 1, 2), "SUP-1", true);
        invoice.AddLine("توريد", qty, cost, _productId);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task<long> OpenShiftAsync()
    {
        var shift = PosShift.Open(_companyId, "كاشير", 500m);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();
        return shift.Id;
    }

    private async Task<decimal> CustomerBalanceAsync() =>
        (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId)).Balance;

    /// <summary>
    /// <b>القياس الأساسيّ.</b> بيعة نقطة بيع نقدية لعميل مسجَّل ثم إرجاعها كاملةً ⟵ رصيد
    /// العميل <b>صفر</b>. قبل الإصلاح كان سالباً بقيمة المرتجع.
    /// </summary>
    [Fact]
    public async Task PosSaleThenReturn_LeavesCustomerBalanceAtZero()
    {
        var shiftId = await OpenShiftAsync();

        var sale = await new PosSaleService(_db).SellAsync(
            _companyId, shiftId, _customerId, [(_productId, 10m)],
            isVatRegistered: true, PosPaymentMethod.Cash, "كاشير");

        Assert.Equal(0m, await CustomerBalanceAsync());   // بيع نقديّ لا يُنشئ ذمّة

        var note = CreditNote.CreateDraft(_companyId, _customerId, sale.Invoice.Id,
            new DateOnly(2026, 2, 2), "مرتجع نقطة بيع");
        foreach (var line in sale.Invoice.Lines)
            note.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId, line.VatCategory);
        await new CreditNotePostingService(_db).PostAsync(note);

        Assert.Equal(0m, await CustomerBalanceAsync());   // كان سالباً قبل الإصلاح
        LedgerIdentities.Assert(await LedgerIdentities.ReceivablesAsync(_db, _companyId));
    }

    /// <summary>
    /// <b>حارسٌ للحارس:</b> إشعار على فاتورة <b>آجلة</b> ما زال يُنقص الذمم. لولا هذا لكان
    /// «استثنِ كل الإشعارات» يُمرّر الاختبار السابق ويكسر المسار الصحيح.
    /// </summary>
    [Fact]
    public async Task CreditNoteOnCreditInvoice_StillReducesReceivable()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        invoice.AddLine("صنف", 10m, 100m, _productId);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(1150m, await CustomerBalanceAsync());

        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id,
            new DateOnly(2026, 2, 2), "مرتجع جزئي");
        note.AddLine("صنف", 4m, 100m, _productId);
        await new CreditNotePostingService(_db).PostAsync(note);

        Assert.Equal(690m, await CustomerBalanceAsync());   // 1,150 − 460
        LedgerIdentities.Assert(await LedgerIdentities.ReceivablesAsync(_db, _companyId));
    }

    /// <summary>
    /// عميل له الحالتان معاً: فاتورة آجلة وبيعةُ نقطة بيع مُرجَعة. الرصيد يعكس الآجلة وحدها،
    /// والهوية هـ-١ تُثبت اتّفاق الدفترين.
    /// </summary>
    [Fact]
    public async Task MixedCreditAndPosActivity_ReflectsOnlyTheCreditSide()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 2, 1));
        invoice.AddLine("صنف", 5m, 100m, _productId);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var shiftId = await OpenShiftAsync();
        var sale = await new PosSaleService(_db).SellAsync(
            _companyId, shiftId, _customerId, [(_productId, 3m)],
            isVatRegistered: true, PosPaymentMethod.Cash, "كاشير");

        var note = CreditNote.CreateDraft(_companyId, _customerId, sale.Invoice.Id,
            new DateOnly(2026, 2, 3), "مرتجع نقطة بيع");
        foreach (var line in sale.Invoice.Lines)
            note.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId, line.VatCategory);
        await new CreditNotePostingService(_db).PostAsync(note);

        Assert.Equal(575m, await CustomerBalanceAsync());   // الفاتورة الآجلة وحدها
        LedgerIdentities.Assert(await LedgerIdentities.ReceivablesAsync(_db, _companyId));
    }

    /// <summary>
    /// كشف الحساب يتبع الرصيد: إشعار نقطة البيع لا يظهر فيه. واختلافُ الرصيد الجاري في الكشف
    /// عن الرصيد في رأسه هو أوّل ما يفحصه العميل.
    /// </summary>
    [Fact]
    public async Task Statement_DoesNotListPosCreditNote()
    {
        var shiftId = await OpenShiftAsync();
        var sale = await new PosSaleService(_db).SellAsync(
            _companyId, shiftId, _customerId, [(_productId, 2m)],
            isVatRegistered: true, PosPaymentMethod.Cash, "كاشير");

        var note = CreditNote.CreateDraft(_companyId, _customerId, sale.Invoice.Id,
            new DateOnly(2026, 2, 2), "مرتجع نقطة بيع");
        foreach (var line in sale.Invoice.Lines)
            note.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId, line.VatCategory);
        await new CreditNotePostingService(_db).PostAsync(note);

        var statement = await new CustomerBalanceQuery(_db).StatementAsync(_companyId, _customerId);

        Assert.DoesNotContain(statement, r => r.DocumentType == "إشعار دائن");
        Assert.Equal(0m, statement.Count == 0 ? 0m : statement[^1].RunningBalance);
    }

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
