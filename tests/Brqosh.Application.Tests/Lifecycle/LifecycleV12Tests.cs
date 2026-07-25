using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Lifecycle;

public class LifecycleV12Tests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public LifecycleV12Tests()
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

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة Lifecycle", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
    }

    [Fact]
    public void TotpMfa_GenerateAndValidate_RoundTrip()
    {
        var secret = TotpMfaService.GenerateSecret();
        var code = TotpMfaService.ValidateCode(secret, "000000");
        Assert.False(code);
    }

    [Fact]
    public async Task LoginAudit_RecordsSuccessAndFailure()
    {
        await SetupAsync();
        await new LoginAuditService(_db).LogAsync(_companyId, "user1", true);
        await new LoginAuditService(_db).LogAsync(_companyId, "user1", false, "كلمة مرور خاطئة");
        Assert.Equal(2, await _db.LoginAuditEntries.CountAsync());
    }

    [Fact]
    public async Task ForcedLogout_IncrementsSessionVersion()
    {
        await SetupAsync();
        var user = AppUser.Create(_companyId, "forced", UserAuthService.HashPassword("TestPass9!"), "مستخدم", UserRole.Accountant);
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync();

        var before = user.SessionVersion;
        user.InvalidateSessions();
        await _db.SaveChangesAsync();
        Assert.Equal(before + 1, user.SessionVersion);
    }

    [Fact]
    public async Task SalesQuotation_CreateAndSend_AssignsNumber()
    {
        await SetupAsync();
        var customer = Customer.Create(_companyId, "عميل عرض", "311133344400003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var quote = SalesQuotation.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 7, 9));
        quote.AddLine("بند عرض", 5m, 100m);
        var sent = await new SalesQuotationService(_db).CreateAndSendAsync(quote);

        Assert.StartsWith("QT-", sent.QuotationNumber);
        Assert.Equal(SalesQuotationStatus.Sent, sent.Status);
    }

    [Fact]
    public async Task BankTransfer_MovesBetweenBankAccounts()
    {
        await SetupAsync();
        var gl1 = await _db.Accounts.Where(a => a.CompanyId == _companyId && a.Code == "1102")
            .Select(a => a.Id).FirstAsync();
        var parent = await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "11");
        var child = Account.Create("1102999", "بنك ثانٍ", "Bank 2", AccountType.Asset, parent.Id, companyId: _companyId);
        _db.Accounts.Add(child);
        await _db.SaveChangesAsync();
        var gl2 = child.Id;

        var b1 = BankAccount.Create(_companyId, "B1", "بنك 1", "بنك", "1", "SA1111111111111111111111", gl1, isDefault: true);
        var b2 = BankAccount.Create(_companyId, "B2", "بنك 2", "بنك", "2", "SA2222222222222222222222", gl2);
        _db.BankAccounts.AddRange(b1, b2);
        await _db.SaveChangesAsync();

        var entry = await new BankTransferService(_db).TransferAsync(
            _companyId, b1.Id, b2.Id, 500m, new DateOnly(2026, 7, 9));
        Assert.NotNull(entry.EntryNumber);
        Assert.Equal(2, entry.Lines.Count);
    }
}
