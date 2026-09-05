using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Modules;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (CEEF-237 · ت-٤) <b>النسخُ الاحتياطيّ يقع من نفسِه — والزرُّ وحدَه ليس حماية.</b>
///
/// <para><b>ما كان:</b> المستدعي الوحيد لـ<c>BackupService</c> زرٌّ في شاشة؛ لا مؤقّتَ ولا
/// شارةَ تقول متى كانت آخرُ نسخة. والكتيّبُ يَعِد بنسخةٍ يوميّة. <b>فمنشأةٌ لم يضغط أحدٌ
/// زرَّها شهراً ثمّ تلف قرصُها تفقد دفاترَها كلَّها</b> — وهو العطلُ الوحيدُ غيرُ القابل للتعويض.</para>
///
/// <para><b>والهويّةُ المقيسة هنا:</b> الاستحقاقُ يُقرأ من <b>ملفّ النسخة نفسِه</b> لا من سجلٍّ
/// جانبيّ — فلا يمكن أن يقول النظامُ «نُسخ» وليس في المجلّد ملفّ.</para>
/// </summary>
public class BackupScheduleTests : IDisposable
{
    private readonly string _folder;

    public BackupScheduleTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "brqosh-bk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { }
        try { Directory.Delete(Path.Combine(Path.GetTempPath(), "live-" + Path.GetFileName(_folder)), recursive: true); } catch { }
        GC.SuppressFinalize(this);
    }

    private string WriteBackup(string name, DateTime writtenUtc, long bytes = 1024)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, new byte[bytes]);
        File.SetLastWriteTimeUtc(path, writtenUtc);
        return path;
    }

    private static readonly DateTime Now = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>قاعدةُ SQLite على القرص — لأنّ VACUUM INTO يحتاج قاعدةً حقيقيّة.</summary>
    private BrqoshDbContext NewSqliteDb()
    {
        // ‏خارج مجلّد النسخ عمداً: القاعدةُ الحيّة ليست نسخة.
        var liveDir = Path.Combine(_folder, "..", "live-" + Path.GetFileName(_folder));
        Directory.CreateDirectory(liveDir);
        var live = Path.Combine(liveDir, "live.db");
        var db = BrqoshDbContextFactory.Create(DatabaseProvider.Sqlite, $"Data Source={live}");
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public void NoBackupAtAll_IsDueAndOverdue_AndSaysSoPlainly()
    {
        var state = BackupScheduleService.ReadState(_folder, nowUtc: Now);

        Assert.Null(state.LastBackupUtc);
        Assert.True(state.IsDue);
        Assert.True(state.IsOverdue);
        Assert.Equal("لا نسخةَ احتياطيّةً قطّ", state.Badge);
    }

    [Fact]
    public void AMissingFolder_IsTreatedAsNoBackup_NotAsAnError()
    {
        var state = BackupScheduleService.ReadState(
            Path.Combine(_folder, "does-not-exist"), nowUtc: Now);

        Assert.True(state.IsDue);
        Assert.Null(state.LastBackupUtc);
    }

    [Fact]
    public void ABackupFromThisMorning_IsNotDue()
    {
        WriteBackup("brqosh_a.dump", Now.AddHours(-3));

        var state = BackupScheduleService.ReadState(_folder, nowUtc: Now);

        Assert.False(state.IsDue);
        Assert.False(state.IsOverdue);
        Assert.Equal("آخرُ نسخة: اليوم", state.Badge);
    }

    [Theory]
    [InlineData(25, true, false)]    // مضى يوم — مستحقّة، لا متأخّرة
    [InlineData(49, true, true)]     // مضى يومان — متأخّرة، وتُعرض تحذيراً
    public void AgeDecidesDueAndOverdue(int hoursOld, bool due, bool overdue)
    {
        WriteBackup("brqosh_a.dump", Now.AddHours(-hoursOld));

        var state = BackupScheduleService.ReadState(_folder, nowUtc: Now);

        Assert.Equal(due, state.IsDue);
        Assert.Equal(overdue, state.IsOverdue);
    }

    [Fact]
    public void AnEmptyFile_IsNotCountedAsABackup()
    {
        // ‏بقيّةُ محاولةٍ مقطوعة: ملفٌّ بحجم صفر يوهم بحمايةٍ غير موجودة.
        WriteBackup("brqosh_truncated.dump", Now.AddHours(-1), bytes: 0);

        var state = BackupScheduleService.ReadState(_folder, nowUtc: Now);

        Assert.Null(state.LastBackupUtc);
        Assert.True(state.IsDue);
    }

    [Fact]
    public void TheNewestBackupWins_EvenAcrossProviders()
    {
        WriteBackup("brqosh_old.dump", Now.AddDays(-9));
        WriteBackup("brqosh_new.db", Now.AddHours(-2));

        var state = BackupScheduleService.ReadState(_folder, nowUtc: Now);

        Assert.False(state.IsDue);
    }

    [Fact]
    public void UnrelatedFilesAreIgnored()
    {
        File.WriteAllText(Path.Combine(_folder, "notes.txt"), "hello");
        File.WriteAllText(Path.Combine(_folder, "brqosh.zip"), "hello");
        // ‏وقاعدةٌ حيّةٌ نُسخت هنا يدوياً ليست نسخةً احتياطيّة.
        WriteBackup("live.db", Now.AddMinutes(-5));

        Assert.True(BackupScheduleService.ReadState(_folder, nowUtc: Now).IsDue);
    }

    [Fact]
    public void AClockRolledBackward_DoesNotLoopTheScheduler()
    {
        // ‏ملفٌّ بطابعٍ مستقبليّ (ساعةٌ عُدّلت، أو نسخةٌ جاءت من جهازٍ آخر).
        WriteBackup("brqosh_future.dump", Now.AddHours(+5));

        var state = BackupScheduleService.ReadState(_folder, nowUtc: Now);

        Assert.Equal(TimeSpan.Zero, state.Age);
        Assert.False(state.IsDue);
    }

    [Fact]
    public async Task WhenDue_ItActuallyWritesABackupAndTheStateFlips()
    {
        await using var db = NewSqliteDb();
        var service = new BackupScheduleService(new BackupService(db));

        var before = BackupScheduleService.ReadState(_folder, nowUtc: Now);
        Assert.True(before.IsDue);

        var outcome = await service.RunIfDueAsync(_folder, isPostgres: false, nowUtc: Now);

        Assert.True(outcome.Ran);
        Assert.NotNull(outcome.Path);
        Assert.True(new FileInfo(outcome.Path!).Length > 0, "النسخةُ كُتبت فارغة.");
        Assert.False(outcome.State.IsDue);
        Assert.Null(outcome.Failure);
    }

    [Fact]
    public async Task WhenNotDue_ItWritesNothing()
    {
        await using var db = NewSqliteDb();
        var service = new BackupScheduleService(new BackupService(db));
        WriteBackup("brqosh_recent.db", Now.AddHours(-1));

        var outcome = await service.RunIfDueAsync(_folder, isPostgres: false, nowUtc: Now);

        Assert.False(outcome.Ran);
        Assert.Null(outcome.Failure);
        Assert.Single(Directory.GetFiles(_folder, "brqosh_*.db"));
    }

    [Fact]
    public async Task AFailingBackup_IsReportedNotThrown_AndTheStateStaysOverdue()
    {
        await using var db = NewSqliteDb();
        var service = new BackupScheduleService(new BackupService(db));

        // ‏مسارٌ لا يمكن أن يصير مجلّداً: ملفٌّ يحمل الاسمَ نفسَه.
        // ‏(‏"a/b" وحدَه ليس مستحيلاً — CreateDirectory يُنشئ الشجرةَ كلَّها، وهو ما أوقعني أوّلاً.)
        var blocker = Path.Combine(_folder, "blocked");
        File.WriteAllText(blocker, "x");
        var impossible = Path.Combine(blocker, "backups");

        var outcome = await service.RunIfDueAsync(impossible, isPostgres: false, nowUtc: Now);

        Assert.False(outcome.Ran);
        Assert.NotNull(outcome.Failure);
        Assert.True(outcome.State.IsOverdue);
    }
}
