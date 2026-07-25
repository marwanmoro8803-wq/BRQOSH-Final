using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

public class ChequeTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public ChequeTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Received_cheque_posts_to_portfolio_and_clears()
    {
        var company = await SeedCompanyAsync();
        var companyId = company.Id;
        var (customerId, bankId) = await SeedCustomerAndBankAsync(companyId);

        var cheque = Cheque.CreateReceived(companyId, "CHQ-001", new DateOnly(2026, 1, 15),
            5000m, "شيك عميل", bankId, customerId);

        await new ChequePostingService(_db).RegisterReceivedAsync(cheque);
        Assert.NotNull(cheque.PortfolioJournalEntryId);

        await new ChequePostingService(_db).ClearAsync(companyId, cheque.Id, new DateOnly(2026, 1, 20));
        var updated = await _db.Cheques.SingleAsync(c => c.Id == cheque.Id);
        Assert.Equal(ChequeStatus.Cleared, updated.Status);
    }

    [Fact]
    public async Task Issued_cheque_posts_and_clears()
    {
        var company = await SeedCompanyAsync();
        var companyId = company.Id;
        var (_, bankId) = await SeedCustomerAndBankAsync(companyId);
        var supplier = Brqosh.Domain.Purchasing.Supplier.Create(companyId, "مورد تجريبي", "300000000000003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var cheque = Cheque.CreateIssued(companyId, "CHQ-OUT-01", new DateOnly(2026, 2, 1),
            3000m, "شيك مورد", bankId, supplier.Id);

        await new ChequePostingService(_db).RegisterIssuedAsync(cheque);
        await new ChequePostingService(_db).ClearAsync(companyId, cheque.Id, new DateOnly(2026, 2, 5));

        var updated = await _db.Cheques.SingleAsync(c => c.Id == cheque.Id);
        Assert.Equal(ChequeStatus.Cleared, updated.Status);
    }

    private async Task<Brqosh.Domain.Companies.Company> SeedCompanyAsync()
    {
        return await new DatabaseInitializer(_db).InitializeAsync("شركة", "300000000000001",
            Brqosh.Domain.Modules.BusinessProfile.General);
    }

    private async Task<(long CustomerId, long BankId)> SeedCustomerAndBankAsync(long companyId)
    {
        var customer = Brqosh.Domain.Sales.Customer.Create(companyId, "عميل", "300000000000002");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var bank = await new BankAccountService(_db).CreateAsync(
            companyId, "BNK1", "حساب رئيسي", "البنك الأهلي", "123", "SA0000000000000000000000", true);
        return (customer.Id, bank.Id);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
