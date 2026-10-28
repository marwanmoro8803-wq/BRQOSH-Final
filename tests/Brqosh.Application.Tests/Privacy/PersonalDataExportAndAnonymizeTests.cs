using Brqosh.Application.Privacy;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Domain.Sales;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Privacy;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Privacy;

/// <summary>
/// ‏(PDPL) <b>تصديرُ بيانات الشخص وإخفاؤها</b>: التصديرُ يحمل كلَّ ما في البطاقة، والإخفاءُ يمحوه ويُعطّل العميل
/// ولا يمسّ قيداً مرحَّلاً، وكلاهما في سجلّ التدقيق وللمسؤول وحدَه.
/// </summary>
public class PersonalDataExportAndAnonymizeTests : IDisposable
{
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public PersonalDataExportAndAnonymizeTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Database.CloseConnection();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ACustomer_IsExported_ThenAnonymized_AndThePostedLedgerIsUntouched()
    {
        await SetupAsync();
        var customer = Customer.Create(_companyId, "سارة أحمد", "", "0551234567", "الرياض، حي النخيل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        var entryId = await PostAsync("عميل: سارة أحمد");
        var ledgerBefore = await LedgerFingerprintAsync();

        var service = new PersonalDataService(_db);
        var export = await service.ExportAsync(_companyId, PersonalDataSubjectType.Customer, customer.Id, "admin");
        Assert.Contains(export.Fields, f => f.Value == "سارة أحمد");
        Assert.Contains(export.Fields, f => f.Value == "0551234567");
        Assert.Contains("0551234567", export.ToJson(), StringComparison.Ordinal);

        var result = await service.AnonymizeAsync(_companyId, PersonalDataSubjectType.Customer, customer.Id, "admin");

        _db.ChangeTracker.Clear();
        var after = await _db.Customers.SingleAsync(c => c.Id == customer.Id);
        Assert.Equal(PersonalDataService.CustomerPlaceholder(customer.Id), after.NameAr);
        Assert.Equal(result.Placeholder, after.NameAr);
        Assert.Equal("", after.Phone);
        Assert.Equal("", after.Address);
        Assert.False(after.IsActive);
        Assert.Equal(ledgerBefore, await LedgerFingerprintAsync());
        Assert.Equal("عميل: سارة أحمد", (await _db.JournalEntries.SingleAsync(e => e.Id == entryId)).Description);
        Assert.True(await _db.AuditLogEntries.AnyAsync(a => a.Action == PersonalDataService.ExportAction));
        Assert.True(await _db.AuditLogEntries.AnyAsync(a => a.Action == PersonalDataService.AnonymizeAction));
    }

    [Fact]
    public async Task AnEmployee_IsAnonymizedOnlyAfterTheirServiceEnds()
    {
        await SetupAsync();
        var employee = Employee.Create(_companyId, "E-77", "خالد سعيد", "1012345678", EmployeeNationality.Saudi, "محاسب", new DateOnly(2024, 1, 1));
        employee.SetIban("SA0380000000608010167519");
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();
        var service = new PersonalDataService(_db);

        var export = await service.ExportAsync(_companyId, PersonalDataSubjectType.Employee, employee.Id, "admin");
        Assert.Contains(export.Fields, f => f.Value == "1012345678");

        await Assert.ThrowsAsync<AccountingException>(() =>
            service.AnonymizeAsync(_companyId, PersonalDataSubjectType.Employee, employee.Id, "admin"));

        employee.Terminate(new DateOnly(2026, 6, 30));
        await _db.SaveChangesAsync();
        await service.AnonymizeAsync(_companyId, PersonalDataSubjectType.Employee, employee.Id, "admin");

        _db.ChangeTracker.Clear();
        var after = await _db.Employees.SingleAsync(e => e.Id == employee.Id);
        Assert.Equal(PersonalDataService.EmployeePlaceholder("E-77"), after.NameAr);
        Assert.Equal("", after.NationalId);
        Assert.Null(after.Iban);
        Assert.Equal("E-77", after.Code);
    }

    [Fact]
    public async Task OnlyAnAdministrator_MayExportOrAnonymize()
    {
        await SetupAsync();
        var customer = Customer.Create(_companyId, "عميل");
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        TestActor.Accountant();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            new PersonalDataService(_db).ExportAsync(_companyId, PersonalDataSubjectType.Customer, customer.Id, "acc"));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            new PersonalDataService(_db).AnonymizeAsync(_companyId, PersonalDataSubjectType.Customer, customer.Id, "acc"));
    }

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db).InitializeAsync("منشأة الخصوصية", "300000000000156", BusinessProfile.General);
        _companyId = company.Id;
    }

    private async Task<long> PostAsync(string description)
    {
        var entry = JournalEntry.CreateDraft(_companyId, new DateOnly(2026, 5, 1), description, sourceDocument: "Test:pdpl");
        entry.AddLine(await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "1101"), 50m, 0m);
        entry.AddLine(await _db.Accounts.SingleAsync(a => a.CompanyId == _companyId && a.Code == "4101"), 0m, 50m);
        return (await new JournalPostingService(_db).PostAsync(entry)).Id;
    }

    private async Task<string> LedgerFingerprintAsync() =>
        string.Join("|", (await _db.JournalEntries.AsNoTracking().OrderBy(e => e.Id).ToListAsync())
            .Select(e => $"{e.Id}:{e.Description}:{e.Status}:{string.Join(",", e.Lines.Select(l => $"{l.AccountCode}/{l.Debit}/{l.Credit}"))}"));
}
