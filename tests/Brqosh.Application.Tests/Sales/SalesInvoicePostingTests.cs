using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

public class SalesInvoicePostingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public SalesInvoicePostingTests()
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

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;

        var customer = Customer.Create(_companyId, "عميل تجريبي", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;
    }

    [Fact]
    public async Task PostInvoice_GeneratesBalancedJournalEntry_WithVat15()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5));
        invoice.AddLine("خدمة استشارية", 2m, 500m);   // 1000
        invoice.AddLine("توريد مواد", 1m, 200m);       // 200

        Assert.Equal(1200m, invoice.Subtotal);
        Assert.Equal(180m, invoice.VatAmount);         // 15%
        Assert.Equal(1380m, invoice.GrandTotal());

        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal("INV-2026-000001", posted.InvoiceNumber);
        Assert.Equal(SalesInvoiceStatus.Posted, posted.Status);
        Assert.NotNull(posted.JournalEntryId);

        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == posted.JournalEntryId);
        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        Assert.True(entry.IsBalanced);
        Assert.Equal(1380m, entry.TotalDebit);

        // المدين على حساب العملاء، والدائن موزع: 1200 إيراد + 180 ضريبة مخرجات
        var receivableLine = entry.Lines.Single(l => l.Debit > 0);
        Assert.Equal(SaudiChartOfAccounts.SystemCodes.AccountsReceivable, receivableLine.AccountCode);
        Assert.Equal(180m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.VatOutput).Credit);
    }

    [Fact]
    public async Task PostInvoice_InClosedPeriod_IsRejected()
    {
        await SetupAsync();

        var period = FiscalPeriod.Create(_companyId, 2026, 7);
        period.Close();
        _db.FiscalPeriods.Add(period);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5));
        invoice.AddLine("خدمة", 1m, 100m);

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new SalesInvoicePostingService(_db).PostAsync(invoice));
        Assert.Contains("مقفلة", ex.Message);
    }

    [Fact]
    public async Task ManualEntry_InClosedPeriod_IsRejected()
    {
        await SetupAsync();

        var period = FiscalPeriod.Create(_companyId, 2026, 8);
        period.Close();
        _db.FiscalPeriods.Add(period);
        await _db.SaveChangesAsync();

        var cash = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1101");
        var sales = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "4101");
        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 8, 10), "قيد في فترة مقفلة");
        entry.AddLine(cash, 100m, 0m);
        entry.AddLine(sales, 0m, 100m);

        await Assert.ThrowsAsync<AccountingException>(
            () => new JournalPostingService(_db).PostAsync(entry));
    }

    [Fact]
    public async Task PostedInvoice_CannotBeModified()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5));
        invoice.AddLine("خدمة", 1m, 100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Throws<AccountingException>(() => invoice.AddLine("صنف إضافي", 1m, 50m));
    }

    [Fact]
    public async Task InvoiceNumbers_AreSequential()
    {
        await SetupAsync();
        var service = new SalesInvoicePostingService(_db);

        for (var i = 1; i <= 3; i++)
        {
            var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, i));
            invoice.AddLine("خدمة", 1m, 100m);
            var posted = await service.PostAsync(invoice);
            Assert.Equal($"INV-2026-{i:D6}", posted.InvoiceNumber);
        }
    }

    [Fact]
    public async Task PosCashSale_DebitsCashNotReceivable()
    {
        await SetupAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5));
        invoice.AddLine("بيع نقدي", 1m, 100m);
        invoice.MarkPosSale(1, PosPaymentMethod.Cash);

        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);
        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == posted.JournalEntryId);

        Assert.Equal(115m, entry.Lines.Single(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.CashOnHand).Debit);
        Assert.DoesNotContain(entry.Lines, l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsReceivable);
    }

    [Fact]
    public async Task B2bInvoice_WithZatcaEnabled_AutoSubmitsInSimulation()
    {
        await SetupAsync();
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(true, ZatcaEnvironment.Simulation, "", "", "", "");
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 5));
        invoice.AddLine("خدمة B2B", 1m, 1000m);
        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var zatca = await _db.ZatcaEInvoiceDocuments.SingleAsync(d =>
            d.CompanyId == _companyId && d.SourceDocumentId == posted.Id);
        // (CEEF-229 · م0-١٠) المحاكاة ليست اعتماداً.
        Assert.Equal(ZatcaComplianceStatus.SimulatedLocally, zatca.ComplianceStatus);
        Assert.False(zatca.ComplianceStatus.IsAcknowledgedByAuthority());
    }
}
