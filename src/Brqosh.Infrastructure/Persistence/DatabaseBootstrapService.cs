using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// تهيئة موحّدة لقاعدة البيانات عند كل تشغيل — EnsureCreated + ترقيات + بذور.
/// </summary>
public class DatabaseBootstrapService(BrqoshDbContext db)
{
    public async Task<CompanyBootstrapResult> InitializeAsync(
        string companyName,
        string vatNumber,
        BusinessProfile profile,
        string? adminUsername = null,
        string? adminPassword = null,
        string countryTemplateId = "saudi-arabia",
        CancellationToken ct = default)
    {
        var company = await new DatabaseInitializer(db)
            .InitializeAsync(companyName, vatNumber, profile, countryTemplateId);

        await new BrqoshSchemaUpdater(db).ApplyAsync(ct);

        if (!string.IsNullOrWhiteSpace(adminUsername) && !string.IsNullOrWhiteSpace(adminPassword))
            await new UserAuthService(db).CreateInitialAdminAsync(company.Id, adminUsername, adminPassword, ct);

        if (!await db.InventoryBalances.AnyAsync(b => b.CompanyId == company.Id, ct))
            await new InventoryService(db).RebuildBalancesFromMovementsAsync(company.Id, ct);

        return new CompanyBootstrapResult(company.Id, company.NameAr);
    }
}

public record CompanyBootstrapResult(long CompanyId, string CompanyName);
