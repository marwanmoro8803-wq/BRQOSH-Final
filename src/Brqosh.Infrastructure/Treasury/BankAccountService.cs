using Brqosh.Infrastructure.Platform;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

/// <summary>
/// إنشاء حساب بنكي مع حساب أستاذ فرعي تلقائياً تحت 1102 (النقدية لدى البنوك).
/// </summary>
public class BankAccountService(BrqoshDbContext db)
{
    public async Task<BankAccount> CreateAsync(long companyId, string code, string nameAr, string bankName,
        string accountNumber, string iban, bool isDefault, CancellationToken ct = default)
    {
        code = code.Trim().ToUpperInvariant();

        // (CEEF-229 · م2-١٠ / D-6) ‏الفحصُ يشمل **المحذوف**.
        //
        // ‏كان `AnyAsync` وحدَه، وهو مُرشَّحٌ بالحذف الناعم — فكودُ حسابٍ محذوف يمرّ الفحصَ
        // ‏ثمّ يصطدم بالفهرس الفريد، فيُعرض نصُّ المحرّك الإنجليزيّ في واجهةٍ عربية.
        // ‏والفحصُ الآن يقول **ما الذي يحجز الكود**: قائمٌ أم محذوفٌ لا يُرى.
        var availability = await new CodeAvailabilityQuery(db)
            .CheckAsync(CodeAvailabilityScope.BankAccount, companyId, code, ct: ct);
        if (CodeAvailabilityQuery.Explain(availability, CodeAvailabilityScope.BankAccount, code) is string reason)
            throw new AccountingException(reason);

        var parent = await db.Accounts.SingleAsync(a =>
            a.CompanyId == companyId && a.Code == SaudiChartOfAccounts.SystemCodes.CashAtBanks, ct);

        var childCodes = await db.Accounts
            .Where(a => a.CompanyId == companyId && a.ParentId == parent.Id)
            .Select(a => a.Code)
            .ToListAsync(ct);

        var glCode = NextChildCode(parent.Code, childCodes);
        var glAccount = Account.Create(glCode, nameAr.Trim(), nameAr.Trim(), AccountType.Asset,
            parent.Id, isGroup: false, companyId: companyId);

        db.Accounts.Add(glAccount);
        await db.SaveChangesAsync(ct);

        if (isDefault)
        {
            var others = await db.BankAccounts
                .Where(b => b.CompanyId == companyId && b.IsDefault)
                .ToListAsync(ct);
            foreach (var o in others)
                o.ClearDefault();
        }

        var bank = BankAccount.Create(companyId, code, nameAr, bankName, accountNumber, iban, glAccount.Id, isDefault);
        db.BankAccounts.Add(bank);
        await db.SaveChangesAsync(ct);
        return bank;
    }

    public async Task UpdateAsync(long companyId, long bankAccountId, string nameAr, string bankName,
        string accountNumber, string iban, bool isDefault, CancellationToken ct = default)
    {
        var bank = await db.BankAccounts
            .SingleAsync(b => b.CompanyId == companyId && b.Id == bankAccountId, ct);

        if (isDefault)
        {
            foreach (var other in await db.BankAccounts
                         .Where(b => b.CompanyId == companyId && b.IsDefault && b.Id != bankAccountId)
                         .ToListAsync(ct))
                other.ClearDefault();
        }

        bank.Update(nameAr, bankName, accountNumber, iban, isDefault);

        var gl = await db.Accounts.SingleAsync(a => a.Id == bank.GlAccountId, ct);
        gl.Rename(nameAr.Trim(), nameAr.Trim());

        await db.SaveChangesAsync(ct);
    }

    internal static string NextChildCode(string parentCode, IReadOnlyList<string> existingChildCodes)
    {
        var maxSuffix = 0;
        foreach (var code in existingChildCodes)
        {
            if (!code.StartsWith(parentCode, StringComparison.Ordinal))
                continue;
            var suffix = code[parentCode.Length..];
            if (suffix.Length > 0 && int.TryParse(suffix, out var n) && n > maxSuffix)
                maxSuffix = n;
        }

        return parentCode + (maxSuffix + 1).ToString("D2");
    }
}
