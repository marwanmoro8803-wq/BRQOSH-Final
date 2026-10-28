using System.Security.Cryptography;
using System.Text;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Security;

public class UserAuthService(BrqoshDbContext db)
{
    private const int Pbkdf2Iterations = 100_000;

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2:{Pbkdf2Iterations}:{Convert.ToHexString(salt)}:{Convert.ToHexString(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (storedHash.StartsWith("pbkdf2:", StringComparison.Ordinal))
        {
            var parts = storedHash.Split(':');
            if (parts.Length != 4) return false;
            if (!int.TryParse(parts[1], out var iterations)) return false;
            var salt = Convert.FromHexString(parts[2]);
            var expected = Convert.FromHexString(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        // ترقية من SHA-256 القديم
        var legacy = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        return string.Equals(legacy, storedHash, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<AppUser?> AuthenticateAsync(long companyId, string username, string password, string? totpCode = null)
    {
        var normalized = username.Trim().ToLowerInvariant();

        // ‏ **القفلُ يُفرَض هنا لا في النافذة.**
        // ‏كان يُفرَض في LoginWindow وحدَها وفي ذاكرة العملية — فإعادةُ التشغيل تمحوه،
        // ‏وأيُّ مسارٍ آخر يستدعي هذه الخدمة لم يكن يمرّ بقفلٍ أصلاً.
        await new PersistentLoginLockout(db).ThrowIfLockedAsync(companyId, normalized);

        var user = await db.AppUsers
            .FirstOrDefaultAsync(u => u.CompanyId == companyId
                && u.Username == normalized
                && u.IsActive);

        if (user is null || !VerifyPassword(password, user.PasswordHash))
        {
            await new LoginAuditService(db).LogAsync(companyId, normalized, false, "بيانات اعتماد غير صحيحة");
            return null;
        }

        if (user.RequiresMfa)
        {
            // ‏(S-5) **والرمزُ يُقبل مرّةً**: تُطابَق خطوتُه ثمّ تُحجز بتحديثٍ مشروطٍ في القاعدة — فرمزٌ أُعيد، أو خطوةٌ
            // ‏أقدمُ ممّا قُبل، يُرفض كرمزٍ خاطئ ويُحسب في القفل.
            var step = string.IsNullOrWhiteSpace(totpCode) ? null : TotpMfaService.MatchStep(user.MfaSecret!, totpCode);
            if (step is null || !await AcceptTotpStepAsync(user.Id, step.Value))
            {
                // ‏(ت-٦) وإخفاقُ رمز MFA يُحسب كإخفاقِ كلمة مرور — وكان لا يُحسب أصلاً،
                // ‏فمن ملك كلمةَ المرور استطاع تخمينَ الرمز بلا حدّ.
                await new LoginAuditService(db).LogAsync(companyId, normalized, false, "رمز MFA غير صحيح");
                return null;
            }
        }

        if (!user.PasswordHash.StartsWith("pbkdf2:", StringComparison.Ordinal))
        {
            user.SetPasswordHash(HashPassword(password));
            await db.SaveChangesAsync();
        }

        await new LoginAuditService(db).LogAsync(companyId, normalized, true);
        return user;
    }

    /// <summary>
    /// ‏(S-5) يحجز الخطوةَ إن كانت أحدثَ من آخر مقبولة — <b>شرطاً في جملة التحديث نفسِها</b>، فدخولان متزامنان بالرمز
    /// نفسِه لا ينجحان معاً: الثاني يجد الشرطَ باطلاً ولا يُحدّث صفّاً.
    /// </summary>
    private async Task<bool> AcceptTotpStepAsync(long userId, long step) =>
        await db.AppUsers
            .Where(u => u.Id == userId && (u.MfaLastAcceptedStep == null || u.MfaLastAcceptedStep < step))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.MfaLastAcceptedStep, step)) == 1;

    /// <summary>
    /// المديرُ الأوّل — <b>ومعه مفتاحُ استعادته يُعاد مرّةً</b> (<c>security-licensing-tenancy#7</c>) ليُعرض للمالك فيحفظه؛
    /// وعدمٌ إن كانت المنشأةُ مأهولةً فلا يُنشأ شيء.
    /// </summary>
    public async Task<string?> CreateInitialAdminAsync(long companyId, string username, string password,
        CancellationToken ct = default)
    {
        if (await db.AppUsers.AnyAsync(u => u.CompanyId == companyId, ct))
            return null;

        if (!PasswordPolicy.IsValid(password, out var error))
            throw new ArgumentException(error);

        var normalized = username.Trim().ToLowerInvariant();
        var admin = AppUser.Create(companyId, normalized, HashPassword(password),
            "مدير النظام", UserRole.Administrator, mustChangePassword: true);
        var recoveryKey = AdminRecoveryKey.IssueFor(admin);
        db.AppUsers.Add(admin);
        await db.SaveChangesAsync(ct);
        return recoveryKey;
    }

    public async Task ChangePasswordAsync(long userId, string currentPassword, string newPassword,
        CancellationToken ct = default)
    {
        if (!PasswordPolicy.IsValid(newPassword, out var error))
            throw new ArgumentException(error);

        var user = await db.AppUsers.SingleAsync(u => u.Id == userId, ct);
        if (!VerifyPassword(currentPassword, user.PasswordHash))
            throw new Domain.Accounting.AccountingException("كلمة المرور الحالية غير صحيحة.");

        user.SetPasswordHash(HashPassword(newPassword));
        await db.SaveChangesAsync(ct);
    }

    [Obsolete("استخدم CreateInitialAdminAsync من معالج الإعداد.")]
    public async Task EnsureDefaultAdminAsync(long companyId, CancellationToken ct = default)
    {
        if (await db.AppUsers.AnyAsync(u => u.CompanyId == companyId, ct))
            return;
        throw new InvalidOperationException("لا يوجد مستخدم مدير — أكمل معالج الإعداد.");
    }
}
