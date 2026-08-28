using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

public class CreditNoteAndReceiptTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public CreditNoteAndReceiptTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<SalesInvoice> SetupWithInvoiceAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 1));
        invoice.AddLine("بضاعة", 10m, 100m); // 1000 + 150 ضريبة = 1150
        return await new SalesInvoicePostingService(_db).PostAsync(invoice);
    }

    [Fact]
    public async Task CreditNote_GeneratesReversedVatEntry()
    {
        var invoice = await SetupWithInvoiceAsync();

        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 3), "مرتجع بضاعة");
        note.AddLine("مرتجع بضاعة", 2m, 100m); // 200 + 30 = 230

        var posted = await new CreditNotePostingService(_db).PostAsync(note);

        Assert.Equal("CN-2026-000001", posted.NoteNumber);
        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == posted.JournalEntryId);
        Assert.True(entry.IsBalanced);
        Assert.Equal(230m, entry.TotalDebit);
        // مدين مردودات 200 + مدين ضريبة 30 / دائن عملاء 230
        Assert.Equal(200m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.SalesReturns).Debit);
        Assert.Equal(30m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.VatOutput).Debit);
        Assert.Equal(230m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsReceivable).Credit);
    }

    [Fact]
    public async Task CreditNote_ExceedingInvoiceRemaining_IsRejected()
    {
        var invoice = await SetupWithInvoiceAsync(); // 1150

        var first = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 3), "مرتجع");
        first.AddLine("مرتجع", 8m, 100m); // 920
        await new CreditNotePostingService(_db).PostAsync(first);

        var second = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 4), "مرتجع إضافي");
        second.AddLine("مرتجع", 3m, 100m); // 345 > المتبقي 230

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new CreditNotePostingService(_db).PostAsync(second));
        Assert.Contains("تتجاوز", ex.Message);
    }

    [Fact]
    public async Task ReceiptVoucher_Cash_DebitsCashCreditsReceivable()
    {
        await SetupWithInvoiceAsync();

        var voucher = ReceiptVoucher.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5),
            500m, ReceiptMethod.Cash, "دفعة من العميل");
        var posted = await new ReceiptVoucherPostingService(_db).PostAsync(voucher);

        Assert.Equal("RV-2026-000001", posted.VoucherNumber);
        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == posted.JournalEntryId);
        Assert.Equal(500m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CashOnHand).Debit);
        Assert.Equal(500m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsReceivable).Credit);
    }

    [Fact]
    public async Task ReceiptVoucher_Bank_DebitsBankAccount()
    {
        await SetupWithInvoiceAsync();

        var voucher = ReceiptVoucher.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5),
            650m, ReceiptMethod.Bank, "حوالة بنكية", "TRF-990");
        var posted = await new ReceiptVoucherPostingService(_db).PostAsync(voucher);

        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == posted.JournalEntryId);
        Assert.Equal(650m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CashAtBanks).Debit);
    }

    [Fact]
    public async Task CustomerBalance_ReflectsInvoicesCreditsAndReceipts()
    {
        var invoice = await SetupWithInvoiceAsync(); // 1150 مدين

        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 3), "مرتجع");
        note.AddLine("مرتجع", 2m, 100m); // 230 دائن
        await new CreditNotePostingService(_db).PostAsync(note);

        var voucher = ReceiptVoucher.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5),
            500m, ReceiptMethod.Cash, "دفعة"); // 500 دائن
        await new ReceiptVoucherPostingService(_db).PostAsync(voucher);

        var balance = await new CustomerBalanceQuery(_db).ForCustomerAsync(_companyId, _customerId);
        Assert.Equal(1150m, balance.Invoiced);
        Assert.Equal(230m, balance.Credited);
        Assert.Equal(500m, balance.Received);
        Assert.Equal(420m, balance.Balance);

        var statement = await new CustomerBalanceQuery(_db).StatementAsync(_companyId, _customerId);
        Assert.Equal(3, statement.Count);
        Assert.Equal(420m, statement[^1].RunningBalance);
    }

    [Fact]
    public async Task CustomerBalance_IncludesPostedProgressBillings()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة مقاولات", "310123456700003", BusinessProfile.Construction);
        var customer = Customer.Create(company.Id, "عميل مشروع", "311111111100003");
        _db.Customers.Add(customer);
        var project = ConstructionProject.Create(company.Id, "PRJ-BAL", "مشروع رصيد", customer.Id,
            contractValue: 500_000m, retentionPercent: 10m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);
        await _db.SaveChangesAsync();

        var billing = ProgressBilling.CreateDraft(
            company.Id, project.Id, customer.Id, new DateOnly(2026, 7, 4),
            "مستخلص", 100_000m, 10m);
        await new ProgressBillingPostingService(_db).PostAsync(billing);

        var balance = await new CustomerBalanceQuery(_db).ForCustomerAsync(company.Id, customer.Id);
        Assert.Equal(115_000m, balance.Invoiced);
        Assert.Equal(115_000m, balance.Balance);

        var statement = await new CustomerBalanceQuery(_db).StatementAsync(company.Id, customer.Id);
        Assert.Single(statement);
        Assert.Equal("مستخلص أعمال", statement[0].DocumentType);
    }

    [Fact]
    public async Task CreditNote_RequiresPostedInvoiceAndMatchingCustomer()
    {
        var invoice = await SetupWithInvoiceAsync();

        var otherCustomer = Customer.Create(_companyId, "عميل آخر");
        _db.Customers.Add(otherCustomer);
        await _db.SaveChangesAsync();

        var note = CreditNote.CreateDraft(_companyId, otherCustomer.Id, invoice.Id, new DateOnly(2026, 7, 3), "مرتجع");
        note.AddLine("مرتجع", 1m, 50m);

        await Assert.ThrowsAsync<AccountingException>(
            () => new CreditNotePostingService(_db).PostAsync(note));
    }

    [Fact]
    public async Task PostedCreditNote_Cancel_ReversesJournalAndMarksCancelled()
    {
        var invoice = await SetupWithInvoiceAsync();

        var note = CreditNote.CreateDraft(_companyId, _customerId, invoice.Id, new DateOnly(2026, 7, 3), "مرتجع");
        note.AddLine("مرتجع", 1m, 100m);
        var posted = await new CreditNotePostingService(_db).PostAsync(note);
        var originalEntryId = posted.JournalEntryId!.Value;

        await new CreditNoteCancellationService(_db).CancelAsync(
            _companyId, posted.Id, new DateOnly(2026, 7, 4), "admin");

        var reloaded = await _db.CreditNotes.SingleAsync(n => n.Id == posted.Id);
        Assert.Equal(CreditNoteStatus.Cancelled, reloaded.Status);

        var original = await _db.JournalEntries.SingleAsync(e => e.Id == originalEntryId);
        Assert.Equal(JournalEntryStatus.Reversed, original.Status);
        Assert.NotNull(original.ReversalEntryId);
    }
}
