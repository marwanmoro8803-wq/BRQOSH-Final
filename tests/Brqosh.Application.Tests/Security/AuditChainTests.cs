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

    /// <summary>
    /// (CEEF-229) <b>ثقبُ `Sequence = 0` — إعفاءٌ بلا تاريخِ انقضاء.</b>
    ///
    /// <para>كان المتحقّقُ يتخطّى عمداً كلَّ صفٍّ ترتيبُه صفر بحجّة «ما قبل تفعيل السلسلة».
    /// والحجّةُ صحيحةٌ **لصفوفٍ كُتبت قبل التفعيل** — لكنّ الإعفاء لا ينتهي، فصفٌّ يُدسّ
    /// اليوم بترتيبٍ صفر <b>يمرّ على كاشف العبث بلا فحصٍ إلى الأبد</b>.</para>
    ///
    /// <para><b>وكيف وُجد:</b> لم يكشفه تدقيقٌ ولا قراءة. كشفته <b>طفرةٌ لم تُلدَغ</b> في
    /// م2-١١: غيّرتُ الشفرةَ لتكتب سطرَ تدقيقٍ غيرَ مربوط، وتوقّعتُ أن يصرخ الحارس —
    /// <b>فمرّ أخضر</b>. وحارسٌ لا تُلدَغ طفرتُه حارسٌ أعمى.</para>
    ///
    /// <para><b>وليس ترفاً:</b> `AuditService` هو الكاتبُ الوحيد في المستودع كلّه وهو يربط
    /// دائماً. فكلُّ صفٍّ غيرِ مربوطٍ يظهر <b>بعد</b> بدء السلسلة أتى من خارج التطبيق —
    /// SQL خام، أو أداةُ إدارة، أو نسخةٌ مستعادة. وهو بعينه مسارُ المهاجم الذي بُنيت
    /// السلسلةُ لأجله.</para>
    /// </summary>
    [Fact]
    public async Task UnchainedRow_InsertedAfterTheChainBegan_IsDetected()
    {
        await SetupAsync();
        await LogAsync("ترحيل", "A-1");
        await LogAsync("ترحيل", "A-2");

        await InsertUnchainedRowAsync(sequence: 0, "مدير مزوَّر", "حذف قيد", "JE-99");

        var result = await new AuditChainVerifier(_db).VerifyAsync(_companyId);

        Assert.False(result.IsIntact,
            "صفٌّ غيرُ مربوطٍ دُسّ بعد بدء السلسلة — ومرّ على كاشف العبث بلا فحص.");
        Assert.Contains(result.Breaks, b => b.Kind == AuditChainBreakKind.UnchainedEntry);
    }

    /// <summary>
    /// <b>ولا ينجو بانتحال حقبةٍ ليس منها.</b> المهاجمُ يعرف أنّ ما قبل السلسلة سالب،
    /// فيدسّ صفَّه بترتيبٍ سالب. لكنّ <b>مفتاحه الأوّليّ يفضحه</b>: صفوفُ ما قبل السلسلة
    /// كُتبت قبل أوّل صفٍّ مربوط، ومفاتيحُها أقدمُ منه بالضرورة.
    /// </summary>
    [Fact]
    public async Task UnchainedRow_ForgedWithALegacyOrdinal_IsStillDetected()
    {
        await SetupAsync();
        await LogAsync("ترحيل", "A-1");
        await LogAsync("ترحيل", "A-2");

        await InsertUnchainedRowAsync(sequence: -1, "مدير مزوَّر", "حذف قيد", "JE-99");

        var result = await new AuditChainVerifier(_db).VerifyAsync(_companyId);

        Assert.Contains(result.Breaks, b => b.Kind == AuditChainBreakKind.UnchainedEntry);
    }

    /// <summary>
    /// <b>والاتّجاه الآخر — وهو ما يجعل الحارس قابلاً للشحن.</b> صفوفُ عميلٍ قائمٍ كُتبت
    /// <b>قبل</b> تفعيل السلسلة، ورقّمتها الترقيةُ في حقبةٍ سالبة. فلو صرخ الحارسُ عليها
    /// لصار كلُّ عميلٍ قائمٍ «معبوثاً بدفاتره» عند أوّل تحقّق — <b>وحارسٌ يصرخ دائماً لا
    /// يُسمَع أبداً</b>.
    /// </summary>
    [Fact]
    public async Task LegacyRows_NumberedByTheUpgrade_AreNotFlagged()
    {
        await SetupAsync();

        // ‏صورةُ ما تُخرجه الترقية: أقدمُ صفٍّ -2 ثمّ -1، ومفاتيحُهما تسبق كلَّ مربوط.
        await InsertUnchainedRowAsync(sequence: -2, "admin", "ترحيل قديم", "OLD-1");
        await InsertUnchainedRowAsync(sequence: -1, "admin", "ترحيل قديم", "OLD-2");

        await LogAsync("ترحيل", "A-1");
        await LogAsync("ترحيل", "A-2");

        var result = await new AuditChainVerifier(_db).VerifyAsync(_companyId);

        Assert.True(result.IsIntact,
            "أُنذر على أثرٍ قديمٍ مشروع — فكلُّ عميلٍ قائمٍ يصير «معبوثاً بدفاتره».");
    }

    /// <summary>
    /// <b>والسلسلةُ تبدأ من ١ فوق حقبةٍ سالبة.</b> لو حُسب «الأعلى + ١» على أثرٍ كلُّه
    /// قديم لكان الناتجُ صفراً — أيْ ترتيباً محجوزاً لغير المربوط، فيصير أوّلُ صفٍّ
    /// مربوطٍ في تاريخ المنشأة <b>غيرَ مربوطٍ في نظر المتحقّق</b>.
    /// </summary>
    [Fact]
    public async Task TheChainStartsAtOne_EvenAboveALegacyEra()
    {
        await SetupAsync();
        await InsertUnchainedRowAsync(sequence: -1, "admin", "ترحيل قديم", "OLD-1");

        await LogAsync("ترحيل", "A-1");

        var first = await _db.AuditLogEntries.AsNoTracking()
            .SingleAsync(a => a.CompanyId == _companyId && a.EntityKey == "A-1");

        Assert.Equal(1, first.Sequence);
        Assert.Equal(AuditLogEntry.GenesisHash, first.PreviousHash);
        Assert.True((await new AuditChainVerifier(_db).VerifyAsync(_companyId)).IsIntact);
    }

    /// <summary>
    /// ‏صفٌّ يُكتب إلى القاعدة **بلا مرور بـ`AuditService`** — تجزئتُه فارغة. وهي صورةُ
    /// الصفّ القديم وصورةُ الصفّ المدسوس معاً: <b>ولا يفرّق بينهما إلا موضعُه</b>.
    /// </summary>
    private async Task InsertUnchainedRowAsync(long sequence, string username, string action, string key) =>
        await ExecuteRawAsync($"""
            INSERT INTO audit_log
                (CompanyId, Username, Action, EntityType, EntityKey, Details, Reason,
                 OccurredAtUtc, Sequence, PreviousHash, EntryHash)
            VALUES
                ({_companyId}, '{username}', '{action}', 'TestEntity', '{key}', '', NULL,
                 '2026-01-01 00:00:00', {sequence}, '{AuditLogEntry.GenesisHash}', '');
            """);
}
