using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>
/// (CEEF-206) الدفع المختلط في نقاط البيع.
///
/// النموذج لا يحمل أي حقل يفصل حصة النقد عن حصة الشبكة، وكان الترحيل يقيّد المبلغ
/// كاملاً على الصندوق — فتخالف عهدة الصندوق الدفترية الفعلية وتفشل مطابقة الوردية،
/// ولا تظهر حصة الشبكة في البنك. ترحيل قيد نعلم أنه خاطئ أسوأ من رفضه.
/// </summary>
public class PosMixedPaymentGuardTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public PosMixedPaymentGuardTests()
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

    private async Task<PosShift> SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة التجزئة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل نقدي", "311122233300003");
        _db.Customers.Add(customer);
        var shift = PosShift.Open(_companyId, "كاشير", 1000m);
        _db.PosShifts.Add(shift);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
        return shift;
    }

    private SalesInvoice PosInvoice(long shiftId, PosPaymentMethod method, decimal? cardAmount = null)
    {
        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("بيع تجزئة", 1m, 200m);
        invoice.MarkPosSale(shiftId, method, cardAmount);
        return invoice;
    }

    /// <summary>المختلط بلا مبلغ بطاقة يُرفض برسالة واضحة بدل قيد خاطئ صامت.</summary>
    [Fact]
    public async Task MixedPayment_WithoutCardAmount_IsRejected()
    {
        var shift = await SetupAsync();

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new SalesInvoicePostingService(_db).PostAsync(PosInvoice(shift.Id, PosPaymentMethod.Mixed)));

        Assert.Contains("المختلط", ex.Message);
        Assert.Empty(await _db.JournalEntries.AsNoTracking().ToListAsync());   // لا قيد خاطئ
    }

    /// <summary>المختلط يقسم المدين: حصّة البطاقة للبنك والباقي للصندوق.</summary>
    [Fact]
    public async Task MixedPayment_SplitsCashAndCard()
    {
        var shift = await SetupAsync();

        var invoice = PosInvoice(shift.Id, PosPaymentMethod.Mixed, cardAmount: 80m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var entry = await _db.JournalEntries.AsNoTracking()
            .Include(e => e.Lines)
            .SingleAsync(e => e.Id == invoice.JournalEntryId);

        Assert.Equal(entry.TotalDebit, entry.TotalCredit);
        Assert.Equal(80m, entry.Lines.Where(l => l.AccountCode.StartsWith("1102")).Sum(l => l.Debit));
        Assert.Equal(invoice.GrandTotal() - 80m, entry.Lines.Where(l => l.AccountCode.StartsWith("1101")).Sum(l => l.Debit));
    }

    /// <summary>عهدة الوردية تحسب حصّة النقد وحدها من المختلط.</summary>
    [Fact]
    public void Shift_CountsOnlyCashShareOfMixed()
    {
        var shift = PosShift.Open(1, "كاشير", 100m);
        shift.RecordSale(230m, PosPaymentMethod.Mixed, 80m);
        Assert.Equal(250m, shift.ExpectedCash);
        Assert.Equal(230m, shift.TotalSales);
    }

    /// <summary>النقد والبطاقة يبقيان يعملان — الحارس لا يمنع المسارين الصحيحين.</summary>
    [Theory]
    [InlineData(PosPaymentMethod.Cash, "1101")]
    [InlineData(PosPaymentMethod.Card, "1102")]
    public async Task CashAndCard_PostToTheirOwnAccount(PosPaymentMethod method, string expectedAccountPrefix)
    {
        var shift = await SetupAsync();

        var invoice = PosInvoice(shift.Id, method);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var entry = await _db.JournalEntries.AsNoTracking()
            .Include(e => e.Lines)
            .SingleAsync(e => e.Id == invoice.JournalEntryId);

        Assert.Equal(entry.TotalDebit, entry.TotalCredit);
        Assert.Contains(entry.Lines, l => l.Debit > 0 && l.AccountCode.StartsWith(expectedAccountPrefix));
    }
}
