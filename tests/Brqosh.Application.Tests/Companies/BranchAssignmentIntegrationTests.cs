using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Companies;

public class BranchAssignmentIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public BranchAssignmentIntegrationTests()
    {
        TestActor.Accountant();   // (م2-١) الفاعل مُصرَّح به — لا تجاوز
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task InitializeCompany_SeedsHeadquartersBranch()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة فروع", "310123456700003", BusinessProfile.Retail);

        var hq = await _db.Branches.SingleAsync(b => b.CompanyId == company.Id);
        Assert.Equal("HQ", hq.Code);
        Assert.True(hq.IsHeadquarters);
        Assert.True(hq.IsActive);
    }

    [Fact]
    public async Task PostSalesInvoice_WithoutAssignBranch_DefaultsToHeadquarters()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة فروع", "310123456700003", BusinessProfile.Retail);
        var hq = await _db.Branches.SingleAsync(b => b.CompanyId == company.Id && b.IsHeadquarters);

        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 5));
        invoice.AddLine("خدمة", 1m, 100m);

        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(hq.Id, posted.BranchId);
    }

    [Fact]
    public async Task PostSalesInvoice_WithExplicitBranch_PreservesBranchId()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة فروع", "310123456700003", BusinessProfile.Retail);
        var hq = await _db.Branches.SingleAsync(b => b.CompanyId == company.Id && b.IsHeadquarters);

        var branch = Branch.Create(company.Id, "BR2", "فرع جدة");
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 5));
        invoice.AddLine("خدمة", 1m, 100m);
        invoice.AssignBranch(branch.Id);

        var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        Assert.Equal(branch.Id, posted.BranchId);
        Assert.NotEqual(hq.Id, posted.BranchId);
    }

    [Fact]
    public async Task ResolveBranchId_PrefersActivePreferred_ElseHeadquarters()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة فروع", "310123456700003", BusinessProfile.Retail);
        var hq = await _db.Branches.SingleAsync(b => b.CompanyId == company.Id && b.IsHeadquarters);

        var branch = Branch.Create(company.Id, "BR2", "فرع جدة");
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        var svc = new BranchAssignmentService(_db);
        Assert.Equal(branch.Id, await svc.ResolveBranchIdAsync(company.Id, branch.Id));
        Assert.Equal(hq.Id, await svc.ResolveBranchIdAsync(company.Id, preferredBranchId: null));
        Assert.Equal(hq.Id, await svc.ResolveBranchIdAsync(company.Id, preferredBranchId: 999_999));
    }

    [Fact]
    public async Task PostSalesInvoice_UsesSessionPreferredBranch_WhenUnsetOnDocument()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة فروع", "310123456700003", BusinessProfile.Retail);
        var hq = await _db.Branches.SingleAsync(b => b.CompanyId == company.Id && b.IsHeadquarters);

        var branch = Branch.Create(company.Id, "BR2", "فرع جدة");
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        BranchSessionContext.PreferredBranchId = branch.Id;
        try
        {
            var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 5));
            invoice.AddLine("خدمة", 1m, 100m);

            var posted = await new SalesInvoicePostingService(_db).PostAsync(invoice);

            Assert.Equal(branch.Id, posted.BranchId);
            Assert.NotEqual(hq.Id, posted.BranchId);
        }
        finally
        {
            BranchSessionContext.PreferredBranchId = null;
        }
    }

    [Fact]
    public async Task PostCreditNote_InheritsBranch_FromOriginalInvoice()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة فروع", "310123456700003", BusinessProfile.Retail);
        var hq = await _db.Branches.SingleAsync(b => b.CompanyId == company.Id && b.IsHeadquarters);

        var branch = Branch.Create(company.Id, "BR2", "فرع جدة");
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        var customer = Customer.Create(company.Id, "عميل", "311111111100003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 5));
        invoice.AddLine("خدمة", 1m, 100m);
        invoice.AssignBranch(branch.Id);
        var postedInvoice = await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var note = CreditNote.CreateDraft(company.Id, customer.Id, postedInvoice.Id,
            new DateOnly(2026, 7, 6), "مرتجع");
        note.AddLine("خدمة", 1m, 50m);

        var postedNote = await new CreditNotePostingService(_db).PostAsync(note);

        Assert.Equal(branch.Id, postedNote.BranchId);
        Assert.NotEqual(hq.Id, postedNote.BranchId);
    }
}
