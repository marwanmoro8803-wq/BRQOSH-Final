using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Treasury;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Brqosh.Infrastructure.Zatca;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Sales;

/// <summary>Golden Path المرحلة 2: بيع → ZATCA → تحصيل (خطوات 35·41–48·63·40).</summary>
public class GoldenPathSellZatcaCollectTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _customerId;

    public GoldenPathSellZatcaCollectTests()
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

    private async Task SetupWithZatcaSimulationAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة GP", "310123456700003", Domain.Modules.BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var customer = Customer.Create(_companyId, "عميل GP", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _customerId = customer.Id;

        var settings = await _db.ZatcaSettings.SingleAsync(s => s.CompanyId == _companyId);
        settings.Update(true, ZatcaEnvironment.Simulation, "", "", "", "");
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GoldenPath_Sell_ZatcaSimulation_Collect_ReducesReceivable()
    {
        await SetupWithZatcaSimulationAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("خدمة استشارية", 1m, 10_000m);
        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(SalesInvoiceStatus.Posted, posted.Status);
        Assert.NotNull(posted.JournalEntryId);

        var zatca = await _db.ZatcaEInvoiceDocuments.SingleAsync(d =>
            d.CompanyId == _companyId && d.SourceDocumentId == posted.Id);
        Assert.Equal(ZatcaComplianceStatus.Cleared, zatca.ComplianceStatus);
        Assert.False(string.IsNullOrWhiteSpace(zatca.InvoiceUuid.ToString()));

        var arBefore = await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsReceivable)
            .SumAsync(l => l.Debit - l.Credit);

        var voucher = ReceiptVoucher.CreateDraft(
            _companyId, _customerId, new DateOnly(2026, 7, 10),
            posted.GrandTotal(), ReceiptMethod.Cash, "تحصيل فاتورة GP");
        var receipt = await new ReceiptVoucherPostingService(_db).PostAsync(voucher);

        Assert.Equal(ReceiptVoucherStatus.Posted, receipt.Status);

        var arAfter = await _db.JournalEntries
            .Where(e => e.CompanyId == _companyId)
            .SelectMany(e => e.Lines)
            .Where(l => l.AccountCode == SaudiChartOfAccounts.SystemCodes.AccountsReceivable)
            .SumAsync(l => l.Debit - l.Credit);

        Assert.Equal(arBefore - posted.GrandTotal(), arAfter);
    }

    [Fact]
    public async Task ZatcaOrchestrator_Simulation_SubmitInsideSameTransactionAsGl()
    {
        await SetupWithZatcaSimulationAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, _customerId, new DateOnly(2026, 7, 9));
        invoice.AddLine("اختبار ذرّية", 1m, 500m);

        await _db.Database.BeginTransactionAsync();
        try
        {
            await new SalesInvoicePostingService(_db).PostCoreAsync(invoice);
            await _db.SaveChangesAsync();

            await ZatcaPostingOrchestrator.GenerateInsideTransactionAsync(
                _db, _companyId,
                () => new ZatcaEInvoiceGenerator(_db).TryGenerateForSalesInvoiceAsync(invoice.Id));

            var zatca = await _db.ZatcaEInvoiceDocuments.SingleAsync(d => d.SourceDocumentId == invoice.Id);
            Assert.Equal(ZatcaComplianceStatus.Cleared, zatca.ComplianceStatus);

            await _db.Database.CommitTransactionAsync();
        }
        catch
        {
            await _db.Database.RollbackTransactionAsync();
            throw;
        }
    }
}
