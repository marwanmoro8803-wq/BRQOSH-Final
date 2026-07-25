using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Construction;

public class ConstructionCycleTests : IDisposable
{
    private readonly BrqoshDbContext _db;

    public ConstructionCycleTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Progress_billing_posts_balanced_journal_entry()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة مقاولات", "300000000000001",
            Brqosh.Domain.Modules.BusinessProfile.Construction);

        var customer = Customer.Create(company.Id, "عميل مشروع", "300000000000002");
        _db.Customers.Add(customer);

        var project = ConstructionProject.Create(company.Id, "PRJ-001", "برج سكني", customer.Id,
            contractValue: 1_000_000m, retentionPercent: 10m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);
        await _db.SaveChangesAsync();

        var billing = ProgressBilling.CreateDraft(
            company.Id, project.Id, customer.Id, new DateOnly(2026, 3, 1),
            "مستخلص أول", 100_000m, 10m);

        await new ProgressBillingPostingService(_db).PostAsync(billing);

        Assert.Equal(ProgressBillingStatus.Posted, billing.Status);
        Assert.NotNull(billing.JournalEntryId);

        var entry = await _db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == billing.JournalEntryId);
        Assert.Equal(entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit));
        Assert.Equal(90_000m, billing.NetBillableAmount);
        Assert.Equal(10_000m, billing.RetentionAmount);
        Assert.Equal(15_000m, billing.VatAmount);
    }

    [Fact]
    public async Task Boq_items_sum_on_project()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة", "300000000000003",
            Brqosh.Domain.Modules.BusinessProfile.Construction);

        var project = ConstructionProject.Create(company.Id, "PRJ-002", "مستودع");
        project.AddBoqItem("1", "حفر", "م³", 100m, 50m);
        project.AddBoqItem("2", "خرسانة", "م³", 200m, 400m);
        await new ConstructionProjectService(_db).CreateAsync(project);

        var loaded = await _db.ConstructionProjects.SingleAsync(p => p.Id == project.Id);
        Assert.Equal(2, loaded.BoqItems.Count);
        Assert.Equal(85_000m, loaded.BoqTotal);
    }

    [Fact]
    public async Task Posted_progress_billing_cancel_reverses_journal()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("شركة مقاولات", "300000000000001",
            Brqosh.Domain.Modules.BusinessProfile.Construction);

        var customer = Customer.Create(company.Id, "عميل مشروع", "300000000000002");
        _db.Customers.Add(customer);

        var project = ConstructionProject.Create(company.Id, "PRJ-CAN", "مشروع إلغاء", customer.Id,
            contractValue: 1_000_000m, retentionPercent: 10m);
        project.Activate();
        await new ConstructionProjectService(_db).CreateAsync(project);
        await _db.SaveChangesAsync();

        var billing = ProgressBilling.CreateDraft(
            company.Id, project.Id, customer.Id, new DateOnly(2026, 3, 1),
            "مستخلص أول", 100_000m, 10m);
        await new ProgressBillingPostingService(_db).PostAsync(billing);
        var entryId = billing.JournalEntryId!.Value;

        await new ProgressBillingCancellationService(_db).CancelAsync(
            company.Id, billing.Id, new DateOnly(2026, 3, 15), "admin");

        Assert.Equal(ProgressBillingStatus.Cancelled, billing.Status);
        var entry = await _db.JournalEntries.SingleAsync(e => e.Id == entryId);
        Assert.Equal(JournalEntryStatus.Reversed, entry.Status);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
