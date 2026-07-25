using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>ACC-001 — ميزان يناير يبقى ثابتاً بعد إلغاء فاتورة فبراير.</summary>
public class TrialBalanceHistoricalTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public TrialBalanceHistoricalTests()
    {
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
    public async Task January_TrialBalance_Unchanged_After_February_Cancellation()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin-test";

        var janDate = new DateOnly(2026, 1, 15);
        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, janDate);
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var janBefore = await new TrialBalanceQuery(_db).ExecuteAsync(
            company.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var salesJanBefore = janBefore.Single(r => r.AccountCode == "4101").TotalCredit;

        await new SalesInvoiceCancellationService(_db).CancelAsync(
            company.Id, invoice.Id, new DateOnly(2026, 2, 10), "admin-test");

        var janAfter = await new TrialBalanceQuery(_db).ExecuteAsync(
            company.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var salesJanAfter = janAfter.Single(r => r.AccountCode == "4101").TotalCredit;

        Assert.Equal(1000m, salesJanBefore);
        Assert.Equal(salesJanBefore, salesJanAfter);
    }
}
