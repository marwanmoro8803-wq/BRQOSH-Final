using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// ‏(تقويةُ الـAPI · S-5) <b>رمزُ TOTP يُقبل مرّةً واحدة.</b>
///
/// <para>‏<c>ValidateCode</c> يقبل الخطوةَ الحاليّة وجارتيها (±٣٠ ثانية) — <b>ولا يتذكّر ما قبِل</b>. فمن رأى الرمزَ أو
/// اعترضه (كتفٌ، شاشةٌ مشتركة، سجلُّ وكيل) يدخل به ثانيةً خلال نحو تسعين ثانية، وكلمةُ المرور وحدَها تكفيه. RFC 6238 §5.2:
/// «لا يقبل المُتحقِّقُ الرمزَ نفسَه بعد نجاح أوّل تحقّقٍ منه».</para>
///
/// <para>‏<b>والقياسُ على الخدمة وبسياقٍ جديدٍ لكلّ دخول</b> — فالذاكرةُ في القاعدة، لا في كائنٍ يبقى بين محاولتين.</para>
/// </summary>
public class ATotpCodeIsAcceptedOnceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<BrqoshDbContext> _options;
    private const long Company = 1;
    private const string User = "mudir";
    private const string Password = "Str0ng-Passw0rd!2026";
    private readonly string _secret = TotpMfaService.GenerateSecret();

    public ATotpCodeIsAcceptedOnceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<BrqoshDbContext>().UseSqlite(_connection).Options;
        using var db = new BrqoshDbContext(_options);
        db.Database.EnsureCreated();
        TestActor.Administrator();

        var user = AppUser.Create(Company, User, UserAuthService.HashPassword(Password), "مدير", UserRole.Administrator);
        user.SetMfaSecret(_secret);
        db.AppUsers.Add(user);
        db.SaveChanges();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>‏<b>القياسُ الحاكم:</b> الرمزُ نفسُه في دخولٍ ثانٍ — بسياقٍ آخر — يُرفض.</summary>
    [Fact]
    public async Task TheSameCode_IsRefusedTheSecondTime()
    {
        var code = TotpMfaService.CodeAt(_secret, DateTime.UtcNow);

        Assert.NotNull(await LoginAsync(code));
        Assert.Null(await LoginAsync(code));
    }

    /// <summary>‏<b>ورمزُ خطوةٍ أقدمَ ممّا قُبل يُرفض</b> وإن بقي داخل نافذة الانحراف.</summary>
    [Fact]
    public async Task AnOlderStepsCode_IsRefusedAfterANewerOneWasAccepted()
    {
        var now = DateTime.UtcNow;
        Assert.NotNull(await LoginAsync(TotpMfaService.CodeAt(_secret, now)));

        Assert.Null(await LoginAsync(TotpMfaService.CodeAt(_secret, now.AddSeconds(-30))));
    }

    /// <summary>‏<b>ورمزُ الخطوة التالية يُقبل</b> (شاهدُ ضبط) — فالحارسُ لا يُقفل الحسابَ بعد أوّل دخول.</summary>
    [Fact]
    public async Task TheNextStepsCode_IsStillAccepted()
    {
        var now = DateTime.UtcNow;
        Assert.NotNull(await LoginAsync(TotpMfaService.CodeAt(_secret, now)));

        Assert.NotNull(await LoginAsync(TotpMfaService.CodeAt(_secret, now.AddSeconds(30))));
    }

    /// <summary>‏<b>والرفضُ يُحسب إخفاقاً</b> — كرمزٍ خاطئ — فلا يصير الإعادةُ طريقاً للتخمين بلا عدّاد.</summary>
    [Fact]
    public async Task AReplayedCode_CountsAsAFailedLogin()
    {
        var code = TotpMfaService.CodeAt(_secret, DateTime.UtcNow);
        await LoginAsync(code);
        await LoginAsync(code);

        await using var db = new BrqoshDbContext(_options);
        var state = await new PersistentLoginLockout(db).ReadAsync(Company, User);
        Assert.Equal(1, state.ConsecutiveFailures);
    }

    /// <summary>
    /// ‏<b>وعلى PostgreSQL حقيقيّ عبر الهجرات</b> — فالعمودُ تُضيفه الهجرةُ هناك لا النموذج، والتحديثُ المشروطُ يجري على الخادم.
    /// </summary>
    [PostgresFact]
    public async Task TheSameCode_IsRefusedTheSecondTime_OnPostgres()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("totp_once");
        await new DatabaseSchemaService(scope.Db).EnsureSchemaAsync();
        var user = AppUser.Create(Company, User, UserAuthService.HashPassword(Password), "مدير", UserRole.Administrator);
        user.SetMfaSecret(_secret);
        scope.Db.AppUsers.Add(user);
        await scope.Db.SaveChangesAsync();

        var code = TotpMfaService.CodeAt(_secret, DateTime.UtcNow);
        await using (var first = scope.OpenIndependentContext())
            Assert.NotNull(await new UserAuthService(first).AuthenticateAsync(Company, User, Password, code));
        await using var second = scope.OpenIndependentContext();
        Assert.Null(await new UserAuthService(second).AuthenticateAsync(Company, User, Password, code));
    }

    private async Task<AppUser?> LoginAsync(string code)
    {
        await using var db = new BrqoshDbContext(_options);
        return await new UserAuthService(db).AuthenticateAsync(Company, User, Password, code);
    }
}
