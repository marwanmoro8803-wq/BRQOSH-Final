using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-٥٠ · <c>security-licensing-tenancy#4</c> + <c>#8</c>) <b>تغييرُ صلاحيّة مستخدمٍ
/// يُنهي جلستَه ويُكتب في سجلّ التدقيق — وكان لا هذا ولا ذاك.</b>
///
/// <para>‏<b>والآليّةُ كانت قائمةً ولا يستدعيها أحد</b>: <c>AppUser.InvalidateSessions</c> يزيد
/// <c>SessionVersion</c>، <b>وسطحُ المكتب يقرؤه في مؤقّت الخمول</b> فيُعيد المستخدمَ إلى الدخول بـ«تم
/// إنهاء جلستك من قبل المسؤول». <b>ومستدعيه الوحيدُ مسارُ استرداد كلمة المرور بالواتساب</b> — فمديرٌ
/// يُعطّل موظّفاً أو يُنزله من «مدير» إلى «محاسب» <b>يتركه يعمل بصلاحيّته القديمة حتى يخرج بنفسه</b>.</para>
///
/// <para>‏<b>ولا أثرَ في سلسلة التدقيق</b>: الإنشاءُ والترقيةُ والتعطيلُ وتعيينُ كلمة المرور — لا شيءَ منها
/// يُكتب. <b>والدخولُ وحدَه يُسجَّل</b>، في جدولٍ آخرَ لا سلسلةَ له.</para>
/// </summary>
public class PrivilegeChangesEndSessionsAndAreAuditedTests : IDisposable
{
    private const string Password = "Str0ng!Passw0rd#1";
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public PrivilegeChangesEndSessionsAndAreAuditedTests()
    {
        _db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        SecurityContext.CurrentRole = UserRole.Administrator;
        SecurityContext.CurrentUsername = "admin";
    }

    [Fact]
    public async Task DemotingAUser_EndsTheirSession_AndIsAudited()
    {
        var user = await CreateAsync("sara", UserRole.Administrator);
        var before = await VersionAsync(user.Id);

        await Service().UpdateAsync(_companyId, user.Id, "سارة", UserRole.Accountant, isActive: true, newPassword: null);

        Assert.True(await VersionAsync(user.Id) > before, "أُنزلت صلاحيّتُها وبقيت جلستُها — تعمل مديرةً حتى تخرج.");
        var audit = await AuditAsync("تغيير دور مستخدم");
        Assert.Equal("sara", audit.EntityKey);
        Assert.Contains(AppUser.RoleLabel(UserRole.Administrator), audit.Details);
        Assert.Contains(AppUser.RoleLabel(UserRole.Accountant), audit.Details);
        Assert.Equal("admin", audit.Username);
    }

    [Fact]
    public async Task DeactivatingAUser_EndsTheirSession_AndIsAudited()
    {
        var user = await CreateAsync("omar", UserRole.Accountant);
        var before = await VersionAsync(user.Id);

        await Service().UpdateAsync(_companyId, user.Id, "عمر", UserRole.Accountant, isActive: false, newPassword: null);

        Assert.True(await VersionAsync(user.Id) > before, "عُطّل وبقيت جلستُه — يعمل حتى يخرج بنفسه.");
        Assert.Equal("omar", (await AuditAsync("تعطيل مستخدم")).EntityKey);
    }

    [Fact]
    public async Task ReactivatingAUser_IsAudited()
    {
        var user = await CreateAsync("lina", UserRole.Accountant);
        await Service().UpdateAsync(_companyId, user.Id, "لينا", UserRole.Accountant, isActive: false, newPassword: null);

        await Service().UpdateAsync(_companyId, user.Id, "لينا", UserRole.Accountant, isActive: true, newPassword: null);

        Assert.Equal("lina", (await AuditAsync("تفعيل مستخدم")).EntityKey);
    }

    /// <summary>
    /// ‏<b>وتعيينُ المدير كلمةَ مرورٍ يُنهي الجلسة — والسجلُّ لا يحمل السرّ</b>: لا الكلمةَ ولا بصمتَها.
    /// </summary>
    [Fact]
    public async Task AnAdminSettingAPassword_EndsTheSession_AndTheAuditHoldsNoSecret()
    {
        var user = await CreateAsync("huda", UserRole.Accountant);
        var before = await VersionAsync(user.Id);
        const string newPassword = "An0ther!Secret#9";

        await Service().UpdateAsync(_companyId, user.Id, "هدى", UserRole.Accountant, isActive: true, newPassword);

        Assert.True(await VersionAsync(user.Id) > before);
        Assert.Equal("huda", (await AuditAsync("تعيين كلمة مرور مستخدم")).EntityKey);

        var hash = (await _db.AppUsers.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == user.Id)).PasswordHash;
        var rows = await _db.AuditLogEntries.AsNoTracking().ToListAsync();
        Assert.DoesNotContain(rows, a => (a.Details + a.Reason).Contains(newPassword) || (a.Details + a.Reason).Contains(hash));
    }

    [Fact]
    public async Task ResettingAPassword_EndsTheSession_AndIsAudited()
    {
        var user = await CreateAsync("khalid", UserRole.Accountant);
        var before = await VersionAsync(user.Id);

        await Service().ResetPasswordAsync(_companyId, "khalid", "Re5et!Passw0rd#2");

        Assert.True(await VersionAsync(user.Id) > before);
        Assert.Equal("khalid", (await AuditAsync("إعادة تعيين كلمة مرور مستخدم")).EntityKey);
    }

    [Fact]
    public async Task CreatingAUser_IsAudited_WithTheirRole()
    {
        await CreateAsync("noor", UserRole.Administrator);

        var audit = await AuditAsync("إنشاء مستخدم");
        Assert.Equal("noor", audit.EntityKey);
        Assert.Contains(AppUser.RoleLabel(UserRole.Administrator), audit.Details);
    }

    /// <summary>
    /// ‏<b>وتعديلُ الاسم وحدَه لا يُنهي جلسةً ولا يُكتب صلاحيّةً</b> (شاهدُ ضبط) — فالإنهاءُ لتغيير
    /// الصلاحيّة لا لكلّ حفظ، <b>وإلّا خرج الموظّفُ كلّما صُحّح اسمُه</b>.
    /// </summary>
    [Fact]
    public async Task RenamingOnly_KeepsTheSession_AndAuditsNoPrivilegeChange()
    {
        var user = await CreateAsync("reem", UserRole.Accountant);
        var before = await VersionAsync(user.Id);
        var auditsBefore = await _db.AuditLogEntries.CountAsync();

        await Service().UpdateAsync(_companyId, user.Id, "ريم العتيبي", UserRole.Accountant, isActive: true, newPassword: null);

        Assert.Equal(before, await VersionAsync(user.Id));
        Assert.Equal(auditsBefore, await _db.AuditLogEntries.CountAsync());
    }

    // ── تهيئة ───────────────────────────────────────────────────────────────

    private UserManagementService Service() => new(_db);

    private async Task<AppUser> CreateAsync(string username, UserRole role)
    {
        if (_companyId == 0)
        {
            var company = await new DatabaseInitializer(_db).InitializeAsync(
                "شركة", "310123456700099", BusinessProfile.General);
            _companyId = company.Id;
        }

        return await Service().CreateAsync(_companyId, username, Password, username, role);
    }

    private Task<int> VersionAsync(long userId) =>
        _db.AppUsers.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.SessionVersion).SingleAsync();

    private Task<AuditLogEntry> AuditAsync(string action) =>
        _db.AuditLogEntries.AsNoTracking()
            .SingleAsync(a => a.CompanyId == _companyId && a.Action == action);

    public void Dispose()
    {
        SecurityContext.CurrentRole = null;
        SecurityContext.CurrentUsername = null;
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
