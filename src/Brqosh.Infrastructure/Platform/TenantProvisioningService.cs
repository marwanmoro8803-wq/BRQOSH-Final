using Brqosh.Application.Platform;
using Brqosh.Application.Setup;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Platform;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Platform;

public class TenantProvisioningService(BrqoshDbContext db) : ITenantProvisioningService
{
    public async Task<TenantProvisionResult> ProvisionAsync(TenantProvisionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyNameAr))
            throw new AccountingException("اسم الشركة مطلوب.");

        var planCode = (request.PlanCode ?? "").Trim().ToUpperInvariant();
        var plan = await db.BillingPlans.SingleOrDefaultAsync(p => p.Code == planCode && p.IsActive, ct)
            ?? throw new AccountingException($"خطة الفوترة '{planCode}' غير موجودة أو غير نشطة.");

        var tenantId = Guid.NewGuid();
        var vat = string.IsNullOrWhiteSpace(request.VatNumber)
            ? $"3{DateTime.UtcNow.Ticks % 1_000_000_000_000_00:D14}"
            : request.VatNumber.Trim();

        var company = Domain.Companies.Company.CreateForTenant(
            tenantId, request.CompanyNameAr, vat, request.Profile);
        var config = new ConfigurationTemplateLoader().Resolve("saudi-arabia", request.Profile);
        company.SetCurrencies(config.Country.Currency.Base, config.Country.Currency.Functional);
        company.SetCostingMethod(ConfigurationTemplateLoader.ParseCostingMethod(config.Industry.Defaults.CostingMethod));
        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);

        await new DatabaseInitializer(db).SeedCompanyDefaultsAsync(company.Id, request.Profile, ct: ct);

        db.TenantSubscriptions.Add(TenantSubscription.Create(
            tenantId, plan.Id, SubscriptionStatus.Trial));
        await db.SaveChangesAsync(ct);

        return new TenantProvisionResult(tenantId, company.Id, plan.Code);
    }
}
