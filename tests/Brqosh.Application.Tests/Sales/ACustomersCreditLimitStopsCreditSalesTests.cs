using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٩٥ · <c>order-to-cash-procure-to-pay-inventory#13</c>) <b>حدُّ ائتمان العميل يوقف البيعَ الآجلَ الذي يتجاوزه — وكان
/// لا حدَّ في الشجرة كلِّها.</b>
///
/// <para>عميلٌ متعثّرٌ يُباع له آجلاً بلا سقف: لا حقلَ على العميل، ولا سؤالَ عند الترحيل. <b>والحدُّ على المفتوح مع الفاتورة</b> — ما عليه
/// من فواتيرَ ومستخلصاتٍ بعد الإشعارات الدائنة والمقبوض (<c>CustomerBalanceQuery</c>)، زائداً إجماليَّ هذه الفاتورة بضريبتها.</para>
/// </summary>
public class ACustomersCreditLimitStopsCreditSalesTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 2, 1);
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public ACustomersCreditLimitStopsCreditSalesTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ الحاكم: حدٌّ ٢٬٠٠٠ وعليه ١٬١٥٠ — ففاتورةٌ أخرى بـ١٬١٥٠ تُرفض</b>، ولا تُرحَّل ولا يُستهلك لها رقم.
    /// </summary>
    [Fact]
    public async Task AnInvoiceThatWouldExceedTheLimit_IsRefused()
    {
        await SetupAsync(creditLimit: 2_000m);
        await InvoiceAsync();

        await Assert.ThrowsAsync<AccountingException>(InvoiceAsync);

        Assert.Equal(1, await _db.SalesInvoices.CountAsync(i => i.CompanyId == _companyId && i.Status == SalesInvoiceStatus.Posted));
        Assert.Equal(1_150m, (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId)).Balance);
    }

    /// <summary><b>وما بلغ الحدَّ بالتمام يُرحَّل</b> — الحدُّ سقفٌ يُبلَغ لا يُتجاوَز.</summary>
    [Fact]
    public async Task AnInvoiceThatReachesTheLimitExactly_Posts()
    {
        await SetupAsync(creditLimit: 2_300m);
        await InvoiceAsync();

        await InvoiceAsync();

        Assert.Equal(2_300m, (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId)).Balance);
    }

    /// <summary><b>والمقبوضُ يُحرّر الحدّ</b> — فالحدُّ على المفتوح لا على مجموع ما بيع.</summary>
    [Fact]
    public async Task AReceipt_FreesTheLimit()
    {
        await SetupAsync(creditLimit: 2_000m);
        await InvoiceAsync();
        await new ReceiptVoucherPostingService(_db).PostAsync(ReceiptVoucher.CreateDraft(
            _companyId, _customerId, Day, 1_150m, ReceiptMethod.Cash, "تحصيل"));

        await InvoiceAsync();

        Assert.Equal(1_150m, (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId)).Balance);
    }

    /// <summary><b>وعميلٌ بلا حدٍّ كما كان</b> — فكلُّ عميلٍ قائمٍ قبل الترقية بلا حدّ، ولا يتغيّر له شيء.</summary>
    [Fact]
    public async Task ACustomerWithoutALimit_IsUnlimitedAsBefore()
    {
        await SetupAsync(creditLimit: null);

        for (var i = 0; i < 3; i++)
            await InvoiceAsync();

        Assert.Equal(3_450m, (await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId)).Balance);
    }

    /// <summary><b>ولا حدَّ سالباً</b> — وصفرٌ يعني نقداً لا آجلاً.</summary>
    [Fact]
    public void ANegativeLimit_IsRefused()
    {
        var customer = Customer.Create(1, "عميل");

        Assert.Throws<AccountingException>(() => customer.SetCreditLimit(-1m));
        customer.SetCreditLimit(0m);
        Assert.Equal(0m, customer.CreditLimit);
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private async Task InvoiceAsync()
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, Day);
        invoice.AddLine("خدمة", 1m, 1_000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    private async Task SetupAsync(decimal? creditLimit)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("منشأة", "310123456700077", BusinessProfile.General);
        _companyId = company.Id;
        var customer = Customer.Create(_companyId, "عميل آجل");
        customer.SetCreditLimit(creditLimit);
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
    }
}
