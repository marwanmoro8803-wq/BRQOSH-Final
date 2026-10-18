namespace Brqosh.Domain.Security;

public enum UserRole
{
    Administrator = 0,
    Accountant = 1,
    Cashier = 2,
    Viewer = 3,
}

/// <summary>مستخدم النظام — كلمة المرور مُخزَّنة كهاش SHA-256.</summary>
public class AppUser : ISoftDeletable
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public string Username { get; private set; }
    public string PasswordHash { get; private set; }
    public string DisplayNameAr { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool MustChangePasswordOnNextLogin { get; private set; }
    public string? MobilePhoneE164 { get; private set; }
    public string? MfaSecret { get; private set; }

    /// <summary>
    /// ‏(S-5) <b>آخرُ خطوة TOTP قُبلت</b> — فلا يُقبل رمزُها ولا ما قبلها ثانيةً (RFC 6238 §5.2). تُكتب بتحديثٍ مشروطٍ
    /// في <c>UserAuthService</c> لا عبر هذا الكائن، فلا يقبل دخولان متزامنان الرمزَ نفسَه.
    /// </summary>
    public long? MfaLastAcceptedStep { get; private set; }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · <c>security-licensing-tenancy#7</c> · قرارُ المالك 2026-10-18) <b>تجزئةُ مفتاح الاستعادة</b> —
    /// ‏يُعرض المفتاحُ مرّةً ولا يُخزَّن إلّا مجزَّأً كأيّ كلمة مرور؛ وعدمٌ = لا استعادةَ لهذا المستخدم.
    /// </summary>
    public string? RecoveryKeyHash { get; private set; }
    public int SessionVersion { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private AppUser()
    {
        Username = null!;
        PasswordHash = null!;
        DisplayNameAr = null!;
    }

    private AppUser(long companyId, string username, string passwordHash, string displayName, UserRole role)
    {
        CompanyId = companyId;
        Username = username;
        PasswordHash = passwordHash;
        DisplayNameAr = displayName;
        Role = role;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static AppUser Create(long companyId, string username, string passwordHash,
        string displayName, UserRole role = UserRole.Accountant, bool mustChangePassword = false)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("اسم المستخدم مطلوب.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("كلمة المرور مطلوبة.");

        return new AppUser(companyId, username.Trim().ToLowerInvariant(), passwordHash,
            displayName.Trim(), role) { MustChangePasswordOnNextLogin = mustChangePassword };
    }

    public void Deactivate()
    {
        if (DeletedAtUtc is not null) return;
        DeletedAtUtc = DateTime.UtcNow;
        IsActive = false;
    }

    public void Activate()
    {
        DeletedAtUtc = null;
        IsActive = true;
    }

    public void ClearMustChangePassword() => MustChangePasswordOnNextLogin = false;

    public void RequirePasswordChange() => MustChangePasswordOnNextLogin = true;

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("كلمة المرور مطلوبة.");
        PasswordHash = passwordHash;
        MustChangePasswordOnNextLogin = false;
    }

    public void UpdateProfile(string displayName, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("الاسم مطلوب.");
        DisplayNameAr = displayName.Trim();
        Role = role;
    }

    public void SetMobilePhone(string? phoneE164) =>
        MobilePhoneE164 = string.IsNullOrWhiteSpace(phoneE164) ? null : phoneE164.Trim();

    public void SetMfaSecret(string? secretBase32)
    {
        MfaSecret = string.IsNullOrWhiteSpace(secretBase32) ? null : secretBase32.Trim();
        // ‏سرٌّ جديدٌ يبدأ عدَّه من جديد — خطواتُ السرّ القديم لا تخصّه.
        MfaLastAcceptedStep = null;
    }

    public bool RequiresMfa => !string.IsNullOrWhiteSpace(MfaSecret);

    /// <summary>مفتاحُ استعادةٍ جديدٌ يُبطل ما قبله — يُكتب مجزَّأً.</summary>
    public void SetRecoveryKeyHash(string recoveryKeyHash)
    {
        if (string.IsNullOrWhiteSpace(recoveryKeyHash))
            throw new ArgumentException("تجزئة مفتاح الاستعادة مطلوبة.");
        RecoveryKeyHash = recoveryKeyHash;
    }

    public void InvalidateSessions() => SessionVersion++;

    public static string RoleLabel(UserRole role) => role switch
    {
        UserRole.Administrator => "مدير النظام",
        UserRole.Accountant => "محاسب",
        UserRole.Cashier => "كاشير",
        UserRole.Viewer => "عرض فقط",
        _ => role.ToString(),
    };
}
