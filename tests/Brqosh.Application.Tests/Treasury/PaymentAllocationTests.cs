using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

public class PaymentAllocationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public PaymentAllocationTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ReceiptAllocation_ReducesOpenBalance_AndSumMustMatch()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700005", BusinessProfile.Retail);
        var customer = Customer.Create(company.Id, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var inv1 = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, 1));
        inv1.AddLine("بضاعة", 10m, 100m); // 1150
        await new SalesInvoicePostingService(_db).PostAsync(inv1);

        var inv2 = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, 2));
        inv2.AddLine("بضاعة", 5m, 100m); // 575
        await new SalesInvoicePostingService(_db).PostAsync(inv2);

        var alloc = new PaymentAllocationService(_db);
        var open = await alloc.OpenSalesDocumentsAsync(company.Id, customer.Id);
        Assert.Equal(2, open.Count);
        Assert.Equal(1150m, open.Single(o => o.DocumentId == inv1.Id).RemainingAmount);

        var voucher = ReceiptVoucher.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, 5),
            1150m, ReceiptMethod.Cash, "سداد فاتورة 1");

        var bad = await Assert.ThrowsAsync<Brqosh.Domain.Accounting.AccountingException>(() =>
            new ReceiptVoucherPostingService(_db).PostAsync(voucher, [
                (PaymentAllocationDocumentType.SalesInvoice, inv1.Id, inv1.InvoiceNumber!, 500m),
            ]));
        Assert.Contains("يساوي", bad.Message);

        var voucher2 = ReceiptVoucher.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, 5),
            1150m, ReceiptMethod.Cash, "سداد فاتورة 1");
        await new ReceiptVoucherPostingService(_db).PostAsync(voucher2, [
            (PaymentAllocationDocumentType.SalesInvoice, inv1.Id, inv1.InvoiceNumber!, 1150m),
        ]);

        open = await alloc.OpenSalesDocumentsAsync(company.Id, customer.Id);
        Assert.Single(open);
        Assert.Equal(inv2.Id, open[0].DocumentId);
        Assert.Equal(575m, open[0].RemainingAmount);
    }

    [Fact]
    public async Task PaymentAllocation_AgainstPurchaseInvoice()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700006", BusinessProfile.Retail);
        var supplier = Supplier.Create(company.Id, "مورد", "312233344400003");
        var product = Product.Create(company.Id, "P1", "صنف", "قطعة", ProductType.Stockable);
        _db.Suppliers.Add(supplier);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var pi = PurchaseInvoice.CreateDraft(company.Id, supplier.Id, new DateOnly(2026, 3, 1), "PI-1", true);
        pi.AddLine("صنف", 10m, 50m, product.Id); // 500 + 75 = 575
        await new PurchaseInvoicePostingService(_db).PostAsync(pi);

        var open = await new PaymentAllocationService(_db).OpenPurchaseDocumentsAsync(company.Id, supplier.Id);
        Assert.Single(open);
        Assert.Equal(575m, open[0].RemainingAmount);

        var voucher = PaymentVoucher.CreateDraft(company.Id, supplier.Id, new DateOnly(2026, 3, 5),
            575m, ReceiptMethod.Cash, "سداد");
        await new PaymentVoucherPostingService(_db).PostAsync(voucher, [
            (PaymentAllocationDocumentType.PurchaseInvoice, pi.Id, pi.InvoiceNumber!, 575m),
        ]);

        open = await new PaymentAllocationService(_db).OpenPurchaseDocumentsAsync(company.Id, supplier.Id);
        Assert.Empty(open);
    }
    /// <summary>
    /// (CEEF-229 · م1-١٦ / A-13) <b>سقفُ التخصيص — الحارسُ القائم الذي لم يقسه اختبارٌ واحد.</b>
    ///
    /// <para>الحارسُ موجودٌ منذ CEEF-206 (‏<c>يتجاوز المتبقي</c>)، <b>ولا اختبارَ في المستودع
    /// يمرّ به</b>: الاختبارُ الوحيد للتخصيص يقيس رسالةَ «المجموع يساوي» وحدها. فحارسٌ لا
    /// يُقاس حارسٌ لا يُعلَم أحيٌّ هو.</para>
    /// </summary>
    [Fact]
    public async Task AllocatingMoreThanTheRemainder_IsRefused()
    {
        var (companyId, customerId, invoiceId, number) = await SeedInvoiceAsync();

        var first = ReceiptVoucher.CreateDraft(companyId, customerId, new DateOnly(2026, 2, 5),
            1_000m, ReceiptMethod.Cash, "دفعة أولى");
        await new ReceiptVoucherPostingService(_db).PostAsync(first, [
            (PaymentAllocationDocumentType.SalesInvoice, invoiceId, number, 1_000m),
        ]);

        // ‏المتبقي 150 — ومحاولةُ تخصيص 500 تتجاوزه.
        var second = ReceiptVoucher.CreateDraft(companyId, customerId, new DateOnly(2026, 2, 6),
            500m, ReceiptMethod.Cash, "دفعة ثانية");

        var ex = await Assert.ThrowsAsync<Brqosh.Domain.Accounting.AccountingException>(() =>
            new ReceiptVoucherPostingService(_db).PostAsync(second, [
                (PaymentAllocationDocumentType.SalesInvoice, invoiceId, number, 500m),
            ]));

        Assert.Contains("يتجاوز المتبقي", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وسطران على المستند نفسه داخل سندٍ واحد — وهذا ما كان يمرّ.</b>
    ///
    /// <para>الحلقةُ تقرأ المتبقي <b>من القاعدة</b> لكلّ سطر، والسطورُ المضافةُ قبله في
    /// الدفعة نفسها لم تُحفَظ بعد — فلا يراها الاستعلام. فيقرأ السطران المتبقيَ نفسَه
    /// ويمرّان معاً، <b>ويُسدَّد المستندُ بأكثر من قيمته</b>.</para>
    ///
    /// <para>وحارسُ «المجموع يساوي مبلغ السند» لا يمنعه: مبلغُ السند 2,000 ومجموعُ سطوره
    /// 2,000 — <b>والفاتورةُ 1,150</b>.</para>
    /// </summary>
    [Fact]
    public async Task TwoLinesOnTheSameDocument_InOneVoucher_AreRefused()
    {
        var (companyId, customerId, invoiceId, number) = await SeedInvoiceAsync();

        var voucher = ReceiptVoucher.CreateDraft(companyId, customerId, new DateOnly(2026, 2, 5),
            2_000m, ReceiptMethod.Cash, "سطران على فاتورة واحدة");

        var ex = await Assert.ThrowsAsync<Brqosh.Domain.Accounting.AccountingException>(() =>
            new ReceiptVoucherPostingService(_db).PostAsync(voucher, [
                (PaymentAllocationDocumentType.SalesInvoice, invoiceId, number, 1_000m),
                (PaymentAllocationDocumentType.SalesInvoice, invoiceId, number, 1_000m),
            ]));

        Assert.Contains("يتجاوز المتبقي", ex.Message, StringComparison.Ordinal);

        // ‏ولا يبقى أثرٌ جزئيّ: الفاتورةُ كما كانت.
        var open = await new PaymentAllocationService(_db).OpenSalesDocumentsAsync(companyId, customerId);
        Assert.Equal(1_150m, open.Single(o => o.DocumentId == invoiceId).RemainingAmount);
    }

    private async Task<(long CompanyId, long CustomerId, long InvoiceId, string Number)> SeedInvoiceAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة التخصيص", "310123456700005", BusinessProfile.Retail);
        var customer = Customer.Create(company.Id, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, 1));
        invoice.AddLine("بضاعة", 10m, 100m); // 1150 شاملة الضريبة
        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        return (company.Id, customer.Id, posted.Id, posted.InvoiceNumber!);
    }

}
