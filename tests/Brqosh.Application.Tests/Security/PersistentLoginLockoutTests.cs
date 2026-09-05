using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (CEEF-237 · ت-٦) <b>قفلُ الدخول يعيش في القاعدة ويُفرَض في الخدمة.</b>
///
/// <para><b>ما كان:</b> قاموسٌ ثابتٌ في ذاكرة العملية، يُفرَض في <c>LoginWindow</c> وحدَها.
/// <b>فإعادةُ تشغيل التطبيق تمحو العدّاد</b> — ومُخمِّنٌ يُغلق النافذةَ ويفتحها يستأنف من
/// صفر بلا حدّ. <b>وأيُّ مسارٍ آخر يستدعي <c>AuthenticateAsync</c> لم يكن يمرّ بقفلٍ أصلاً.</b>
/// وإخفاقاتُ رمز MFA لم تكن تُحسب بحال.</para>
///
/// <para><b>والقياسُ هنا على الخدمة لا على النافذة</b> — فذاك موضعُ العطل.</para>
/// </summary>
public class PersistentLoginLockoutTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private const long Company = 1;
    private const string User = "muhasib";
    private const string Password = "Str0ng-Passw0rd!2026";

    public PersistentLoginLockoutTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new BrqoshDbContext(new DbContextOptionsBuilder<BrqoshDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        TestActor.Administrator();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task SeedUserAsync()
    {
        _db.AppUsers.Add(AppUser.Create(
            Company, User, UserAuthService.HashPassword(Password), "محاسب", UserRole.Accountant));
        await _db.SaveChangesAsync();
    }

    private async Task FailAsync(int times)
    {
        for (var i = 0; i < times; i++)
            await new LoginAuditService(_db).LogAsync(Company, User, false, "بيانات اعتماد غير صحيحة");
    }

    [Fact]
    public async Task BelowTheThreshold_TheAccountIsOpen()
    {
        await FailAsync(PersistentLoginLockout.MaxFailedAttempts - 1);

        var state = await new PersistentLoginLockout(_db).ReadAsync(Company, User);

        Assert.False(state.IsLocked);
        Assert.Equal(4, state.ConsecutiveFailures);
    }

    [Fact]
    public async Task AtTheThreshold_TheAccountLocks()
    {
        await FailAsync(PersistentLoginLockout.MaxFailedAttempts);

        var state = await new PersistentLoginLockout(_db).ReadAsync(Company, User);

        Assert.True(state.IsLocked);
        Assert.True(state.MinutesRemaining is > 0 and <= 15);
    }

    [Fact]
    public async Task ASuccessfulLogin_ClearsWhatCameBefore()
    {
        await FailAsync(4);
        await new LoginAuditService(_db).LogAsync(Company, User, true);
        await FailAsync(2);

        var state = await new PersistentLoginLockout(_db).ReadAsync(Company, User);

        // ‏وإلا قُفل حسابٌ يعمل منذ سنةٍ لأنّ صاحبَه أخطأ متفرّقاً عبر شهور.
        Assert.False(state.IsLocked);
        Assert.Equal(2, state.ConsecutiveFailures);
    }

    [Fact]
    public async Task TheLockExpiresOnItsOwn()
    {
        await FailAsync(PersistentLoginLockout.MaxFailedAttempts);

        var later = DateTime.UtcNow + PersistentLoginLockout.LockoutDuration + TimeSpan.FromMinutes(1);
        var state = await new PersistentLoginLockout(_db).ReadAsync(Company, User, nowUtc: later);

        Assert.False(state.IsLocked);
    }

    [Fact]
    public async Task ALockOnOneUser_DoesNotLockAnother()
    {
        await FailAsync(PersistentLoginLockout.MaxFailedAttempts);

        var other = await new PersistentLoginLockout(_db).ReadAsync(Company, "someone-else");

        Assert.False(other.IsLocked);
    }

    /// <summary>
    /// <b>العضّة:</b> القفلُ يُفرَض داخل <c>AuthenticateAsync</c> — لا في النافذة.
    /// نزعُ السطر من الخدمة يُسقط هذا الاختبار وحدَه.
    /// </summary>
    [Fact]
    public async Task TheServiceItselfRefusesALockedAccount_EvenWithTheRightPassword()
    {
        await SeedUserAsync();
        var service = new UserAuthService(_db);

        // ‏كلمةٌ صحيحةٌ قبل القفل: الحسابُ يعمل.
        Assert.NotNull(await service.AuthenticateAsync(Company, User, Password));

        await FailAsync(PersistentLoginLockout.MaxFailedAttempts);

        // ‏وبعد القفل تُرفض **كلمةُ المرور الصحيحة نفسُها** — وهذا معنى القفل.
        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => service.AuthenticateAsync(Company, User, Password));
        Assert.Contains("مقفل", ex.Message);
    }
}
