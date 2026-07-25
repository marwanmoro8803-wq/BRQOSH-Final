using Brqosh.Domain.Accounting;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Security;

public class UserManagementService(BrqoshDbContext db)
{
    public async Task<IReadOnlyList<AppUser>> ListAsync(long companyId, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        return await db.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.CompanyId == companyId)
            .OrderBy(u => u.Username)
            .ToListAsync(ct);
    }

    public async Task<AppUser> CreateAsync(
        long companyId, string username, string password, string displayName, UserRole role,
        string? mobilePhone = null,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        if (!PasswordPolicy.IsValid(password, out var pwdError))
            throw new AccountingException(pwdError!);

        var normalized = username.Trim().ToLowerInvariant();
        if (await db.AppUsers.IgnoreQueryFilters()
                .AnyAsync(u => u.CompanyId == companyId && u.Username == normalized, ct))
            throw new AccountingException($"اسم المستخدم '{username}' مستخدم مسبقاً.");

        string? phoneE164 = null;
        if (!string.IsNullOrWhiteSpace(mobilePhone))
        {
            if (!PhoneNumberNormalizer.TryNormalizeSaudi(mobilePhone, out phoneE164, out var phoneError))
                throw new AccountingException(phoneError!);
        }

        var user = AppUser.Create(companyId, normalized, UserAuthService.HashPassword(password),
            displayName, role);
        user.SetMobilePhone(phoneE164);
        db.AppUsers.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task UpdateAsync(
        long companyId, long userId, string displayName, UserRole role, bool isActive,
        string? newPassword, string? mobilePhone = null,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        var user = await db.AppUsers.IgnoreQueryFilters()
            .SingleAsync(u => u.Id == userId && u.CompanyId == companyId, ct);
        user.UpdateProfile(displayName, role);
        if (isActive) user.Activate(); else user.Deactivate();
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (!PasswordPolicy.IsValid(newPassword, out var pwdError))
                throw new AccountingException(pwdError!);
            user.SetPasswordHash(UserAuthService.HashPassword(newPassword));
        }

        if (mobilePhone is not null)
        {
            if (string.IsNullOrWhiteSpace(mobilePhone))
                user.SetMobilePhone(null);
            else if (!PhoneNumberNormalizer.TryNormalizeSaudi(mobilePhone, out var phoneE164, out var phoneError))
                throw new AccountingException(phoneError!);
            else
                user.SetMobilePhone(phoneE164);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>إعادة تعيين كلمة مرور مستخدم — للمدير أو أداة الطوارئ بعد التحقق.</summary>
    public async Task ResetPasswordAsync(
        long companyId, string username, string newPassword, CancellationToken ct = default)
    {
        if (!PasswordPolicy.IsValid(newPassword, out var pwdError))
            throw new AccountingException(pwdError!);

        var normalized = username.Trim().ToLowerInvariant();
        var user = await db.AppUsers.IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => u.CompanyId == companyId && u.Username == normalized, ct)
            ?? throw new AccountingException($"المستخدم '{username}' غير موجود.");

        user.SetPasswordHash(UserAuthService.HashPassword(newPassword));
        user.RequirePasswordChange();
        await db.SaveChangesAsync(ct);
    }
}
