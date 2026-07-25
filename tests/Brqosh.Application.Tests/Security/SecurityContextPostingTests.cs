using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Security;

public class SecurityContextPostingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public SecurityContextPostingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }

    [Fact]
    public async Task Viewer_CannotPostSalesInvoice()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        SecurityContext.CurrentRole = UserRole.Viewer;
        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 8));
        invoice.AddLine("خدمة", 1m, 100m);

        await Assert.ThrowsAsync<Domain.Accounting.AccountingException>(
            () => new SalesInvoicePostingService(_db).PostAsync(invoice));
    }

    [Fact]
    public async Task Viewer_CannotCancelSalesInvoice()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة", "310123456700003", BusinessProfile.Retail);
        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        SecurityContext.CurrentRole = UserRole.Administrator;
        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 8));
        invoice.AddLine("خدمة", 1m, 100m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        SecurityContext.CurrentRole = UserRole.Viewer;
        await Assert.ThrowsAsync<Domain.Accounting.AccountingException>(
            () => new SalesInvoiceCancellationService(_db).CancelAsync(
                company.Id, invoice.Id, new DateOnly(2026, 7, 8), "viewer"));
    }

    [Fact]
    public void NullRole_WithoutSession_ThrowsOnPost()
    {
        var prior = Environment.GetEnvironmentVariable("BRQOSH_TEST");
        try
        {
            Environment.SetEnvironmentVariable("BRQOSH_TEST", null);
            SecurityContext.CurrentRole = null;
            Assert.Throws<Domain.Accounting.AccountingException>(() => SecurityContext.EnsureCanPost());
        }
        finally
        {
            Environment.SetEnvironmentVariable("BRQOSH_TEST", prior ?? "1");
        }
    }
}
