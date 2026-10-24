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

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · الأداء) <b>المفتوحُ بالجملة = المفتوحُ عميلاً عميلاً، بالأرقام نفسِها.</b>
///
/// <para>توقّعُ النقد كان يسأل القاعدةَ عن كلّ فاتورةٍ على حدة (٧٬٢٠٠ استعلام على دفاترِ خمس سنوات) فصار بأربعة استعلاماتٍ بالجملة. وتغيير
/// طريقة الحساب في كودٍ مالٍّ يُقاس بمطابقته للطريقة القديمة على حالاتٍ فيها كلُّ ما يغيّر المتبقّي: تخصيصٌ جزئيّ، وإشعارُ دائنٍ مرحَّل،
/// وفاتورةٌ بلا شيء، وشراءٌ بسدادٍ جزئيّ.</para>
/// </summary>
public class OpenDocumentsInBulkMatchTheOneByOneTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public OpenDocumentsInBulkMatchTheOneByOneTests()
    {
        TestActor.Accountant();
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
    public async Task Sales_BulkEqualsPerCustomer_WithPartialPaymentAndACreditNote()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة", "310123456700005", BusinessProfile.Retail);
        var service = Product.Create(company.Id, "S1", "خدمة", "خدمة", ProductType.Service, 100m);
        var customers = new[]
        {
            Customer.Create(company.Id, "أ", "311122233300003"),
            Customer.Create(company.Id, "ب", "311122233400003"),
            Customer.Create(company.Id, "ج", "311122233500003"),
        };
        _db.Products.Add(service);
        _db.Customers.AddRange(customers);
        await _db.SaveChangesAsync();

        async Task<SalesInvoice> InvoiceAsync(Customer customer, int day, decimal qty)
        {
            var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 2, day));
            invoice.AddLine("خدمة", qty, 100m, service.Id);
            await new SalesInvoicePostingService(_db).PostAsync(invoice);
            return invoice;
        }

        var a1 = await InvoiceAsync(customers[0], 1, 10m);     // 1150
        await InvoiceAsync(customers[0], 2, 4m);               // 460 — بلا سداد
        var b1 = await InvoiceAsync(customers[1], 3, 10m);     // 1150
        await InvoiceAsync(customers[2], 4, 2m);               // 230

        // ‏سدادٌ جزئيٌّ لفاتورة أ.
        await new ReceiptVoucherPostingService(_db).PostAsync(
            ReceiptVoucher.CreateDraft(company.Id, customers[0].Id, new DateOnly(2026, 2, 6), 400m, ReceiptMethod.Cash, "جزئي"),
            [(PaymentAllocationDocumentType.SalesInvoice, a1.Id, a1.InvoiceNumber!, 400m)]);

        // ‏إشعارُ دائنٍ مرحَّلٌ على فاتورة ب (٣ وحدات = ٣٤٥).
        var note = CreditNote.CreateDraft(company.Id, customers[1].Id, b1.Id, new DateOnly(2026, 2, 7), "مرتجع");
        note.AddLine("خدمة", 3m, 100m, service.Id);
        await new CreditNotePostingService(_db).PostAsync(note);

        var allocation = new PaymentAllocationService(_db);
        var bulk = await allocation.OpenSalesDocumentsForAllAsync(company.Id);

        foreach (var customer in customers)
        {
            var one = await allocation.OpenSalesDocumentsAsync(company.Id, customer.Id);
            var many = bulk.Where(b => b.CustomerId == customer.Id).Select(b => b.Document)
                .OrderBy(d => d.DocumentDate).ThenBy(d => d.DocumentNumber).ToList();

            Assert.Equal(one.Select(d => (d.DocumentId, d.RemainingAmount, d.OriginalAmount, d.AllocatedAmount)),
                many.Select(d => (d.DocumentId, d.RemainingAmount, d.OriginalAmount, d.AllocatedAmount)));
        }

        // ‏وأرقامٌ مرجعيّةٌ بالعدّ اليدويّ: أ: ٧٥٠ + ٤٦٠ · ب: ١١٥٠ − ٣٤٥ = ٨٠٥ · ج: ٢٣٠.
        decimal Remaining(Customer c) => bulk.Where(b => b.CustomerId == c.Id).Sum(b => b.Document.RemainingAmount);
        Assert.Equal(750m + 460m, Remaining(customers[0]));
        Assert.Equal(805m, Remaining(customers[1]));
        Assert.Equal(230m, Remaining(customers[2]));
    }

    [Fact]
    public async Task Purchases_BulkEqualsPerSupplier_WithPartialPayment()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة", "310123456700006", BusinessProfile.Retail);
        var suppliers = new[]
        {
            Supplier.Create(company.Id, "مورد 1", "312233344400003"),
            Supplier.Create(company.Id, "مورد 2", "312233344500003"),
        };
        var product = Product.Create(company.Id, "P1", "صنف", "قطعة", ProductType.Stockable);
        _db.Suppliers.AddRange(suppliers);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var p1 = PurchaseInvoice.CreateDraft(company.Id, suppliers[0].Id, new DateOnly(2026, 3, 1), "PI-1", true);
        p1.AddLine("صنف", 10m, 50m, product.Id);               // 575
        await new PurchaseInvoicePostingService(_db).PostAsync(p1);
        var p2 = PurchaseInvoice.CreateDraft(company.Id, suppliers[1].Id, new DateOnly(2026, 3, 2), "PI-2", true);
        p2.AddLine("صنف", 4m, 50m, product.Id);                // 230
        await new PurchaseInvoicePostingService(_db).PostAsync(p2);

        await new PaymentVoucherPostingService(_db).PostAsync(
            PaymentVoucher.CreateDraft(company.Id, suppliers[0].Id, new DateOnly(2026, 3, 5), 200m, ReceiptMethod.Cash, "جزئي"),
            [(PaymentAllocationDocumentType.PurchaseInvoice, p1.Id, p1.InvoiceNumber!, 200m)]);

        var allocation = new PaymentAllocationService(_db);
        var bulk = await allocation.OpenPurchaseDocumentsForAllAsync(company.Id);

        foreach (var supplier in suppliers)
        {
            var one = await allocation.OpenPurchaseDocumentsAsync(company.Id, supplier.Id);
            var many = bulk.Where(b => b.SupplierId == supplier.Id).Select(b => b.Document).ToList();
            Assert.Equal(one.Select(d => (d.DocumentId, d.RemainingAmount)), many.Select(d => (d.DocumentId, d.RemainingAmount)));
        }

        Assert.Equal(375m, bulk.Single(b => b.SupplierId == suppliers[0].Id).Document.RemainingAmount);
        Assert.Equal(230m, bulk.Single(b => b.SupplierId == suppliers[1].Id).Document.RemainingAmount);
    }
}
