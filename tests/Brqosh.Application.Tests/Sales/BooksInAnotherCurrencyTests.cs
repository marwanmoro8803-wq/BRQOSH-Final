using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (دفاترٌ بغير الريال) <b>منشأةٌ دفاترُها بالدولار تُصدر وتُرحِّل بالدولار فعلاً</b> — لا يُوسَم مستندُها «ريالاً» ولا يُعدّ أجنبيّاً، والمستندُ
/// بالريال عندها هو الأجنبيُّ بسعر صرفه. كان المحرّكُ يفترض الريالَ في وسم المستند وفي سؤال «أجنبيّ؟» فتُعامَل دولاراتُها كأنّها عملةٌ أجنبيّة.
/// </summary>
public class BooksInAnotherCurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public BooksInAnotherCurrencyTests()
    {
        TestActor.Administrator();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).AddInterceptors(new BooksCurrencyInterceptor()).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync(string booksCurrency)
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        if (booksCurrency != "SAR")
            await new CompanyCurrencyChangeService(_db).ChangeAsync(_companyId, booksCurrency, booksCurrency);

        var customer = Customer.Create(_companyId, "عميل", "311111111100003").WithStandardIdentity();
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
    }

    [Fact]
    public async Task ADollarBooksInvoice_IsPostedInDollars_AndIsNotForeign()
    {
        await SetupAsync("USD");

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5));
        invoice.AddLine("خدمة", 2m, 500m);
        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var saved = await _db.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == posted.Id);
        Assert.Equal("USD", saved.CurrencyCode);
        Assert.False(saved.IsForeignCurrency);
        Assert.Equal(1000m, saved.Subtotal);
        Assert.Equal(1000m, saved.ForeignSubtotal);

        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == saved.JournalEntryId);
        Assert.True(entry.IsBalanced);
    }

    [Fact]
    public async Task ASaudiRiyalInvoice_InDollarBooks_IsForeign_AndConvertsByItsRate()
    {
        await SetupAsync("USD");

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5));
        invoice.SetCurrency("SAR", 0.25m, booksCurrencyCode: "USD");
        invoice.AddLine("خدمة", 1m, 4000m);                 // 4000 ريال = 1000 دولار
        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var saved = await _db.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == posted.Id);
        Assert.Equal("SAR", saved.CurrencyCode);
        Assert.True(saved.IsForeignCurrency);
        Assert.Equal(4000m, saved.ForeignSubtotal);
        Assert.Equal(1000m, saved.Subtotal);
    }

    [Fact]
    public async Task TheSaudiBooks_StayExactlyAsTheyWere()
    {
        await SetupAsync("SAR");

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5));
        invoice.AddLine("خدمة", 1m, 100m);
        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var saved = await _db.SalesInvoices.AsNoTracking().SingleAsync(i => i.Id == posted.Id);
        Assert.Equal("SAR", saved.CurrencyCode);
        Assert.False(saved.IsForeignCurrency);
    }

    [Theory]
    [InlineData("USD", 1, "SAR")]   // عملتان مختلفتان بسعر ١ — يضيع الإفصاح
    [InlineData("SAR", 0.25, "SAR")] // عملةُ الدفاتر بسعرٍ غير ١
    public void ADocumentCurrencyAndItsRate_MustAgreeWithTheBooks(string code, double rate, string books)
    {
        var invoice = SalesInvoice.CreateDraft(1, 1, new DateOnly(2026, 7, 5));
        Assert.Throws<AccountingException>(() => invoice.SetCurrency(code, (decimal)rate, books));
    }

    [Fact]
    public void ADollarDocumentInRiyalBooks_StillNeedsItsRate()
    {
        var invoice = SalesInvoice.CreateDraft(1, 1, new DateOnly(2026, 7, 5));
        invoice.SetCurrency("USD", 3.75m, "SAR");
        Assert.True(invoice.IsForeignCurrency);
    }
}
