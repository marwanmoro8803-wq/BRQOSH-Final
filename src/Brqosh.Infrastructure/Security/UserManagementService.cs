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

        return await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            db.AppUsers.Add(user);
            await db.SaveChangesAsync(token);
            await AuditAsync(companyId, "إنشاء مستخدم", user, AppUser.RoleLabel(role), token);
            return user;
        }, ct);
    }

    /// <summary>
    /// ‏(جاهزيّةُ الإطلاق · ح-٥٠ · <c>security-licensing-tenancy#4</c> + <c>#8</c>) <b>تغييرُ الصلاحيّة
    /// يُنهي الجلسةَ ويُكتب في السلسلة.</b>
    ///
    /// <para>‏<b>والآليّةُ كانت قائمةً ولا يستدعيها أحد</b>: <see cref="AppUser.InvalidateSessions"/> يزيد
    /// <c>SessionVersion</c> وسطحُ المكتب يقرؤه في مؤقّت الخمول فيُعيد المستخدمَ إلى الدخول. <b>ومستدعيه
    /// الوحيدُ كان استردادَ كلمة المرور بالواتساب</b> — فموظّفٌ يُعطَّل أو يُنزَل من «مدير» يبقى يعمل بصلاحيّته
    /// القديمة حتى يخرج بنفسه، <b>ولا أثرَ لذلك في سجلّ التدقيق</b>.</para>
    ///
    /// <para>‏<b>والإنهاءُ لتغيير الصلاحيّة لا لكلّ حفظ</b> (الدورُ · التعطيلُ · كلمةُ المرور) — وإلّا خرج
    /// الموظّفُ كلّما صُحّح اسمُه. <b>والتفعيلُ يُكتب ولا يُنهي</b>: المعطَّلُ لا جلسةَ له أصلاً. <b>والسجلُّ
    /// لا يحمل السرّ</b>: لا كلمةَ المرور ولا بصمتَها.</para>
    /// </summary>
    public async Task UpdateAsync(
        long companyId, long userId, string displayName, UserRole role, bool isActive,
        string? newPassword, string? mobilePhone = null,
        CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var user = await db.AppUsers.IgnoreQueryFilters()
                .SingleAsync(u => u.Id == userId && u.CompanyId == companyId, token);
            var roleBefore = user.Role;
            var activeBefore = user.IsActive;

            user.UpdateProfile(displayName, role);
            if (isActive) user.Activate(); else user.Deactivate();

            var passwordSet = false;
            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (!PasswordPolicy.IsValid(newPassword, out var pwdError))
                    throw new AccountingException(pwdError!);
                user.SetPasswordHash(UserAuthService.HashPassword(newPassword));
                passwordSet = true;
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

            var roleChanged = roleBefore != user.Role;
            var deactivated = activeBefore && !user.IsActive;
            var reactivated = !activeBefore && user.IsActive;

            if (roleChanged || deactivated || passwordSet)
                user.InvalidateSessions();

            await db.SaveChangesAsync(token);

            if (roleChanged)
                await AuditAsync(companyId, "تغيير دور مستخدم", user,
                    $"{AppUser.RoleLabel(roleBefore)} ⟵ {AppUser.RoleLabel(user.Role)}", token);
            if (deactivated)
                await AuditAsync(companyId, "تعطيل مستخدم", user, "", token);
            if (reactivated)
                await AuditAsync(companyId, "تفعيل مستخدم", user, "", token);
            if (passwordSet)
                await AuditAsync(companyId, "تعيين كلمة مرور مستخدم", user, "بيد مدير النظام", token);

            return true;
        }, ct);
    }

    /// <summary>إعادة تعيين كلمة مرور مستخدم — للمدير أو أداة الطوارئ بعد التحقق.</summary>
    public async Task ResetPasswordAsync(
        long companyId, string username, string newPassword, CancellationToken ct = default)
    {
        // (CEEF-229 · م2-١، S-7) كانت الدالّة العامّة **الوحيدة في هذا الصنف** بلا حارس،
        // وأخواتها الثلاث (List · Create · Update) تفتتح بـ EnsureAdministrator. وهي تُعيد
        // تعيين كلمة مرور **أيّ** مستخدم بمن فيهم مدير النظام، بلا كلمة مرورٍ حالية ولا رمز —
        // أي بدائيّةُ تصعيد امتيازٍ صافية لمن بلغها. ولم يكن لها مستدعٍ في المستودع (شاشة
        // نسيان كلمة المرور تذهب إلى مسار OTP المحروس)، فكانت خطراً كامناً لا مستغَلّاً؛
        // والتعليق فوقها «للمدير أو أداة الطوارئ بعد التحقق» يُقرّ بأن الحماية مُوكَلةٌ إلى
        // انضباط المستدعي — وحارسٌ يعتمد على انضباط كل مستدعٍ ليس حارساً.
        SecurityContext.EnsureAdministrator();

        if (!PasswordPolicy.IsValid(newPassword, out var pwdError))
            throw new AccountingException(pwdError!);

        var normalized = username.Trim().ToLowerInvariant();

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var user = await db.AppUsers.IgnoreQueryFilters()
                .SingleOrDefaultAsync(u => u.CompanyId == companyId && u.Username == normalized, token)
                ?? throw new AccountingException($"المستخدم '{username}' غير موجود.");

            user.SetPasswordHash(UserAuthService.HashPassword(newPassword));
            user.RequirePasswordChange();
            // ‏(ح-٥٠) ‏**ومن أُعيدت كلمةُ مروره لا تبقى جلستُه القديمة** — فمن بلغها قبل الإعادة لا يستمرّ بها.
            user.InvalidateSessions();
            await db.SaveChangesAsync(token);

            await AuditAsync(companyId, "إعادة تعيين كلمة مرور مستخدم", user, "بيد مدير النظام", token);
            return true;
        }, ct);
    }

    /// <summary>
    /// ‏سطرُ تدقيقٍ في السلسلة — <b>والمنفِّذُ من الجلسة</b> لا من المستدعي، <b>والمفتاحُ اسمُ المستخدم</b>
    /// المتأثِّر. <b>ولا سرَّ في التفاصيل</b>.
    /// </summary>
    private Task AuditAsync(long companyId, string action, AppUser user, string details, CancellationToken ct) =>
        new AuditService(db).LogAsync(companyId, SecurityContext.ResolveUsername(), action,
            "AppUser", user.Username, details, ct: ct);
}
