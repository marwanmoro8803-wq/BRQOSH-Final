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

    /// <summary>
    /// (م-٤ · ن-٢٨) أقلُّ فاصلٍ بين إرسالين — <b>يحدّ مجموعَ التخمينات</b>.
    /// ودقيقةٌ واحدة: تكسر حلقةَ «أرسِل ⟵ خمّن خمساً ⟵ أرسِل»، ولا تُعطّل مستخدماً لم
    /// تصله الرسالة — إذ يبقى رمزُه السابقُ صالحاً عشرَ دقائق.
    /// </summary>
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);
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

        await EnsureCodeMatchesAsync(challenge, code, ct);
    }

    public async Task ResetPasswordAsync(
        long companyId, string username, string code, string newPassword, CancellationToken ct = default)
    {
        if (!PasswordPolicy.IsValid(newPassword, out var pwdError))
            throw new AccountingException(pwdError!);

        var normalized = username.Trim().ToLowerInvariant();
        var challenge = await FindActiveChallengeAsync(companyId, normalized, OtpPurpose.PasswordRecovery, ct)
            ?? throw new AccountingException("الرمز غير صالح أو منتهي الصلاحية.");

        await EnsureCodeMatchesAsync(challenge, code, ct);

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
        await EnsureCodeMatchesAsync(challenge, code, ct);
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

    /// <summary>
    /// (م-٤ · ن-٢٨) <b>المطابقةُ تُعدّ — وكانت ترمي بلا أن تمسّ الصفّ.</b>
    ///
    /// <para><b>العطل:</b> ثلاثةُ مواضعِ تحقّقٍ — <c>VerifyCodeAsync</c> و<c>ResetPasswordAsync</c>
    /// و<c>RegisterVerifiedUserAsync</c> — كتبت جميعُها السطرَ نفسَه: تقرأ التحدّي، تُقارن
    /// الهاش، وترمي. <b>ولا شيءَ يُسجَّل</b>. فرمزٌ من ستّة أرقامٍ يُخمَّن بلا حدٍّ طوالَ
    /// عشر دقائق.</para>
    ///
    /// <para><b>وثلاثُ نسخٍ من شرطِ أمانٍ تفترق في أوّل تعديل</b> — فجُمعت هنا. والرسالةُ
    /// تفرّق بين «غير صحيح» و«نفدت المحاولات» <b>لأنّ الفرق يعني للمستخدم شيئاً</b>:
    /// الأولى يُعيد المحاولة، والثانية يطلب رمزاً جديداً.</para>
    /// </summary>
    private async Task EnsureCodeMatchesAsync(OtpChallenge challenge, string code, CancellationToken ct)
    {
        if (OtpCodeGenerator.Verify(code, challenge.CodeHash)) return;

        // ‏**ويُلتزم قبل الرمي** — فرميةٌ قبل الحفظ تترك العدّادَ في المتعقّب وحدَه،
        // ‏والسياقُ يُغلق مع الحوار، **فلا يُعدّ شيء**. وهو عينُ نمطِ ن-٠٥.
        challenge.RecordFailedAttempt();
        await db.SaveChangesAsync(ct);

        throw new AccountingException(challenge.IsConsumed
            ? "‏تجاوزتَ عددَ المحاولات المسموح — اطلب رمزاً جديداً."
            : "الرمز غير صحيح.");
    }

    private async Task<OtpSendResult> SendAsync(
        long companyId, string username, string phoneE164, OtpPurpose purpose, CancellationToken ct)
    {
        var stale = await db.OtpChallenges
            .Where(c => c.CompanyId == companyId
                && c.Username == username
                && c.Purpose == purpose
                && !c.IsConsumed)
            .ToListAsync(ct);

        // ‏(م-٤ · ن-٢٨) ‏**مهلةُ إعادةِ إرسال — تحدّ مجموعَ التخمينات لا تخميناتِ تحدٍّ واحد.**
        //
        // ‏عدّادُ المحاولات يُغلق التحدّيَ بعد خمس، **ولا يمنع طلبَ سادسٍ فوراً**:
        // ‏أرسِل ⟵ خمّن خمساً ⟵ أرسِل. فبلا مهلةٍ يبقى مجموعُ التخمينات بلا سقف، وتُدفع
        // ‏رسالةٌ مفوترةٌ ويُوقَظ هاتفُ صاحب الحساب في كلّ دورة.
        var newest = stale
            .Where(c => !c.IsExpired())
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefault();

        if (newest is not null)
        {
            var waited = DateTime.UtcNow - newest.CreatedAtUtc;
            if (waited < ResendCooldown)
                throw new AccountingException(
                    $"‏أُرسل رمزٌ قبل قليل — انتظر {Math.Ceiling((ResendCooldown - waited).TotalSeconds):N0} ثانيةً "
                    + "قبل طلب رمزٍ جديد، والرمزُ السابق ما زال صالحاً.");
        }

        var code = OtpCodeGenerator.Generate();
        var hash = OtpCodeGenerator.Hash(code);

        var message = purpose switch
        {
            OtpPurpose.PasswordRecovery =>
                $"BRQOSH — رمز استعادة كلمة المرور\nالرمز: {code}\nصالح لمدة 10 دقائق. لا تشاركه مع أحد.",
            OtpPurpose.Registration =>
                $"BRQOSH — رمز تأكيد إنشاء الحساب\nالرمز: {code}\nصالح لمدة 10 دقائق. لا تشاركه مع أحد.",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose)),
        };

        // ‏(م-٤ · ن-٢٨) ‏**يُرسَل أوّلاً، ثمّ يُكتب — وكان العكس.**
        //
        // ‏كان الصفُّ يُلتزم ثمّ يُرسَل، والرميةُ عند فشل الإرسال **لا تُرجع ما التُزم**.
        // ‏فيبقى تحدٍّ حيٌّ لرسالةٍ لم تصل، **وقد استُهلك التحدّي السابقُ معه** — أي أنّ
        // ‏محاولةً فاشلةً تحرق رمزاً صالحاً بيد المستخدم وتضع مكانه رمزاً لا يعرفه.
        //
        // ‏**وليس هذا حالةً نادرة بل الافتراض:** بلا اعتمادات واتساب في الإنتاج يُعيد
        // ‏`WhatsAppNotificationChannelFactory` قناةً تُرجع `Success=false` دائماً — فكلُّ
        // ‏ضغطةِ «نسيت كلمة المرور» كانت تُنتج تحدّياً يتيماً حيّاً ثمّ ترمي.
        var send = _channel.SendOtp(phoneE164, message);
        if (!send.Success)
            throw new AccountingException($"تعذر إنشاء رمز التحقق: {send.Error}");

        foreach (var item in stale)
            item.Consume();

        db.OtpChallenges.Add(OtpChallenge.Create(companyId, username, phoneE164, hash, purpose, OtpTtl));
        await db.SaveChangesAsync(ct);

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
