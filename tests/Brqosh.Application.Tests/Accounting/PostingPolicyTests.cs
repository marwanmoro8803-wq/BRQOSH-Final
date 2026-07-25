using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

public class PostingPolicyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;
    private long _inactiveAccountId;

    public PostingPolicyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditLogImmutabilityInterceptor())
            .Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة قواعد", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var inactive = await _db.Accounts
            .Where(a => a.CompanyId == _companyId && !a.IsGroup && !a.IsSystem)
            .FirstAsync();
        inactive.Deactivate();
        _inactiveAccountId = inactive.Id;
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Validate_RejectsClosedPeriod()
    {
        await SetupAsync();
        var date = new DateOnly(2026, 7, 15);
        var period = FiscalPeriod.Create(_companyId, date.Year, date.Month);
        period.Close();
        _db.FiscalPeriods.Add(period);
        await _db.SaveChangesAsync();

        var orchestrator = new PostingOrchestrator(_db);
        await Assert.ThrowsAsync<AccountingException>(() =>
            orchestrator.ValidateDocumentPostAsync(_companyId, date, PostingDocumentKind.SalesInvoice));
    }

    [Fact]
    public async Task Validate_RejectsInactiveAccount()
    {
        await SetupAsync();
        var orchestrator = new PostingOrchestrator(_db);
        await Assert.ThrowsAsync<AccountingException>(() =>
            orchestrator.ValidateDocumentPostAsync(
                _companyId, new DateOnly(2026, 7, 9), PostingDocumentKind.JournalEntry,
                accountIds: [_inactiveAccountId]));
    }

    [Fact]
    public async Task ResolveAccount_ReturnsActiveSystemCode()
    {
        await SetupAsync();
        var policy = new SaudiPostingPolicy(_db);
        var id = await policy.ResolveAccountIdAsync(
            _companyId, SaudiChartOfAccounts.SystemCodes.CashOnHand);
        var account = await _db.Accounts.SingleAsync(a => a.Id == id);
        Assert.Equal(SaudiChartOfAccounts.SystemCodes.CashOnHand, account.Code);
        Assert.True(account.IsActive);
    }
}
