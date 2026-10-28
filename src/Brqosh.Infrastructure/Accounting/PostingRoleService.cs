using Brqosh.Application.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Accounting;

/// <summary>
/// ‏حساباتُ أدوار الترحيل لكلّ منشأة. ‏خدماتُ الترحيل تسأل <see cref="ResolveAsync"/> بالرمز الافتراضيّ للدور،
/// ‏فتأخذ حسابَ المنشأة إن عُيِّن، وإلّا الرمزَ الافتراضيَّ نفسَه — فالسلوكُ لا يتغيّر ما لم يُعدَّل الإعداد.
/// </summary>
public class PostingRoleService(BrqoshDbContext db) : IPostingRoleService
{
    private static readonly Dictionary<string, string> RoleByDefaultCode =
        PostingRoleAccount.Defaults.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    /// <summary>‏رمزُ الحساب الفعّال للدور الذي رمزُه الافتراضيّ <paramref name="defaultCode"/>.</summary>
    public static async Task<string> ResolveAsync(BrqoshDbContext db, long companyId, string defaultCode, CancellationToken ct = default)
    {
        if (!RoleByDefaultCode.TryGetValue(defaultCode, out var role))
            return defaultCode;
        var mapped = await db.PostingRoleAccounts.AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.Role == role)
            .Select(r => r.AccountCode)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(mapped) ? defaultCode : mapped;
    }

    /// <summary>‏يُعبّئ الأدوارَ الناقصة بأرقامها الافتراضيّة (عند إنشاء دليل المنشأة).</summary>
    public static async Task SeedDefaultsAsync(BrqoshDbContext db, long companyId, CancellationToken ct = default)
    {
        var existing = await db.PostingRoleAccounts.Where(r => r.CompanyId == companyId).Select(r => r.Role).ToListAsync(ct);
        foreach (var (role, code) in PostingRoleAccount.Defaults)
            if (!existing.Contains(role))
                db.PostingRoleAccounts.Add(PostingRoleAccount.Create(companyId, role, code));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PostingRoleAccountDto>> ListAsync(long companyId, CancellationToken ct = default)
    {
        var rows = await db.PostingRoleAccounts.AsNoTracking().Where(r => r.CompanyId == companyId)
            .ToDictionaryAsync(r => r.Role, r => r.AccountCode, ct);
        var names = await db.Accounts.AsNoTracking().Where(a => a.CompanyId == companyId)
            .Select(a => new { a.Code, a.NameAr }).ToDictionaryAsync(a => a.Code, a => a.NameAr, ct);
        return PostingRoleAccount.Defaults
            .OrderBy(kv => kv.Value, StringComparer.Ordinal)
            .Select(kv =>
            {
                var code = rows.TryGetValue(kv.Key, out var c) && !string.IsNullOrWhiteSpace(c) ? c : kv.Value;
                return new PostingRoleAccountDto(kv.Key, kv.Value, code, names.GetValueOrDefault(code), code != kv.Value);
            })
            .ToList();
    }

    public async Task SetAsync(long companyId, string role, string accountCode, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        if (!PostingRoleAccount.Defaults.ContainsKey(role))
            throw new AccountingException($"دورُ الترحيل '{role}' غير معروف.");
        var code = accountCode?.Trim() ?? "";
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code, ct)
            ?? throw new AccountingException($"الحساب {code} غير موجود في دليل حسابات المنشأة.");
        if (account.IsGroup || !account.IsActive)
            throw new AccountingException($"الحساب {code} تجميعيٌّ أو موقوف — اختر حساباً تفصيليّاً نشطاً.");

        var row = await db.PostingRoleAccounts.SingleOrDefaultAsync(r => r.CompanyId == companyId && r.Role == role, ct);
        if (row is null)
            db.PostingRoleAccounts.Add(PostingRoleAccount.Create(companyId, role, code));
        else
            row.Assign(code);
        await db.SaveChangesAsync(ct);
        await new AuditService(db).LogAsync(companyId, SecurityContext.CurrentUsername ?? "", "تعديل حساب دور ترحيل",
            "PostingRoleAccount", role, code);
    }

    public async Task ResetAsync(long companyId, string role, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        if (!PostingRoleAccount.Defaults.TryGetValue(role, out var code))
            throw new AccountingException($"دورُ الترحيل '{role}' غير معروف.");
        var row = await db.PostingRoleAccounts.SingleOrDefaultAsync(r => r.CompanyId == companyId && r.Role == role, ct);
        if (row is null)
            return;
        row.Assign(code);   // ‏الافتراضيّ قد لا يكون مُنشأً بعد في الدليل — يُنشأ عند أوّل ترحيلٍ كما كان
        await db.SaveChangesAsync(ct);
        await new AuditService(db).LogAsync(companyId, SecurityContext.CurrentUsername ?? "", "إعادة حساب دور ترحيل للافتراضي",
            "PostingRoleAccount", role, code);
    }
}
