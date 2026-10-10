using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١١٥ · ‏`order-to-cash-procure-to-pay-inventory#8`) ‏**عرضُ السعر يُقبل ويُرفض ويُحوَّل —
/// ‏وأمرُ البيع متعدّدُ الأسطر بالصنف والفئة الضريبيّة.**
///
/// ‏<para>كان <c>Accept()</c> بلا حارس ولا <c>Reject()</c>، فلا مسارَ يُسجّل جواب العميل؛ وكان الأمرُ والعرضُ
/// في الواجهة سطراً نصّيّاً واحداً بلا صنفٍ ولا فئة. هذه الاختباراتُ تقيس الانتقالَ في الخدمة (مع الحفظ)،
/// ‏وأنّ المرفوضَ لا يُحوَّل، وأنّ الأمرَ المتعدّدَ الأسطر يُؤكَّد ويُفوتَر بضريبةٍ صحيحة.</para>
/// </summary>
public class AQuotationIsAcceptedRejectedAndConvertedTests : IDisposable
{
    private const string ExemptCode = "VATEX-SA-29";

    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;
    private long _productA;
    private long _productB;

    public AQuotationIsAcceptedRejectedAndConvertedTests()
    {
        TestActor.Accountant();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose() => _db.Dispose();

    private async Task SeedAsync()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة", "300000000000001", Brqosh.Domain.Modules.BusinessProfile.Manufacturing);
        _companyId = company.Id;
        var customer = Customer.Create(company.Id, "عميل", "300000000000002");
        var a = Product.Create(company.Id, "SRV-A", "خدمة أ", "جلسة", ProductType.Service, salePrice: 1000m);
        var b = Product.Create(company.Id, "SRV-B", "خدمة ب", "جلسة", ProductType.Service, salePrice: 500m);
        _db.Customers.Add(customer);
        _db.Products.AddRange(a, b);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        _productA = a.Id;
        _productB = b.Id;
    }

    private async Task<SalesQuotation> SentQuotationAsync()
    {
        var quote = SalesQuotation.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 1));
        quote.AddLine("خدمة أ", 1m, 1000m, _productA);
        quote.AddLine("خدمة معفاة", 1m, 200m, _productB, VatCategory.Exempt, ExemptCode);
        await new SalesQuotationService(_db).CreateAndSendAsync(quote);
        return quote;
    }

    /// <summary>القبولُ ثمّ التحويل ينتجان أمراً يحمل أسطرَ العرض بأصنافها وفئاتها الضريبيّة.</summary>
    [Fact]
    public async Task Accepted_quotation_converts_to_an_order_carrying_its_product_lines_and_vat_categories()
    {
        await SeedAsync();
        var quote = await SentQuotationAsync();

        await new SalesQuotationService(_db).AcceptAsync(_companyId, quote.Id);
        Assert.Equal(SalesQuotationStatus.Accepted,
            (await _db.SalesQuotations.AsNoTracking().SingleAsync(q => q.Id == quote.Id)).Status);

        var order = await new SalesOrderService(_db).CreateFromQuotationAsync(_companyId, quote.Id);
        var stored = await _db.SalesOrders.Include(o => o.Lines).SingleAsync(o => o.Id == order.Id);

        Assert.Equal(2, stored.Lines.Count);
        Assert.Equal([_productA, _productB], stored.Lines.Select(l => l.ProductId!.Value).ToArray());
        Assert.Equal([VatCategory.Standard, VatCategory.Exempt], stored.Lines.Select(l => l.VatCategory).ToArray());
        Assert.Equal(150m, stored.VatAmount);
    }

    /// <summary>المرفوضُ لا يُحوَّل إلى أمر — ولا يُنشأ أمرٌ بعد الرفض.</summary>
    [Fact]
    public async Task Rejected_quotation_cannot_be_converted()
    {
        await SeedAsync();
        var quote = await SentQuotationAsync();

        await new SalesQuotationService(_db).RejectAsync(_companyId, quote.Id);
        Assert.Equal(SalesQuotationStatus.Rejected,
            (await _db.SalesQuotations.AsNoTracking().SingleAsync(q => q.Id == quote.Id)).Status);

        await Assert.ThrowsAsync<AccountingException>(
            () => new SalesOrderService(_db).CreateFromQuotationAsync(_companyId, quote.Id));
        Assert.Empty(await _db.SalesOrders.ToListAsync());
    }

    /// <summary>العرضُ يُحوَّل مرّةً: التحويلُ الثاني يُرفض ولا يُنشئ أمراً ثانياً.</summary>
    [Fact]
    public async Task A_quotation_converts_to_one_order_only()
    {
        await SeedAsync();
        var quote = await SentQuotationAsync();
        await new SalesQuotationService(_db).AcceptAsync(_companyId, quote.Id);
        await new SalesOrderService(_db).CreateFromQuotationAsync(_companyId, quote.Id);

        await Assert.ThrowsAsync<AccountingException>(
            () => new SalesOrderService(_db).CreateFromQuotationAsync(_companyId, quote.Id));
        Assert.Single(await _db.SalesOrders.ToListAsync());
    }

    /// <summary>المرفوضُ لا يُقبل، والمقبولُ لا يُرفض.</summary>
    [Fact]
    public async Task A_rejected_quotation_cannot_be_accepted_nor_an_accepted_one_rejected()
    {
        await SeedAsync();
        var rejected = await SentQuotationAsync();
        await new SalesQuotationService(_db).RejectAsync(_companyId, rejected.Id);
        await Assert.ThrowsAsync<AccountingException>(
            () => new SalesQuotationService(_db).AcceptAsync(_companyId, rejected.Id));

        var accepted = await SentQuotationAsync();
        await new SalesQuotationService(_db).AcceptAsync(_companyId, accepted.Id);
        await Assert.ThrowsAsync<AccountingException>(
            () => new SalesQuotationService(_db).RejectAsync(_companyId, accepted.Id));
    }

    /// <summary>أمرُ بيعٍ بصنفَين وسطرٍ معفىً يُؤكَّد ويُفوتَر بضريبةٍ على الخاضع وحده.</summary>
    [Fact]
    public async Task A_multi_line_order_with_two_products_and_an_exempt_line_confirms_and_invoices_with_the_right_vat()
    {
        await SeedAsync();
        var order = SalesOrder.CreateDraft(_companyId, _customerId, new DateOnly(2026, 3, 1));
        order.AddLine("خدمة أ", 2m, 500m, _productA);                 // 1000 → 150
        order.AddLine("خدمة ب", 1m, 500m, _productB);                 //  500 →  75
        order.AddLine("خدمة معفاة", 1m, 200m, null, VatCategory.Exempt, ExemptCode);
        var service = new SalesOrderService(_db);
        await service.ConfirmAsync(order);

        var stored = await _db.SalesOrders.Include(o => o.Lines).SingleAsync(o => o.Id == order.Id);
        Assert.Equal(SalesOrderStatus.Confirmed, stored.Status);
        Assert.Equal(3, stored.Lines.Count);
        Assert.Equal(225m, stored.VatAmount);

        var invoice = await service.CreateInvoiceFromOrderAsync(_companyId, order.Id, new DateOnly(2026, 3, 4));
        var inv = await _db.SalesInvoices.Include(i => i.Lines).SingleAsync(i => i.Id == invoice.Id);
        Assert.Equal(1700m, inv.Subtotal);
        Assert.Equal(225m, inv.VatAmount);
        Assert.Equal([VatCategory.Standard, VatCategory.Standard, VatCategory.Exempt],
            inv.Lines.Select(l => l.VatCategory).ToArray());
    }
}
