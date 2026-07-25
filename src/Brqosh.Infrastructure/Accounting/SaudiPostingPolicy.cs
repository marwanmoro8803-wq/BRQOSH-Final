using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>سياسة ترحيل سعودية — فترة مفتوحة · حسابات نشطة · لا ترحيل على مجموعات.</summary>
public sealed class SaudiPostingPolicy(BrqoshDbContext db) : IPostingPolicy
{
    public async Task ValidateBeforePostAsync(PostingValidationRequest request, CancellationToken ct = default)
    {
        var company = await db.Companies
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == request.CompanyId, ct)
            ?? throw new AccountingException("المنشأة غير موجودة.");
        company.EnsureCanPost();

        await new JournalPostingService(db).EnsurePeriodOpenAsync(request.CompanyId, request.DocumentDate);

        if (request.AccountIds is null || request.AccountIds.Count == 0)
            return;

        var accounts = await db.Accounts
            .Where(a => a.CompanyId == request.CompanyId && request.AccountIds.Contains(a.Id))
            .ToListAsync(ct);

        if (accounts.Count != request.AccountIds.Distinct().Count())
            throw new AccountingException("أحد الحسابات في القيد غير موجود في دليل الحسابات.");

        foreach (var account in accounts)
        {
            if (account.IsGroup)
                throw new AccountingException($"الحساب {account.Code} مجموعة — لا يُرحَّل عليه مباشرة.");
            if (!account.IsActive)
                throw new AccountingException($"الحساب {account.Code} غير نشط — الترحيل مرفوض.");
        }
    }

    public async Task<long> ResolveAccountIdAsync(long companyId, string systemCode, CancellationToken ct = default)
    {
        var account = await db.Accounts
            .Where(a => a.CompanyId == companyId && a.Code == systemCode && !a.IsGroup && a.IsActive)
            .Select(a => new { a.Id })
            .FirstOrDefaultAsync(ct)
            ?? throw new AccountingException($"الحساب الافتراضي {systemCode} غير مهيأ للشركة.");

        return account.Id;
    }
}
