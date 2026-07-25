using Brqosh.Domain.Modules;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Security;

/// <summary>
/// O-19 (CEEF-212) — سلسلة تجزئة سجل التدقيق.
///
/// معترض EF يمنع التعديل والحذف **داخل التطبيق فقط**؛ أي وصول مباشر للقاعدة
/// (SQL خام، أداة إدارة، نسخة مستعادة) كان يعدّل أو يحذف بلا أثر. السلسلة لا تمنع
/// العبث — تجعله مكشوفاً. لذلك تعبث هذه الاختبارات بـ**SQL خام** لا عبر EF:
/// هذا هو مسار المهاجم الحقيقي، وهو ما كان غير قابل للكشف.
/// </summary>
public class AuditChainTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BrqoshDbContext _db;
    private long _companyId;

    public AuditChainTests()
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

    private async Task SetupAsync()
    {
        var company = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة السلسلة", "310123456700003", BusinessProfile.General);
        _companyId = company.Id;
        await new BrqoshSchemaUpdater(_db).ApplyAsync();
    }

    private async Task LogAsync(string action, string key) =>
        await new AuditService(_db).LogAsync(
            _companyId, "admin", action, "TestEntity", key, $"تفاصيل {key}");

    private async Task ExecuteRawAsync(string sql)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>سلسلة سليمة: ترتيب متتابع وارتباط صحيح وتجزئات مطابقة.</summary>
    [Fact]
    public async Task IntactChain_VerifiesClean()
    {
        await SetupAsync();
        await LogAsync("ترحيل", "A-1");
        await LogAsync("إلغاء", "A-2");
        await LogAsync("تعديل", "A-3");

        var result = await new AuditChainVerifier(_db).VerifyAsync(_companyId);

        Assert.True(result.IsIntact, string.Join(" | ", result.Breaks.Select(b => b.Description)));

        var entries = await _db.AuditLogEntries.AsNoTracking()
            .Where(a => a.CompanyId == _companyId && a.Sequence > 0)
            .OrderBy(a => a.Sequence).ToListAsync();

        Assert.Equal([1L, 2L, 3L], entries.Select(e => e.Sequence));
        Assert.Equal(AuditLogEntry.GenesisHash, entries[0].PreviousHash);
        Assert.Equal(entries[0].EntryHash, entries[1].PreviousHash);   // الارتباط فعليّ
        Assert.Equal(entries[1].EntryHash, entries[2].PreviousHash);
    }

    /// <summary>تعديل حقل عبر SQL خام — يُكشف رغم تجاوزه معترض EF.</summary>
    [Fact]
    public async Task TamperedContent_IsDetected()
    {
        await SetupAsync();
        await LogAsync("ترحيل", "A-1");
        await LogAsync("إلغاء", "A-2");

        // مسار المهاجم: تعديل مباشر في القاعدة لا عبر التطبيق.
        await ExecuteRawAsync(
            "UPDATE audit_log SET Details = 'محتوى مزوَّر' WHERE Sequence = 1");

        var result = await new AuditChainVerifier(_db).VerifyAsync(_companyId);

        Assert.False(result.IsIntact);
        Assert.Contains(result.Breaks, b =>
            b.Kind == AuditChainBreakKind.TamperedContent && b.Sequence == 1);
    }

    /// <summary>حذف إدخال من المنتصف — تُكشف الفجوة والارتباط المكسور معاً.</summary>
    [Fact]
    public async Task DeletedEntry_IsDetected()
    {
        await SetupAsync();
        await LogAsync("ترحيل", "A-1");
        await LogAsync("إلغاء", "A-2");
        await LogAsync("تعديل", "A-3");

        await ExecuteRawAsync("DELETE FROM audit_log WHERE Sequence = 2");

        var result = await new AuditChainVerifier(_db).VerifyAsync(_companyId);

        Assert.False(result.IsIntact);
        Assert.Contains(result.Breaks, b => b.Kind == AuditChainBreakKind.MissingEntry);
        Assert.Contains(result.Breaks, b => b.Kind == AuditChainBreakKind.BrokenLink);
    }

    /// <summary>
    /// إعادة حساب التجزئة بعد التزوير لا تُنقذ المهاجم: الإدخال التالي يحمل التجزئة
    /// القديمة، فينكسر الارتباط. هذا جوهر السلسلة — التزوير يتطلب إعادة كتابة كل ما بعده.
    /// </summary>
    [Fact]
    public async Task TamperWithRecomputedHash_StillBreaksTheLink()
    {
        await SetupAsync();
        await LogAsync("ترحيل", "A-1");
        await LogAsync("إلغاء", "A-2");

        var first = await _db.AuditLogEntries.AsNoTracking()
            .SingleAsync(a => a.CompanyId == _companyId && a.Sequence == 1);

        // المهاجم يعدّل المحتوى **ويعيد حساب** تجزئته ليبدو متسقاً.
        var forged = AuditLogEntry.Record(_companyId, first.Username, first.Action,
            first.EntityType, first.EntityKey, "محتوى مزوَّر");
        forged.ChainTo(1, AuditLogEntry.GenesisHash);

        await ExecuteRawAsync(
            $"UPDATE audit_log SET Details = 'محتوى مزوَّر', EntryHash = '{forged.EntryHash}' WHERE Sequence = 1");

        var result = await new AuditChainVerifier(_db).VerifyAsync(_companyId);

        Assert.False(result.IsIntact);
        Assert.Contains(result.Breaks, b =>
            b.Kind == AuditChainBreakKind.BrokenLink && b.Sequence == 2);
    }

    /// <summary>سلسلة كل شركة مستقلة — عبثٌ في واحدة لا يُبطل الأخرى.</summary>
    [Fact]
    public async Task ChainsAreIndependentPerCompany()
    {
        await SetupAsync();
        var other = await new DatabaseInitializer(_db)
            .InitializeAsync("شركة أخرى", "310123456700004", BusinessProfile.General);

        await LogAsync("ترحيل", "A-1");
        await new AuditService(_db).LogAsync(other.Id, "admin", "ترحيل", "TestEntity", "B-1");

        var mine = await new AuditChainVerifier(_db).VerifyAsync(_companyId);
        var theirs = await new AuditChainVerifier(_db).VerifyAsync(other.Id);

        Assert.True(mine.IsIntact);
        Assert.True(theirs.IsIntact);

        // كل سلسلة تبدأ من 1 بجذر التكوين.
        var firstOther = await _db.AuditLogEntries.AsNoTracking()
            .SingleAsync(a => a.CompanyId == other.Id && a.Sequence == 1);
        Assert.Equal(AuditLogEntry.GenesisHash, firstOther.PreviousHash);
    }
}
