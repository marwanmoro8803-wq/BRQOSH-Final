using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Modules;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// تهيئة قاعدة البيانات عند أول تشغيل: إنشاء المخطط، إنشاء الشركة، وزرع دليل الحسابات السعودي.
/// ملاحظة هندسية: EnsureCreated مرحلي — سننتقل إلى EF Migrations عند استقرار المخطط.
/// </summary>
public class DatabaseInitializer(BrqoshDbContext db)
{
    public async Task<Company> InitializeAsync(string companyName, string vatNumber, BusinessProfile profile)
    {
        await db.Database.EnsureCreatedAsync();

        var existing = await db.Companies.FirstOrDefaultAsync();
        if (existing is not null)
            return existing;

        var company = Company.Create(companyName, vatNumber, profile);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        await SeedChartOfAccountsAsync(company.Id);
        return company;
    }

    /// <summary>زرع دليل الحسابات السعودي مع الحفاظ على الشجرة (الآباء قبل الأبناء).</summary>
    private async Task SeedChartOfAccountsAsync(long companyId)
    {
        if (await db.Accounts.AnyAsync(a => a.CompanyId == companyId))
            return;

        var idByCode = new Dictionary<string, long>();
        foreach (var seed in SaudiChartOfAccounts.Template)
        {
            long? parentId = seed.ParentCode is null ? null : idByCode[seed.ParentCode];
            var account = Account.Create(
                seed.Code, seed.NameAr, seed.NameEn, seed.Type,
                parentId, seed.IsGroup, seed.IsSystem, companyId);

            db.Accounts.Add(account);
            await db.SaveChangesAsync();
            idByCode[seed.Code] = account.Id;
        }
    }
}
