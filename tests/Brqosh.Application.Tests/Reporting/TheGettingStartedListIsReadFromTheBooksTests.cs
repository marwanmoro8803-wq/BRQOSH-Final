using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Reporting;
using Brqosh.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Reporting;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · «صفحة ترحيب ومراحل تتبّع») قائمةُ «ابدأ هنا» تُقرأ من الدفاتر: الخطوةُ منجزةٌ إن وُجد أثرُها فعلاً.
/// </summary>
public class TheGettingStartedListIsReadFromTheBooksTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public TheGettingStartedListIsReadFromTheBooksTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
    }

    private async Task<Dictionary<string, bool>> StepsAsync() =>
        (await new OnboardingQuery(_db).ExecuteAsync(_companyId)).ToDictionary(s => s.Code, s => s.Done);

    [Fact]
    public async Task ANewCompany_HasNothingDone_AndEachStepNamesItsScreen()
    {
        _companyId = (await new DatabaseInitializer(_db).InitializeAsync("منشأة", "310123456700003", BusinessProfile.Retail)).Id;

        var steps = await new OnboardingQuery(_db).ExecuteAsync(_companyId);

        Assert.Equal(["company-data", "logo", "first-customer", "first-product", "first-invoice"], steps.Select(s => s.Code).ToArray());
        Assert.All(steps, s => Assert.False(s.Done, s.Code));
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.PageKey)));
        Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.TitleEn)));
    }

    [Fact]
    public async Task EachStep_TurnsDone_OnlyWhenItsTraceExists()
    {
        _companyId = (await new DatabaseInitializer(_db).InitializeAsync("منشأة", "310123456700003", BusinessProfile.Retail)).Id;

        var customer = Customer.Create(_companyId, "عميل", "311122233300003");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        var steps = await StepsAsync();
        Assert.True(steps["first-customer"]);
        Assert.False(steps["first-product"]);
        Assert.False(steps["first-invoice"]);

        var product = Product.Create(_companyId, "P1", "خدمة", "خدمة", ProductType.Service, 100m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        Assert.True((await StepsAsync())["first-product"]);

        // ‏المسودّةُ لا تكفي: «أصدر أوّل فاتورة» تعني مرحَّلة.
        var draft = SalesInvoice.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 10, 1));
        draft.AddLine("خدمة", 1m, 100m, product.Id);
        _db.SalesInvoices.Add(draft);
        await _db.SaveChangesAsync();
        Assert.False((await StepsAsync())["first-invoice"]);

        _db.SalesInvoices.Remove(draft);
        await _db.SaveChangesAsync();
        var posted = SalesInvoice.CreateDraft(_companyId, customer.Id, new DateOnly(2026, 10, 2));
        posted.AddLine("خدمة", 1m, 100m, product.Id);
        await new SalesInvoicePostingService(_db).PostAsync(posted);
        Assert.True((await StepsAsync())["first-invoice"]);

        var company = await _db.Companies.SingleAsync(c => c.Id == _companyId);
        company.SetLogoFilePath("C:/x/logo.png");
        await _db.SaveChangesAsync();
        Assert.True((await StepsAsync())["logo"]);
    }
}
