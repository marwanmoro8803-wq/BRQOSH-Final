using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Accounting;

/// <summary>‏حساباتُ أدوار الترحيل لكلّ منشأة: تُعبَّأ بالأرقام الافتراضيّة، والترحيلُ يتبع تعديلَها، والإعادةُ تُرجعه.</summary>
public class PostingRoleAccountsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public PostingRoleAccountsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<(long CompanyId, long SupplierId)> CompanyAsync()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة أدوار", "310123456700003", BusinessProfile.Retail);
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
        var supplier = Supplier.Create(company.Id, "مورد", "312222222200003");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        return (company.Id, supplier.Id);
    }

    private async Task<IReadOnlyList<string>> DebitCodesOfServicePurchaseAsync(long companyId, long supplierId, string reference)
    {
        var purchase = PurchaseInvoice.CreateDraft(companyId, supplierId, new DateOnly(2026, 7, 1), reference, true);
        purchase.AddLine("خدمة صيانة", 1m, 100m);
        await new PurchaseInvoicePostingService(_db).PostAsync(purchase);
        var entry = await _db.JournalEntries.AsNoTracking().Include(e => e.Lines).SingleAsync(e => e.Id == purchase.JournalEntryId);
        return entry.Lines.Where(l => l.Debit > 0m).Select(l => l.AccountCode).ToList();
    }

    [Fact]
    public async Task NewCompany_IsSeededWithEveryRoleAtItsDefault()
    {
        var (companyId, _) = await CompanyAsync();
        var roles = await new PostingRoleService(_db).ListAsync(companyId);
        Assert.Equal(PostingRoleAccount.Defaults.Count, await _db.PostingRoleAccounts.CountAsync(r => r.CompanyId == companyId));
        Assert.Equal(PostingRoleAccount.Defaults.Count, roles.Count);
        Assert.All(roles, r => Assert.False(r.IsCustom));
        Assert.Contains(roles, r => r.Role == nameof(SaudiChartOfAccounts.PostingRoleCodes.Purchases) && r.AccountCode == "5102");
    }

    [Fact]
    public async Task Posting_FollowsTheCompanyMapping_AndResetRestoresTheDefault()
    {
        var (companyId, supplierId) = await CompanyAsync();
        var service = new PostingRoleService(_db);
        const string purchasesRole = nameof(SaudiChartOfAccounts.PostingRoleCodes.Purchases);

        Assert.Contains("5102", await DebitCodesOfServicePurchaseAsync(companyId, supplierId, "SUP-1"));

        await service.SetAsync(companyId, purchasesRole, "5215");
        var mapped = await DebitCodesOfServicePurchaseAsync(companyId, supplierId, "SUP-2");
        Assert.Contains("5215", mapped);
        Assert.DoesNotContain("5102", mapped);
        Assert.True((await service.ListAsync(companyId)).Single(r => r.Role == purchasesRole).IsCustom);

        await service.ResetAsync(companyId, purchasesRole);
        Assert.Contains("5102", await DebitCodesOfServicePurchaseAsync(companyId, supplierId, "SUP-3"));
    }

    [Fact]
    public async Task Mapping_ToAGroupOrMissingAccountOrUnknownRole_IsRefused()
    {
        var (companyId, _) = await CompanyAsync();
        var service = new PostingRoleService(_db);
        const string role = nameof(SaudiChartOfAccounts.PostingRoleCodes.Purchases);
        await Assert.ThrowsAsync<AccountingException>(() => service.SetAsync(companyId, role, "5"));
        await Assert.ThrowsAsync<AccountingException>(() => service.SetAsync(companyId, role, "9999"));
        await Assert.ThrowsAsync<AccountingException>(() => service.SetAsync(companyId, "NoSuchRole", "5102"));
        Assert.Equal("5102", await PostingRoleService.ResolveAsync(_db, companyId, "5102"));
    }
}
