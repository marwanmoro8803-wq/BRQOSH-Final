using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>security-licensing-tenancy#7</c> · قرارُ المالك 2026-10-18) <b>مديرٌ نسي كلمةَ مروره يعود بمفتاح استعادته.</b>
///
/// <para><b>ما كان:</b> المديرُ يُنشأ بلا هاتف، والقناةُ في الإنتاج غيرُ متاحة — فكلمةُ مرورٍ منسيّةٌ إغلاقٌ تامٌّ للدفاتر.
/// والمفتاحُ يُعرض مرّةً عند التهيئة، <b>ويُحرق عند استعماله</b>، وخطؤه يُحسب في القفل كخطأ كلمة المرور.</para>
/// </summary>
public class TheAdministratorRecoversWithTheRecoveryKeyTests : IDisposable
{
    private const string Password = "SecurePass12!";
    private const string NewPassword = "AnotherPass34!";

    private readonly BrqoshDbContext _db;

    public TheAdministratorRecoversWithTheRecoveryKeyTests()
    {
        TestActor.Administrator();
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        TestActor.None();
        _db.Dispose();
    }

    private async Task<(long CompanyId, string Key)> BootAsync()
    {
        var result = await new DatabaseBootstrapService(_db)
            .InitializeAsync("شركة", "310123456700099", BusinessProfile.General, "admin", Password);
        return (result.CompanyId, result.RecoveryKey!);
    }

    private Task<AppUser?> LoginAsync(long companyId, string password) =>
        new UserAuthService(_db).AuthenticateAsync(companyId, "admin", password);

    [Fact]
    public async Task Setup_ReturnsAKeyOnce_AndStoresOnlyItsHash()
    {
        var (companyId, key) = await BootAsync();

        Assert.Matches("^[A-Z2-9]{5}(-[A-Z2-9]{5}){4}$", key);
        var stored = (await _db.AppUsers.SingleAsync()).RecoveryKeyHash;
        Assert.StartsWith("pbkdf2:", stored);
        Assert.DoesNotContain(AdminRecoveryKey.Normalize(key), stored!, StringComparison.Ordinal);

        var again = await new DatabaseBootstrapService(_db)
            .InitializeAsync("شركة", "310123456700099", BusinessProfile.General, "admin", Password);
        Assert.Equal(companyId, again.CompanyId);
        Assert.Null(again.RecoveryKey);
    }

    [Fact]
    public async Task TheKey_SetsANewPassword_AndIsBurned_AndANewOneIsIssued()
    {
        var (companyId, key) = await BootAsync();
        var recovery = new AdminRecoveryKey(_db);

        var next = await recovery.RecoverAsync(companyId, "ADMIN", key.ToLowerInvariant().Replace("-", " "), NewPassword);

        Assert.NotNull(await LoginAsync(companyId, NewPassword));
        Assert.Null(await LoginAsync(companyId, Password));
        Assert.NotEqual(AdminRecoveryKey.Normalize(key), AdminRecoveryKey.Normalize(next));

        await Assert.ThrowsAsync<AccountingException>(() =>
            recovery.RecoverAsync(companyId, "admin", key, "ThirdPass56!xx"));
        await recovery.RecoverAsync(companyId, "admin", next, "ThirdPass56!xx");
        Assert.NotNull(await LoginAsync(companyId, "ThirdPass56!xx"));
    }

    [Fact]
    public async Task WrongKeys_CountTowardTheLockout_AndThenEvenTheRightKeyWaits()
    {
        var (companyId, key) = await BootAsync();
        var recovery = new AdminRecoveryKey(_db);

        for (var i = 0; i < PersistentLoginLockout.MaxFailedAttempts; i++)
            await Assert.ThrowsAsync<AccountingException>(() =>
                recovery.RecoverAsync(companyId, "admin", AdminRecoveryKey.NewKey(), NewPassword));

        var locked = await Assert.ThrowsAsync<AccountingException>(() =>
            recovery.RecoverAsync(companyId, "admin", key, NewPassword));
        Assert.Contains("مقفل", locked.Message, StringComparison.Ordinal);

        // ‏والعدّادُ واحد: من خمّن المفتاحَ أقفل الدخولَ بكلمة المرور كذلك — لا بابان يُخمَّن كلٌّ منهما خمساً.
        await Assert.ThrowsAsync<AccountingException>(() => LoginAsync(companyId, Password));
    }

    [Fact]
    public async Task AWeakNewPassword_IsRefused_AndTheKeyStaysValid()
    {
        var (companyId, key) = await BootAsync();
        var recovery = new AdminRecoveryKey(_db);

        await Assert.ThrowsAsync<ArgumentException>(() => recovery.RecoverAsync(companyId, "admin", key, "short"));

        await recovery.RecoverAsync(companyId, "admin", key, NewPassword);
        Assert.NotNull(await LoginAsync(companyId, NewPassword));
    }

    [Fact]
    public async Task ANonAdministrator_HasNoRecoveryPath()
    {
        var (companyId, _) = await BootAsync();
        var clerk = AppUser.Create(companyId, "clerk", UserAuthService.HashPassword(Password), "محاسب");
        var clerkKey = AdminRecoveryKey.IssueFor(clerk);   // ‏حتى لو حمل مفتاحاً بطريقٍ ما — الدورُ يُسأل لا المفتاحُ وحدَه
        _db.AppUsers.Add(clerk);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<AccountingException>(() =>
            new AdminRecoveryKey(_db).RecoverAsync(companyId, "clerk", clerkKey, NewPassword));
    }

    [Fact]
    public async Task Reissuing_InvalidatesThePreviousKey()
    {
        var (companyId, key) = await BootAsync();
        var adminId = (await _db.AppUsers.SingleAsync()).Id;
        var recovery = new AdminRecoveryKey(_db);

        var fresh = await recovery.ReissueAsync(adminId);

        await Assert.ThrowsAsync<AccountingException>(() => recovery.RecoverAsync(companyId, "admin", key, NewPassword));
        await recovery.RecoverAsync(companyId, "admin", fresh, NewPassword);
        Assert.NotNull(await LoginAsync(companyId, NewPassword));
    }
}
