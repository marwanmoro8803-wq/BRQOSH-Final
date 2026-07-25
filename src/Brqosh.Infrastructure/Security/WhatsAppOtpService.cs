using Brqosh.Domain.Accounting;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Security;

public sealed record OtpSendResult(string MaskedPhone, string? DevHint);

public class WhatsAppOtpService(
    BrqoshDbContext db,
    IWhatsAppNotificationChannel? channel = null)
{
    private static readonly TimeSpan OtpTtl = TimeSpan.FromMinutes(10);
    private readonly IWhatsAppNotificationChannel _channel = channel ?? new InAppOtpNotificationChannel();

    public async Task<OtpSendResult> SendRecoveryCodeAsync(
        long companyId, string username, CancellationToken ct = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var user = await db.AppUsers.IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => u.CompanyId == companyId && u.Username == normalized && u.IsActive, ct)
            ?? throw new AccountingException("المستخدم غير موجود أو غير نشط.");

        if (string.IsNullOrWhiteSpace(user.MobilePhoneE164))
            throw new AccountingException("لم يُسجَّل رقم جوال لهذا الحساب — تواصل مع مدير النظام.");

        return await SendAsync(companyId, normalized, user.MobilePhoneE164, OtpPurpose.PasswordRecovery, ct);
    }

    public async Task<OtpSendResult> SendRegistrationCodeAsync(
        long companyId, string username, string phoneInput, CancellationToken ct = default)
    {
        if (!PhoneNumberNormalizer.TryNormalizeSaudi(phoneInput, out var phone, out var phoneError))
            throw new AccountingException(phoneError!);

        var normalized = username.Trim().ToLowerInvariant();
        if (await db.AppUsers.IgnoreQueryFilters()
                .AnyAsync(u => u.CompanyId == companyId && u.Username == normalized, ct))
            throw new AccountingException($"اسم المستخدم '{username}' مستخدم مسبقاً.");

        return await SendAsync(companyId, normalized, phone, OtpPurpose.Registration, ct);
    }

    public async Task VerifyCodeAsync(
        long companyId, string username, string code, OtpPurpose purpose, CancellationToken ct = default)
    {
        var challenge = await FindActiveChallengeAsync(companyId, username, purpose, ct)
            ?? throw new AccountingException("الرمز غير صالح أو منتهي الصلاحية.");

        if (!OtpCodeGenerator.Verify(code, challenge.CodeHash))
            throw new AccountingException("الرمز غير صحيح.");
    }

    public async Task ResetPasswordAsync(
        long companyId, string username, string code, string newPassword, CancellationToken ct = default)
    {
        if (!PasswordPolicy.IsValid(newPassword, out var pwdError))
            throw new AccountingException(pwdError!);

        var normalized = username.Trim().ToLowerInvariant();
        var challenge = await FindActiveChallengeAsync(companyId, normalized, OtpPurpose.PasswordRecovery, ct)
            ?? throw new AccountingException("الرمز غير صالح أو منتهي الصلاحية.");

        if (!OtpCodeGenerator.Verify(code, challenge.CodeHash))
            throw new AccountingException("الرمز غير صحيح.");

        var user = await db.AppUsers.IgnoreQueryFilters()
            .SingleAsync(u => u.CompanyId == companyId && u.Username == normalized, ct);

        user.SetPasswordHash(UserAuthService.HashPassword(newPassword));
        user.InvalidateSessions();
        challenge.Consume();
        await db.SaveChangesAsync(ct);
    }

    public async Task<AppUser> RegisterVerifiedUserAsync(
        long companyId, string username, string displayName, string phoneInput,
        string password, string code, CancellationToken ct = default)
    {
        if (!PasswordPolicy.IsValid(password, out var pwdError))
            throw new AccountingException(pwdError!);
        if (!PhoneNumberNormalizer.TryNormalizeSaudi(phoneInput, out var phone, out var phoneError))
            throw new AccountingException(phoneError!);

        var normalized = username.Trim().ToLowerInvariant();
        var challenge = await FindActiveChallengeAsync(companyId, normalized, OtpPurpose.Registration, ct)
            ?? throw new AccountingException("الرمز غير صالح أو منتهي الصلاحية.");

        if (!string.Equals(challenge.PhoneE164, phone, StringComparison.Ordinal))
            throw new AccountingException("رقم الجوال لا يطابق الرمز المُرسَل.");
        if (!OtpCodeGenerator.Verify(code, challenge.CodeHash))
            throw new AccountingException("الرمز غير صحيح.");
        if (await db.AppUsers.IgnoreQueryFilters()
                .AnyAsync(u => u.CompanyId == companyId && u.Username == normalized, ct))
            throw new AccountingException($"اسم المستخدم '{username}' مستخدم مسبقاً.");

        var user = AppUser.Create(companyId, normalized, UserAuthService.HashPassword(password),
            displayName, UserRole.Viewer, mustChangePassword: false);
        user.SetMobilePhone(phone);
        user.Deactivate();
        db.AppUsers.Add(user);
        challenge.Consume();
        await db.SaveChangesAsync(ct);
        return user;
    }

    private async Task<OtpSendResult> SendAsync(
        long companyId, string username, string phoneE164, OtpPurpose purpose, CancellationToken ct)
    {
        var code = OtpCodeGenerator.Generate();
        var hash = OtpCodeGenerator.Hash(code);

        var stale = await db.OtpChallenges
            .Where(c => c.CompanyId == companyId
                && c.Username == username
                && c.Purpose == purpose
                && !c.IsConsumed)
            .ToListAsync(ct);
        foreach (var item in stale)
            item.Consume();

        db.OtpChallenges.Add(OtpChallenge.Create(companyId, username, phoneE164, hash, purpose, OtpTtl));
        await db.SaveChangesAsync(ct);

        var message = purpose switch
        {
            OtpPurpose.PasswordRecovery =>
                $"BRQOSH — رمز استعادة كلمة المرور\nالرمز: {code}\nصالح لمدة 10 دقائق. لا تشاركه مع أحد.",
            OtpPurpose.Registration =>
                $"BRQOSH — رمز تأكيد إنشاء الحساب\nالرمز: {code}\nصالح لمدة 10 دقائق. لا تشاركه مع أحد.",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
        };

        var send = _channel.SendOtp(phoneE164, message);
        if (!send.Success)
            throw new AccountingException($"تعذر إنشاء رمز التحقق: {send.Error}");

        return new OtpSendResult(PhoneNumberNormalizer.Mask(phoneE164), send.DevHint);
    }

    private async Task<OtpChallenge?> FindActiveChallengeAsync(
        long companyId, string username, OtpPurpose purpose, CancellationToken ct)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var challenge = await db.OtpChallenges
            .Where(c => c.CompanyId == companyId
                && c.Username == normalized
                && c.Purpose == purpose
                && !c.IsConsumed)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (challenge is null || challenge.IsExpired())
            return null;

        return challenge;
    }
}
