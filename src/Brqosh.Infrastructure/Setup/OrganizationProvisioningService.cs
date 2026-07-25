using Brqosh.Application.Setup;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Setup;

public sealed class OrganizationProvisioningService(BrqoshDbContext db) : IOrganizationProvisioningService
{
    public async Task<OrganizationProvisionResult> ProvisionAsync(
        OrganizationProvisionRequest request, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();

        var name = request.CompanyName.Trim();
        if (name.Length < 2)
            throw new AccountingException("اسم المنشأة مطلوب.");

        var vat = request.VatNumber.Trim();
        if (vat.Length > 0 && (vat.Length != 15 || !vat.All(char.IsDigit)))
            throw new AccountingException("الرقم الضريبي يجب أن يتكون من 15 رقماً.");

        var countryId = string.IsNullOrWhiteSpace(request.CountryTemplateId)
            ? "saudi-arabia"
            : request.CountryTemplateId.Trim();

        var company = await new DatabaseInitializer(db)
            .CreateAdditionalCompanyAsync(name, vat, request.Profile, countryId);

        long? adminId = null;
        if (!string.IsNullOrWhiteSpace(request.AdminUsername) &&
            !string.IsNullOrWhiteSpace(request.AdminPassword))
        {
            var admin = await new UserManagementService(db).CreateAsync(
                company.Id,
                request.AdminUsername,
                request.AdminPassword,
                string.IsNullOrWhiteSpace(request.AdminDisplayName)
                    ? request.AdminUsername
                    : request.AdminDisplayName.Trim(),
                UserRole.Administrator,
                ct: ct);
            adminId = admin.Id;
        }

        return new OrganizationProvisionResult(company.Id, company.NameAr, adminId);
    }
}
