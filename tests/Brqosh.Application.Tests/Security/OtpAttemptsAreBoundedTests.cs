using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (م-٤ · ن-٢٨) <b>رمزُ الاستعادة كان بلا حدِّ محاولات، ويُحفَظ قبل أن يُرسَل.</b>
///
/// <para><b>العطل الأوّل — لا عدّاد:</b> ثلاثةُ مواضعِ تحقّقٍ (<c>VerifyCodeAsync</c> ·
/// <c>ResetPasswordAsync</c> · <c>RegisterVerifiedUserAsync</c>) كانت تقرأ التحدّي، تُقارن
/// الهاش، وترمي — <b>بلا أن تمسّ الصفّ</b>. فستّةُ أرقامٍ تُخمَّن بلا حدٍّ طوالَ عشر دقائق.</para>
///
/// <para><b>والعطل الثاني — الحفظُ قبل الإرسال:</b> كان الصفُّ يُلتزم ثمّ تُرسَل الرسالة،
/// والرميةُ عند الفشل <b>لا تُرجع ما التُزم</b>. وأسوأُ من بقاء اليتيم أنّ <b>التحدّي
/// السابق استُهلك معه</b> — فمحاولةٌ فاشلةٌ تحرق رمزاً صالحاً بيد المستخدم وتضع مكانَه
/// رمزاً لا يعرفه أحد.</para>
///
/// <para><b>وليس الفشلُ حالةً نادرة بل الافتراض:</b> بلا اعتمادات واتساب في الإنتاج يُعيد
/// <c>WhatsAppNotificationChannelFactory</c> قناةً تُرجع <c>Success=false</c> دائماً —
/// <b>فكلُّ ضغطةِ «نسيت كلمة المرور» كانت تُنتج يتيماً حيّاً ثمّ ترمي</b>.</para>
///
/// <para><b>وفخّان في الكناريّ نفسِه، رُصدا قبل كتابته:</b> (١) <c>ResetPasswordAsync</c>
/// يفحص <b>كلمةَ المرور أوّلاً</b> — فمحاولاتُ التخمين يجب أن تحمل كلمةً مطابقةً للسياسة
/// وإلّا رمى الفحصُ الأوّلُ ولم يُعدّ شيء، <b>ومرّ الاختبارُ على الكود المُصلَح ظانّاً أنّه
/// معطوب</b>. (٢) ومهلةُ إعادة الإرسال تمنع الإرسالَ الثاني من بلوغ القناة أصلاً، فيُقدَّم
/// طابعُ التحدّي الأوّل ليُقاس ما قُصد قياسُه.</para>
/// </summary>
public sealed class OtpAttemptsAreBoundedTests : IDisposable
{
    private const string User = "recoverme";
    private const string ValidNewPassword = "NewPass12!!!";

    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private readonly StubChannel _channel = new();
    private long _companyId;

    public OtpAttemptsAreBoundedTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── حدُّ المحاولات ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ:</b> خمسُ محاولاتٍ خاطئةٍ تحرق التحدّي — <b>والرمزُ الصحيحُ
    /// بعدها لا يعمل</b>.
    ///
    /// <para>والشقُّ الثاني هو الذي يفرّق بين عدّادٍ يُحصي وعدّادٍ يُغلق: عدٌّ بلا استهلاكٍ
    /// يُبطئ المهاجمَ ولا يوقفه.</para>
    /// </summary>
    [Fact]
    public async Task FiveWrongCodes_BurnTheChallenge_SoEvenTheRightOneStopsWorking()
    {
        await SetupAsync();
        var service = new WhatsAppOtpService(_db, _channel);
        await service.SendRecoveryCodeAsync(_companyId, User);
        var right = _channel.LastCode!;

        for (var i = 0; i < OtpChallenge.MaxFailedAttempts; i++)
        {
            // ‏كلمةُ مرورٍ صالحةٌ عمداً — وإلّا رمى فحصُ السياسة قبل فحص الرمز فلا يُعدّ شيء.
            await Assert.ThrowsAsync<AccountingException>(() =>
                service.ResetPasswordAsync(_companyId, User, "000000", ValidNewPassword));
        }

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            service.ResetPasswordAsync(_companyId, User, right, ValidNewPassword));

        Assert.Contains("غير صالح", ex.Message);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وهو ألزمُ من الحدّ:</b> الرمزُ الصحيحُ من أوّل مرّةٍ يعمل.
    ///
    /// <para>فحدٌّ يرفض الجميعَ يُغلق الثغرةَ ويمنع الاستعادةَ تماماً، ولا يُميّز بينهما
    /// إلّا هذا.</para>
    /// </summary>
    [Fact]
    public async Task TheRightCodeOnTheFirstTry_StillResetsThePassword()
    {
        await SetupAsync();
        var service = new WhatsAppOtpService(_db, _channel);
        await service.SendRecoveryCodeAsync(_companyId, User);

        await service.ResetPasswordAsync(_companyId, User, _channel.LastCode!, ValidNewPassword);

        var challenge = await _db.OtpChallenges.SingleAsync();
        Assert.True(challenge.IsConsumed);
        Assert.Equal(0, challenge.FailedAttempts);
    }

    /// <summary><b>وأربعُ محاولاتٍ لا تحرق</b> — فالحدُّ خمسٌ لا أقلّ.</summary>
    [Fact]
    public async Task FourWrongCodes_LeaveTheChallengeUsable()
    {
        await SetupAsync();
        var service = new WhatsAppOtpService(_db, _channel);
        await service.SendRecoveryCodeAsync(_companyId, User);
        var right = _channel.LastCode!;

        for (var i = 0; i < OtpChallenge.MaxFailedAttempts - 1; i++)
            await Assert.ThrowsAsync<AccountingException>(() =>
                service.ResetPasswordAsync(_companyId, User, "000000", ValidNewPassword));

        await service.ResetPasswordAsync(_companyId, User, right, ValidNewPassword);

        Assert.True((await _db.OtpChallenges.SingleAsync()).IsConsumed);
    }

    // ── الإرسالُ قبل الحفظ ────────────────────────────────────────────────────

    /// <summary>
    /// <b>إرسالٌ فاشلٌ لا يترك تحدّياً وراءه</b> — وكان يترك يتيماً حيّاً على كلّ تنصيبٍ
    /// بلا اعتمادات واتساب.
    /// </summary>
    [Fact]
    public async Task AFailedSend_WritesNoChallengeAtAll()
    {
        await SetupAsync();
        _channel.Fail = true;
        var service = new WhatsAppOtpService(_db, _channel);

        await Assert.ThrowsAsync<AccountingException>(() =>
            service.SendRecoveryCodeAsync(_companyId, User));

        Assert.Empty(await _db.OtpChallenges.ToListAsync());
    }

    /// <summary>
    /// <b>والأحدّ:</b> إرسالٌ فاشلٌ <b>لا يحرق التحدّيَ القائم</b> — والرمزُ الذي بيد
    /// المستخدم يبقى صالحاً.
    ///
    /// <para><b>ولمَ لا يُقاس بعدّ الأحياء:</b> على الكود المعطوب يُستهلك القديمُ ويُضاف
    /// يتيمٌ حيّ — <b>فعددُ غيرِ المستهلَكين يبقى واحداً</b> ويمرّ توكيدٌ ساذج. فيُقاس
    /// المجموعُ الكلّيُّ وحالةُ الصفّ الأصليّ <b>وأنّ رمزَه ما زال يعمل</b>.</para>
    /// </summary>
    [Fact]
    public async Task AFailedSend_DoesNotBurnTheCodeTheUserAlreadyHas()
    {
        await SetupAsync();
        var service = new WhatsAppOtpService(_db, _channel);
        await service.SendRecoveryCodeAsync(_companyId, User);
        var right = _channel.LastCode!;

        // ‏يُقدَّم الطابعُ لتجاوز مهلة إعادة الإرسال — وإلّا رُفض الطلبُ قبل بلوغ القناة،
        // ‏فلا يُقاس ما جاء الاختبارُ لقياسه.
        await BackdateChallengesAsync(TimeSpan.FromMinutes(2));

        _channel.Fail = true;
        await Assert.ThrowsAsync<AccountingException>(() =>
            service.SendRecoveryCodeAsync(_companyId, User));

        Assert.Single(await _db.OtpChallenges.IgnoreQueryFilters().ToListAsync());
        Assert.False((await _db.OtpChallenges.SingleAsync()).IsConsumed);

        // ‏والدليلُ القاطع: الرمزُ القديمُ ما زال يفتح.
        _channel.Fail = false;
        await service.ResetPasswordAsync(_companyId, User, right, ValidNewPassword);
    }

    // ── مهلةُ إعادة الإرسال ───────────────────────────────────────────────────

    /// <summary>
    /// <b>إرسالٌ ثانٍ فوريٌّ يُرفض</b> — فبلا مهلةٍ يبقى مجموعُ التخمينات بلا سقف:
    /// أرسِل ⟵ خمّن خمساً ⟵ أرسِل.
    /// </summary>
    [Fact]
    public async Task AnImmediateResend_IsRefused()
    {
        await SetupAsync();
        var service = new WhatsAppOtpService(_db, _channel);
        await service.SendRecoveryCodeAsync(_companyId, User);

        var ex = await Assert.ThrowsAsync<AccountingException>(() =>
            service.SendRecoveryCodeAsync(_companyId, User));

        Assert.Contains("انتظر", ex.Message);
    }

    /// <summary><b>شاهدُ ضبطٍ موجب:</b> وبعد انقضاء المهلة يُقبل — فلا تُعطَّل الاستعادة.</summary>
    [Fact]
    public async Task AResendAfterTheCooldown_IsAllowed()
    {
        await SetupAsync();
        var service = new WhatsAppOtpService(_db, _channel);
        await service.SendRecoveryCodeAsync(_companyId, User);

        await BackdateChallengesAsync(TimeSpan.FromMinutes(2));

        await service.SendRecoveryCodeAsync(_companyId, User);

        var live = await _db.OtpChallenges.Where(c => !c.IsConsumed).ToListAsync();
        Assert.Single(live);
    }

    // ── مساعدات ──────────────────────────────────────────────────────────────

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة OTP", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();

        var user = AppUser.Create(_companyId, User, UserAuthService.HashPassword("OldPass12!!"),
            "مستخدم", UserRole.Accountant);
        user.SetMobilePhone("+966501234567");
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync();
    }

    /// <summary>يُقدّم طابعَ إنشاء التحدّيات — فالمهلةُ زمنٌ حقيقيٌّ لا يُنتظَر في اختبار.</summary>
    private async Task BackdateChallengesAsync(TimeSpan by)
    {
        var when = DateTime.UtcNow - by;
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE otp_challenges SET CreatedAtUtc = {0}",
            when.ToString("yyyy-MM-dd HH:mm:ss.fffffff"));
        _db.ChangeTracker.Clear();
    }

    private sealed class StubChannel : IWhatsAppNotificationChannel
    {
        public string? LastCode { get; private set; }
        public bool Fail { get; set; }

        public WhatsAppSendResult SendOtp(string phoneE164, string messageAr)
        {
            if (Fail) return new WhatsAppSendResult(false, null, "القناة غير مهيّأة");

            const string marker = "الرمز:";
            var idx = messageAr.IndexOf(marker, StringComparison.Ordinal);
            LastCode = idx >= 0
                ? messageAr[(idx + marker.Length)..].Trim().Split(' ', '\n', '\r')[0]
                : null;
            return new WhatsAppSendResult(true, LastCode, null);
        }
    }
}
