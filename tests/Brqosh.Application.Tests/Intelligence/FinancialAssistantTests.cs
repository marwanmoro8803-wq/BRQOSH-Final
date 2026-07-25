using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Intelligence;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Intelligence;

public class FinancialAssistantTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;

    public FinancialAssistantTests()
    {
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
    public async Task Ask_maps_cash_flow_question_to_kpis()
    {
        var company = await SeedCompanyWithPostedInvoiceAsync();
        var assistant = new FinancialAssistantService(_db, new FinancialIntelligenceService(_db, new DataQualityService(_db)));

        var reply = await assistant.AskAsync(company.Id, "ما هو التدفق النقدي؟");

        Assert.Equal(FinancialIntelligenceService.EngineId, reply.Source);
        Assert.Contains("تحصيلات", reply.AnswerAr);
    }

    [Fact]
    public async Task Ask_maps_vat_keyword()
    {
        var company = await SeedCompanyWithPostedInvoiceAsync();
        var assistant = new FinancialAssistantService(_db, new FinancialIntelligenceService(_db, new DataQualityService(_db)));

        var reply = await assistant.AskAsync(company.Id, "VAT status");

        Assert.Contains("ضريبة", reply.AnswerAr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ask_empty_question_returns_prompt()
    {
        var company = await SeedCompanyWithPostedInvoiceAsync();
        var assistant = new FinancialAssistantService(_db, new FinancialIntelligenceService(_db, new DataQualityService(_db)));

        var reply = await assistant.AskAsync(company.Id, "   ");

        Assert.Contains("اكتب سؤالك", reply.AnswerAr);
    }

    private async Task<Company> SeedCompanyWithPostedInvoiceAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة مساعد", "310123456700003", BusinessProfile.Retail);
        var customer = Customer.Create(company.Id, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var invoice = SalesInvoice.CreateDraft(company.Id, customer.Id, new DateOnly(2026, 7, 5));
        invoice.AddLine("خدمة", 1m, 1000m);
        await new SalesInvoicePostingService(_db).PostAsync(invoice);
        return company;
    }
}
