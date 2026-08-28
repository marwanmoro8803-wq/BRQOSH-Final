using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

public class CashFlowStatementTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public CashFlowStatementTests()
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
            .InitializeAsync("شركة تدفق", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
    }

    [Fact]
    public async Task CashFlowStatement_WithSalesInvoice_ShowsOperatingNetIncome()
    {
        await SetupAsync();
        var customer = Customer.Create(_companyId, "عميل", "311122233300003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 6, 15));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);

        var result = await new CashFlowStatementService(_db).ExecuteAsync(
            _companyId, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        Assert.Contains(result.Operating.Lines, l => l.Label.Contains("صافي الدخل"));

        // (CEEF-228) كان هنا `Assert.True(result.Operating.Subtotal != 0m)` — وهو تأكيدٌ
        // يُثبّت العطل: في فاتورة **آجلة** لم يُقبض شيء، فالتدفق التشغيليّ الصحيح **صفر**.
        // كان الاختبار يمرّ لأن القيمة كانت -300 (ضريبة المخرجات محسوبة بإشارة معكوسة
        // وخارج رأس المال العامل). القيمة تُقاس الآن ولا يُكتفى بكونها غير صفرية.
        Assert.Equal(1000m, result.Operating.Lines.Single(l => l.Label.Contains("صافي الدخل")).Amount);
        Assert.Equal(0m, result.Operating.Subtotal);
        Assert.Equal(result.OpeningCash, result.ClosingCash);
    }
}
