using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

public class FxRevaluationTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public FxRevaluationTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    [Fact]
    public async Task Revalue_foreign_bank_posts_unrealized_gain()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync(
            "شركة", "300000000000001", BusinessProfile.General);
        var companyId = company.Id;

        var bank = await new BankAccountService(_db).CreateAsync(
            companyId, "USD1", "حساب دولار", "البنك الأهلي", "999", "SA0000000000000000000001", true);
        bank.SetCurrency("USD");
        await _db.SaveChangesAsync();

        var bankGl = await _db.Accounts.SingleAsync(a => a.Id == bank.GlAccountId);
        var capital = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == companyId && a.Code == "3101");

        var opening = JournalEntry.CreateDraft(companyId, new DateOnly(2026, 1, 1),
            "رصيد افتتاحي USD", "Opening:USD");
        opening.AddLine(bankGl, 3750m, 0m, "1000 USD @ 3.75");
        opening.AddLine(capital, 0m, 3750m, "رأس المال");
        await new JournalPostingService(_db).PostAsync(opening);

        var asOfDate = new DateOnly(2026, 7, 8);
        var entry = await new FxRevaluationService(_db).RevalueAsync(
            companyId, bank.Id, asOfDate, 3.80m, "admin", foreignBalance: 1000m);

        Assert.NotNull(entry);
        Assert.Equal(JournalEntryStatus.Posted, entry!.Status);
        Assert.Equal(entry.TotalDebit, entry.TotalCredit);

        var gainAccount = await _db.Accounts.SingleAsync(a =>
            a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.UnrealizedFxGain);

        var gainLine = entry.Lines.Single(l => l.AccountId == gainAccount.Id);
        Assert.Equal(0m, gainLine.Debit);
        Assert.Equal(50m, gainLine.Credit);

        var bankLine = entry.Lines.Single(l => l.AccountId == bankGl.Id);
        Assert.Equal(50m, bankLine.Debit);
        Assert.Equal(0m, bankLine.Credit);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
