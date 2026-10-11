using System.Security.Cryptography;
using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Modules;
using Brqosh.Domain.Payroll;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Licensing;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٠ · ‏`commercial-readiness#6` · ‏`installer-deployment-ops#8`) <b>الترخيصُ المنتهي يفتح الدفاترَ
/// ‏للقراءة ولا يُقفلها — والإنذارُ يسبقه بثلاثين يوماً.</b>
///
/// <para>‏كانت الصلاحيةُ إذا انقضت تُظهر نافذةَ التفعيل، وإغلاقُها يُغلق البرنامج: <b>المالكُ خارجَ دفاتره النظاميّة</b> بلا إنذارٍ
/// ‏سابق — وهو ملزَمٌ بحفظها وإتاحتها. فالمنتهي الأصيلُ (توقيعُه صحيح) يدخل قراءةً، و<b>القراءةُ تُفرض عند الحفظ</b>
/// ‏(<see cref="ReadOnlyBooksInterceptor"/>) لا في كلّ شاشة: لا يُضاف ولا يُعدَّل ولا يُحذف شيءٌ من الدفاتر، إلّا سجلَّ الدخول
/// ‏والتدقيق. <b>والمزوَّرُ المنتهي لا يُمنحها</b> — فحكمُ «منتهٍ» يسبق فحصَ التوقيع في <c>Validate</c>.</para>
/// </summary>
[Collection("SequentialEnv")]
public class AnExpiredLicenseOpensTheBooksReadOnlyTests : IDisposable
{
    private const string Company = "منشأة القراءة";
    private const string Machine = "MACHINE-1";

    public AnExpiredLicenseOpensTheBooksReadOnlyTests()
    {
        TestActor.Administrator();
        ReadOnlyBooks.ResetForTests();
    }

    public void Dispose()
    {
        ReadOnlyBooks.ResetForTests();
        TestActor.None();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void A_genuine_expired_license_is_recognised_as_expired_not_as_invalid()
    {
        using var _ = RsaTestKeys.UseForValidation();

        Assert.True(RsaLicenseValidator.IsGenuineButExpired(Company, Machine, RsaTestKeys.Sign(Company, Machine, days: -1)));
        Assert.False(RsaLicenseValidator.IsGenuineButExpired(Company, Machine, RsaTestKeys.Sign(Company, Machine, days: 30)));
        Assert.False(RsaLicenseValidator.IsGenuineButExpired("منشأة أخرى", Machine, RsaTestKeys.Sign(Company, Machine, days: -1)));
    }

    /// <summary>والمزوَّرُ المنتهي — موقَّعٌ بمفتاحٍ غير مفتاح المورِّد — لا يفتح شيئاً، ولا قراءة.</summary>
    [Fact]
    public void A_forged_expired_license_does_not_open_the_books()
    {
        using var keys = RsaTestKeys.UseForValidation();
        using var forger = RSA.Create(2048);
        var forged = RsaLicenseValidator.CreateSignedLicenseJson(
            new LicensePayload(Company, Machine, "test", DateTime.UtcNow.AddDays(-1), ["core"]),
            forger.ExportPkcs8PrivateKeyPem());

        Assert.Equal(LicenseVerdict.Expired, RsaLicenseValidator.Validate(Company, Machine, forged, out _));
        Assert.False(RsaLicenseValidator.IsGenuineButExpired(Company, Machine, forged));
    }

    [Fact]
    public async Task In_read_only_mode_no_book_record_is_saved_but_the_audit_trail_is()
    {
        using var db = await BooksAsync();
        var companyId = await db.Companies.Select(c => c.Id).SingleAsync();

        ReadOnlyBooks.Enter();

        db.Employees.Add(Employee.Create(companyId, "E-1", "موظّف", "1011111111",
            EmployeeNationality.Saudi, "محاسب", new DateOnly(2022, 1, 1)));
        var error = await Assert.ThrowsAsync<AccountingException>(() => db.SaveChangesAsync());
        Assert.Contains("للقراءة فقط", error.Message, StringComparison.Ordinal);
        db.ChangeTracker.Clear();

        await new AuditService(db).LogAsync(companyId, "admin", "دخول", "Session", "S-1", "قراءة فقط");
        Assert.Equal(1, await db.AuditLogEntries.CountAsync(e => e.EntityKey == "S-1"));
    }

    /// <summary>شاهدُ ضبط: خارجَ وضع القراءة يُحفظ كلُّ شيءٍ كما كان.</summary>
    [Fact]
    public async Task Outside_read_only_mode_books_are_saved_as_before()
    {
        using var db = await BooksAsync();
        var companyId = await db.Companies.Select(c => c.Id).SingleAsync();

        db.Employees.Add(Employee.Create(companyId, "E-1", "موظّف", "1011111111",
            EmployeeNationality.Saudi, "محاسب", new DateOnly(2022, 1, 1)));
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Employees.CountAsync());
    }

    [Theory]
    [InlineData(45, null)]
    [InlineData(31, null)]
    [InlineData(30, 30)]
    [InlineData(7, 7)]
    [InlineData(0, 0)]
    public void The_owner_is_warned_thirty_days_ahead(int daysLeft, int? warned)
    {
        var now = new DateTime(2026, 10, 11, 9, 0, 0, DateTimeKind.Utc);

        Assert.Equal(warned, LicenseExpiryNotice.DaysLeftToWarn(now.AddDays(daysLeft).AddHours(1), now));
    }

    private static async Task<BrqoshDbContext> BooksAsync()
    {
        var db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, "Data Source=:memory:");
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        await new DatabaseInitializer(db).InitializeAsync(Company, "310123456700077", BusinessProfile.General);
        return db;
    }
}
