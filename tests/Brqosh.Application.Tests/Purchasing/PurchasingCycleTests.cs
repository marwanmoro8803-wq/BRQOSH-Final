using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Purchasing;

public class PurchasingCycleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _supplierId;

    public PurchasingCycleTests()
    {
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

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;

        var supplier = Supplier.Create(_companyId, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        _supplierId = supplier.Id;
    }

    [Fact]
    public async Task PurchaseInvoice_Deductible_SplitsVatToInputAccount()
    {
        await SetupAsync();

        var invoice = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 2), "SUP-771", vatDeductible: true);
        invoice.AddLine("بضاعة", 5m, 400m); // 2000 + 300 = 2300

        var posted = await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal("PI-2026-000001", posted.InvoiceNumber);
        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == posted.JournalEntryId);
        Assert.True(entry.IsBalanced);
        Assert.Equal(2000m, entry.Lines.Single(l => l.AccountCode == "5102").Debit);
        Assert.Equal(300m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.VatInput).Debit);
        Assert.Equal(2300m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsPayable).Credit);
    }

    [Fact]
    public async Task PurchaseInvoice_NonDeductible_CapitalizesVatInCost()
    {
        await SetupAsync();

        var invoice = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 2), "SUP-772", vatDeductible: false);
        invoice.AddLine("بضاعة", 1m, 1000m); // 1000 + 150

        var posted = await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == posted.JournalEntryId);
        Assert.Equal(1150m, entry.Lines.Single(l => l.AccountCode == "5102").Debit);
        Assert.DoesNotContain(entry.Lines, l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.VatInput);
    }

    [Fact]
    public async Task DuplicateSupplierReference_IsRejected()
    {
        await SetupAsync();

        var first = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 2), "SUP-100", true);
        first.AddLine("بضاعة", 1m, 100m);
        await new PurchaseInvoicePostingService(_db).PostAsync(first);

        var second = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 3), "SUP-100", true);
        second.AddLine("بضاعة", 1m, 200m);

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new PurchaseInvoicePostingService(_db).PostAsync(second));
        Assert.Contains("مسجلة مسبقاً", ex.Message);
    }

    [Fact]
    public async Task PurchaseInvoice_Cancel_ReversesJournalAndMarksCancelled()
    {
        await SetupAsync();

        var invoice = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 2), "SUP-CAN", true);
        invoice.AddLine("بضاعة", 1m, 500m);
        var posted = await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        await new PurchaseInvoiceCancellationService(_db).CancelAsync(
            _companyId, posted.Id, new DateOnly(2026, 7, 15), "admin");

        var reloaded = await _db.PurchaseInvoices.SingleAsync(i => i.Id == posted.Id);
        Assert.Equal(PurchaseInvoiceStatus.Cancelled, reloaded.Status);

        var entries = await _db.JournalEntries.Where(e => e.SourceDocument == $"PurchaseInvoice:{posted.InvoiceNumber}").ToListAsync();
        Assert.Equal(2, entries.Count); // original + reversal
    }

    [Fact]
    public async Task PaymentVoucher_DebitsPayableCreditsBank()
    {
        await SetupAsync();

        var invoice = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 2), "SUP-200", true);
        invoice.AddLine("بضاعة", 1m, 1000m);
        await new PurchaseInvoicePostingService(_db).PostAsync(invoice);

        var voucher = PaymentVoucher.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 10),
            700m, ReceiptMethod.Bank, "سداد جزئي", "TRF-55");
        var posted = await new PaymentVoucherPostingService(_db).PostAsync(voucher);

        Assert.Equal("PV-2026-000001", posted.VoucherNumber);
        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == posted.JournalEntryId);
        Assert.Equal(700m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsPayable).Debit);
        Assert.Equal(700m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CashAtBanks).Credit);

        var balances = await new SupplierBalanceQuery(_db).AllBalancesAsync(_companyId);
        Assert.Equal(450m, balances[_supplierId]); // 1150 - 700
    }

    [Fact]
    public async Task VatReturn_ComputesNetPayable()
    {
        await SetupAsync();

        // مبيعات: 1000 + 150 مخرجات
        var customer = Customer.Create(_companyId, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        var sale = SalesInvoice.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 7, 5));
        sale.AddLine("بضاعة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(sale);

        // إشعار دائن: 200 + 30 تخفيض مخرجات
        var note = CreditNote.CreateDraft(_companyId, customer.Id, sale.Id, new DateOnly(2026, 7, 10), "مرتجع");
        note.AddLine("مرتجع", 1m, 200m);
        await new CreditNotePostingService(_db).PostAsync(note);

        // مشتريات قابلة للخصم: 400 + 60 مدخلات
        var purchase = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 8), "SUP-300", true);
        purchase.AddLine("بضاعة", 1m, 400m);
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);

        var result = await new VatReturnQuery(_db).ExecuteAsync(_companyId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        Assert.Equal(150m, result.OutputVat);
        Assert.Equal(30m, result.CreditNotesVat);
        Assert.Equal(120m, result.NetOutputVat);
        Assert.Equal(60m, result.DeductibleInputVat);
        Assert.Equal(60m, result.NetVatPayable); // 120 - 60
    }

    [Fact]
    public async Task VatReturn_DebitNote_ReducesDeductibleInputVat()
    {
        await SetupAsync();

        var purchase = PurchaseInvoice.CreateDraft(_companyId, _supplierId, new DateOnly(2026, 7, 8), "SUP-301", true);
        purchase.AddLine("بضاعة", 1m, 1000m);
        var postedPurchase = await new PurchaseInvoicePostingService(_db).PostAsync(purchase);

        var debitNote = DebitNote.CreateDraft(
            _companyId, _supplierId, postedPurchase.Id, new DateOnly(2026, 7, 12), "خصم");
        debitNote.AddLine("خصم", 1m, 200m);
        await new DebitNotePostingService(_db).PostAsync(debitNote);

        var result = await new VatReturnQuery(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        Assert.Equal(150m, result.DeductibleInputVat);
        Assert.Equal(30m, result.DebitNotesVat);
        Assert.Equal(30m, result.DebitNotesDeductibleVat);
        Assert.Equal(120m, result.NetDeductibleInputVat);
        Assert.Equal(-120m, result.NetVatPayable);
    }
}
