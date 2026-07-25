using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Construction;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace Brqosh.Application.Tests.Construction;

public class RetentionReleaseTests
{
    [Fact]
    public async Task Customer_retention_release_posts_balanced_entry()
    {
        var path = Path.Combine(Path.GetTempPath(), $"brqosh-ret-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={path}");
            var company = await new DatabaseInitializer(db).InitializeAsync("مقاولات", "300000000000005", BusinessProfile.Construction);

            var customer = Customer.Create(company.Id, "عميل", "300000000000006");
            db.Customers.Add(customer);

            var project = ConstructionProject.Create(
                company.Id, "PRJ-01", "مشروع تجريبي", customer.Id,
                contractValue: 1_000_000m, retentionPercent: 10m);
            project.Activate();
            await new ConstructionProjectService(db).CreateAsync(project);
            await db.SaveChangesAsync();

            var billing = ProgressBilling.CreateDraft(
                company.Id, project.Id, customer.Id, DateOnly.FromDateTime(DateTime.Today),
                "مستخلص", 100_000m, 10m);
            await new ProgressBillingPostingService(db).PostAsync(billing);

            var entry = await new RetentionReleaseService(db).ReleaseAsync(
                company.Id, project.Id, RetentionReleaseType.CustomerRelease,
                5000m, DateOnly.FromDateTime(DateTime.Today), "إفراج جزئي", "admin");

            Assert.Equal(JournalEntryStatus.Posted, entry.Status);
            Assert.Equal(5000m, entry.TotalDebit);
            Assert.Equal(5000m, entry.TotalCredit);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
