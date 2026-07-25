using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

public class FxRevaluationBatchTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public FxRevaluationBatchTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    [Fact]
    public async Task Batch_revalues_foreign_banks_with_stored_rates()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة", "300000000000004", BusinessProfile.General);
        var companyId = company.Id;

        var bank = await new BankAccountService(_db).CreateAsync(
            companyId, "USD1", "حساب دولار", "البنك", "999", "SA0000000000000000000001", true);
        bank.SetCurrency("USD");
        await _db.SaveChangesAsync();

        var rateService = new ExchangeRateService(_db);
        await rateService.RecordRateAsync(companyId, "USD", "SAR", new DateOnly(2026, 7, 8), 3.80m);

        var bankGl = await _db.Accounts.SingleAsync(a => a.Id == bank.GlAccountId);
        var capital = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == companyId && a.Code == "3101");

        var opening = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 1, 1), "افتتاحي", "Open");
        opening.AddLine(bankGl, 3750m, 0m, "1000 USD @ 3.75");
        opening.AddLine(capital, 0m, 3750m, "رأس المال");
        await new JournalPostingService(_db).PostAsync(opening);

        var entries = await new FxRevaluationBatchService(
            _db,
            rateService,
            new FxRevaluationService(_db)).RevalueAllForeignBanksAsync(
            companyId,
            new DateOnly(2026, 7, 8),
            "admin",
            new Dictionary<long, decimal> { [bank.Id] = 1000m });

        Assert.Single(entries);
        Assert.Equal(50m, entries[0].TotalDebit);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
