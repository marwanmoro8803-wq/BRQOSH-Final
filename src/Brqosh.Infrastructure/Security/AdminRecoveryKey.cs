using System.Security.Cryptography;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Security;

/// <summary>
/// <b>مفتاحُ استعادة المدير.</b>
///
/// <para><b>ما كان:</b> المديرُ يُنشأ بلا هاتف، والقناةُ في الإنتاج بلا WhatsApp مُهيّأٍ غيرُ متاحة — <b>فكلمةُ مرورٍ منسيّةٌ إغلاقٌ
/// تامٌّ للدفاتر</b>، ولا طريقَ إلّا أداةٌ تفتح القاعدةَ من خارج البرنامج. واختار المالكُ من أربع طرقٍ مفتاحاً يُعرض مرّةً.</para>
///
/// <para><b>والمفتاحُ كلمةُ مرورٍ ثانيةٌ قويّة</b>: ٢٥ حرفاً من أبجديّةٍ بلا حروفٍ ملتبسة (≈١٢٢ بت)، يُخزَّن مجزَّأً بـPBKDF2 كأيّ
/// كلمة مرور، <b>ويُحرق عند استعماله</b> ويُصدَر بدلُه. ومحاولاتُه تمرّ بالقفل نفسِه وسجلِّ الدخول نفسِه — فلا يصير بابُ
/// الاستعادة بابَ تخمينٍ بلا حدّ.</para>
/// </summary>
public class AdminRecoveryKey(BrqoshDbContext db)
{
    /// <summary>أبجديّةُ المفتاح — بلا 0/O ولا 1/I/L ولا U، فيُنقل بالخطّ أو الهاتف بلا لبس.</summary>
    private const string Alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";
    private const int Groups = 5;
    private const int GroupLength = 5;

    /// <summary>مفتاحٌ جديد بصيغة <c>XXXXX-XXXXX-XXXXX-XXXXX-XXXXX</c>.</summary>
    public static string NewKey()
    {
        var chars = new char[Groups * GroupLength];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return string.Join('-', Enumerable.Range(0, Groups).Select(g => new string(chars, g * GroupLength, GroupLength)));
    }

    /// <summary>الصيغةُ المعياريّة للمقارنة — حروفٌ كبيرةٌ بلا شرطاتٍ ولا مسافات؛ فالمنقولُ باليد يُقبل كما كُتب.</summary>
    public static string Normalize(string key) =>
        new(key.Where(c => !char.IsWhiteSpace(c) && c != '-').Select(char.ToUpperInvariant).ToArray());

    /// <summary>يُصدر مفتاحاً جديداً للمستخدم ويُبطل ما قبله — ويُعاد نصّاً مرّةً واحدة.</summary>
    public static string IssueFor(AppUser user)
    {
        var key = NewKey();
        user.SetRecoveryKeyHash(UserAuthService.HashPassword(Normalize(key)));
        return key;
    }

    /// <summary>
    /// مديرٌ داخلٌ يُصدر مفتاحاً جديداً لنفسه — للتركيبات التي سبقت المفتاح، أو لمفتاحٍ يُظنّ أنّه انكشف.
    /// </summary>
    public async Task<string> ReissueAsync(long userId, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        var user = await db.AppUsers.SingleAsync(u => u.Id == userId && u.IsActive, ct);
        if (user.Role != UserRole.Administrator)
            throw new AccountingException("مفتاح الاستعادة للمدير وحده.");

        var key = IssueFor(user);
        await db.SaveChangesAsync(ct);
        return key;
    }

    /// <summary>
    /// <b>استعادةُ الدخول بالمفتاح:</b> كلمةُ مرورٍ جديدةٌ للمدير، وتُبطَل جلساتُه، <b>ويُحرق المفتاحُ ويُعاد بدلُه</b>.
    /// والخطأُ يُحسب في القفل ويُسجَّل كإخفاق دخول — ولا يقول أيُّهما أخطأ: الاسمُ أم المفتاح.
    /// </summary>
    public async Task<string> RecoverAsync(long companyId, string username, string recoveryKey, string newPassword,
        CancellationToken ct = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        await new PersistentLoginLockout(db).ThrowIfLockedAsync(companyId, normalized);

        if (!PasswordPolicy.IsValid(newPassword, out var policyError))
            throw new ArgumentException(policyError);

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.CompanyId == companyId
            && u.Username == normalized && u.IsActive && u.Role == UserRole.Administrator, ct);

        if (user?.RecoveryKeyHash is null
            || !UserAuthService.VerifyPassword(Normalize(recoveryKey), user.RecoveryKeyHash))
        {
            await new LoginAuditService(db).LogAsync(companyId, normalized, false, "مفتاح استعادة غير صحيح");
            throw new AccountingException("اسم المستخدم أو مفتاح الاستعادة غير صحيح.");
        }

        user.SetPasswordHash(UserAuthService.HashPassword(newPassword));
        user.InvalidateSessions();
        var next = IssueFor(user);
        await db.SaveChangesAsync(ct);
        await new LoginAuditService(db).LogAsync(companyId, normalized, true, "استعادة بمفتاح الاستعادة");
        return next;
    }
}
