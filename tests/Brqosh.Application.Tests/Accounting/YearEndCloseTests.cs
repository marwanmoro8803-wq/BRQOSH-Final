using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

public class YearEndCloseTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public YearEndCloseTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    [Fact]
    public async Task YearEndClose_Closes_Pnl_Into_Retained_Earnings()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة", "310123456700099", BusinessProfile.Retail);
        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2025, 6, 15));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var result = await new YearEndCloseService(_db).CloseYearAsync(company.Id, 2025, "admin");
        Assert.Equal(2025, result.Year);
        Assert.True(result.NetIncome > 0);
        Assert.False(string.IsNullOrWhiteSpace(result.EntryNumber));

        var closeEntry = await _db.JournalEntries.Include(e => e.Lines)
            .SingleAsync(e => e.Id == result.ClosingEntryId);
        Assert.Equal(JournalEntryStatus.Posted, closeEntry.Status);
        Assert.Equal(closeEntry.TotalDebit, closeEntry.TotalCredit);

        var openPeriods = await _db.FiscalPeriods.CountAsync(p =>
            p.CompanyId == company.Id && p.StartDate.Year == 2025 && p.Status == FiscalPeriodStatus.Open);
        Assert.Equal(0, openPeriods);
    }

    [Fact]
    public async Task YearEndClose_Cannot_Run_Twice()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة", "310123456700088", BusinessProfile.General);

        await new YearEndCloseService(_db).CloseYearAsync(company.Id, 2024, "admin");
        await Assert.ThrowsAsync<AccountingException>(() =>
            new YearEndCloseService(_db).CloseYearAsync(company.Id, 2024, "admin"));
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
