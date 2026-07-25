using Brqosh.Domain.Modules;
using Brqosh.Domain.Purchasing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Brqosh.Infrastructure.Treasury;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Treasury;

public class PaymentRunTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public PaymentRunTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateDraft_IncludesDuePurchaseInvoicesOnly()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة PR", "310123456700015", BusinessProfile.General);
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var supplier = Supplier.Create(company.Id, "مورد مستحق");
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        var dueInvoice = PurchaseInvoice.CreateDraft(
            company.Id, supplier.Id, new DateOnly(2026, 5, 1), "DUE-001", true);
        dueInvoice.AddLine("بضاعة", 1m, 1000m);
        await new PurchaseInvoicePostingService(_db).PostAsync(dueInvoice);

        var futureInvoice = PurchaseInvoice.CreateDraft(
            company.Id, supplier.Id, new DateOnly(2026, 7, 20), "FUT-001", true);
        futureInvoice.AddLine("بضاعة", 1m, 500m);
        await new PurchaseInvoicePostingService(_db).PostAsync(futureInvoice);

        var draft = await new PaymentRunService(_db)
            .CreateDraftAsync(company.Id, new DateOnly(2026, 7, 9));

        Assert.Single(draft.Lines);
        Assert.Equal(dueInvoice.Id, draft.Lines[0].InvoiceId);
        Assert.True(draft.TotalAmount > 0m);
    }
}
