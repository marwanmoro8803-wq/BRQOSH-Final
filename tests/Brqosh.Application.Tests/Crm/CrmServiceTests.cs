using Brqosh.Domain.Crm;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Crm;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Crm;

public class CrmServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly CrmService _crm;
    private long _companyId;

    public CrmServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _crm = new CrmService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateLead_and_convert_creates_customer()
    {
        await SeedCompanyAsync();
        var lead = await _crm.CreateLeadAsync(CrmLead.Create(_companyId, "عميل محتمل", "0500000000"));
        var customer = await _crm.ConvertLeadAsync(_companyId, lead.Id);
        Assert.True(customer.Id > 0);
        var updated = await _db.CrmLeads.SingleAsync(l => l.Id == lead.Id);
        Assert.Equal(CrmLeadStatus.Converted, updated.Status);
        Assert.Equal(customer.Id, updated.CustomerId);
    }

    [Fact]
    public async Task CreateOpportunity_and_log_activity()
    {
        await SeedCompanyAsync();
        var opp = await _crm.CreateOpportunityAsync(
            CrmOpportunity.Create(_companyId, "صفقة كبرى", 250_000m));
        var activity = await _crm.LogActivityAsync(
            CrmActivity.Log(_companyId, CrmActivityType.Meeting, "اجتماع تقديم", opportunityId: opp.Id));
        Assert.True(opp.Id > 0);
        Assert.Equal(opp.Id, activity.OpportunityId);
    }

    [Fact]
    public void ModuleCatalog_includes_Crm_for_retail()
    {
        var modules = ModuleCatalog.ResolveModules(BusinessProfile.Retail);
        Assert.Contains(ModuleKey.Crm, modules);
    }

    private async Task SeedCompanyAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("CRM Co", "310123456700003", BusinessProfile.Retail);
        _companyId = company.Id;
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
        await new UserAuthService(_db).CreateInitialAdminAsync(company.Id, "admin", "SecurePass12!");
    }
}
