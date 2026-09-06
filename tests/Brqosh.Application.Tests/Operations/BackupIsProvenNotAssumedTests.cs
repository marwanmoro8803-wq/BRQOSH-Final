using Brqosh.Application.Tests.TestSupport;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (م-٣ · ن-٣١ / ن-٣٢) <b>نسخةٌ احتياطيّةٌ نصفيّةٌ كانت تُحسب نسخةً صالحة، ولا شيءَ يتحقّق.</b>
///
/// <para><b>ن-٣١:</b> <c>CreateAsync</c> كان يكتب في مكان الهدف مباشرةً. و<c>RunAsync</c>
/// يرمي عند رمزِ خروجٍ غيرِ صفر — <b>لكنّ ما كتبه <c>pg_dump</c> قبل أن يفشل يبقى على
/// القرص</b>. ثمّ يقرؤه <c>BackupScheduleService</c> فيجده أكبرَ من صفر ويعدّه نسخة،
/// <b>فيُطفئ إنذارَ «تأخّر النسخ» إلى الأبد</b>. <b>وهو أسوأُ من غياب النسخة، إذ يمنع
/// الشكوى</b> — والمنشأةُ تكتشف يومَ الحاجة أنّ حمايتَها كانت ملفّاً لا يُفتح.</para>
///
/// <para><b>ن-٣٢:</b> ولم يكن في الكود المشحون ما يتحقّق أنّ نسخةً تُستعاد، <b>واستدعاءُ
/// <c>pg_restore</c> الوحيد متلِفٌ فوق القاعدة الحيّة</b>. فلا سبيلَ لاختبار نسخةٍ إلّا
/// بتدمير الإنتاج — <b>فلا تُختبَر أبداً</b>. وأخطرُ من ذلك: الاستعادةُ من ملفٍّ تالفٍ
/// كانت <b>تحذف قبل أن تكتشف</b>، فتُنتج محاولةُ التعافي عطلاً أكبرَ ولا طريقَ رجوع.</para>
///
/// <para><b>وهذا الملفُّ يقيس على SQLite عمداً</b> — لا لأنّه المزوّدُ الأهمّ بل لأنّ
/// المقيسَ هنا <b>سلوكُ الخدمة لا سلوكُ المحرّك</b>: أين تُكتب، ومتى تُنقل، وماذا يبقى
/// عند الفشل. وهويّةُ الاستعادة على PostgreSQL مقيسةٌ في <c>BackupRestoreIdentityTests</c>.</para>
/// </summary>
public sealed class BackupIsProvenNotAssumedTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "brqosh-backup-" + Guid.NewGuid().ToString("N")[..8]);

    public BackupIsProvenNotAssumedTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true); }
        catch (IOException) { /* ‏تنظيفٌ لا يُسقط اختباراً */ }
        GC.SuppressFinalize(this);
    }

    // ── ن-٣١ · بقايا المحاولة المقطوعة ───────────────────────────────────────

    /// <summary>
    /// <b>القياسُ المركزيّ لـن-٣١:</b> بقايا محاولةٍ مقطوعةٍ لا تُطفئ الإنذار.
    ///
    /// <para>والعلاجُ <b>بنيويٌّ لا فحصيّ</b>: الملفُّ المرحليُّ ينتهي بـ<c>.partial</c>،
    /// و<c>NewestBackupUtc</c> يبحث بنمطَي <c>brqosh_*.dump</c> و<c>brqosh_*.db</c> —
    /// <b>فلا يُطابقه أيٌّ منهما</b>. فبقيّةٌ مقطوعةٌ تصير غيرَ معدودةٍ بالتصميم، لا بشرطِ
    /// حجمٍ يُنسى أو يُخفَّف.</para>
    /// </summary>
    [Fact]
    public void APartialRemnant_DoesNotSilenceTheOverdueAlarm()
    {
        // ‏ملفٌّ بحجمٍ معتبَر — فشرطُ `Length == 0` وحدَه كان يمرّره.
        File.WriteAllBytes(Path.Combine(_folder, "brqosh_20260101_000000.dump.partial"),
            new byte[4096]);

        var state = BackupScheduleService.ReadState(_folder, nowUtc: new DateTime(2026, 6, 1));

        Assert.Null(state.LastBackupUtc);
        Assert.True(state.IsOverdue,
            "‏بقيّةٌ مقطوعةٌ أطفأت إنذارَ «تأخّر النسخ» — والمنشأةُ تظنّ نفسَها محميّةً بلا نسخة.");
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وبدونه القياسُ أعلاه خبرٌ عن مجلّدٍ فارغ.</b>
    /// نسخةٌ باسمها الصحيح <b>تُعدّ</b> وتُطفئ الإنذار.
    /// </summary>
    [Fact]
    public void ARealBackupFile_IsCounted_SoTheScanIsNotBlind()
    {
        var path = Path.Combine(_folder, "brqosh_20260101_000000.dump");
        File.WriteAllBytes(path, new byte[4096]);
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 5, 31));

        var state = BackupScheduleService.ReadState(_folder, nowUtc: new DateTime(2026, 6, 1));

        Assert.NotNull(state.LastBackupUtc);
        Assert.False(state.IsOverdue);
    }

    /// <summary>
    /// <b>ونسخةٌ ناجحةٌ لا تترك ملفّاً مرحليّاً وراءها</b> — فبقاؤه يُراكم نصفَ نسخةٍ
    /// عن كلّ تشغيلٍ حتى يمتلئ القرص، وهو ما يُنتج الفشلَ الذي جاء يحرس منه.
    /// </summary>
    [Fact]
    public async Task ASuccessfulBackup_LeavesNoStagingFileBehind()
    {
        var target = await TakeABackupAsync();

        Assert.True(File.Exists(target));
        Assert.False(File.Exists(target + ".partial"),
            "‏بقي الملفُّ المرحليُّ بعد نجاح النسخ.");
        Assert.Empty(Directory.GetFiles(_folder, "*.partial"));
    }

    /// <summary>
    /// <b>القياسُ الذي يُعيد إنتاج عطلِ ن-٣١ نفسِه</b> — لا الثابتَ الذي حلّ محلَّه.
    ///
    /// <para><b>ولمَ يلزم:</b> الاختباراتُ الثلاثةُ أعلاه تُثبّت الترتيبَ الجديد، <b>وتمرّ
    /// على الكود المعطوب كذلك</b> — قِيس ذلك في شجرةٍ معزولةٍ رُدَّ فيها السلوكُ القديم.
    /// <b>وكناريٌّ يمرّ على العطل زينةٌ لا حارس.</b></para>
    ///
    /// <para><b>والعطلُ يُصنع كما وقع:</b> <c>pg_dump</c> يُنشئ ملفَّ الخرج <b>ثمّ</b> يفشل
    /// حين لا تكون القاعدةُ موجودة. فتبقى بقيّةٌ باسمٍ يعدّه <c>NewestBackupUtc</c> نسخةً
    /// صالحة. وبالترتيب الجديد تقع البقيّةُ على الاسم المرحليّ ويمحوها <c>finally</c>،
    /// <b>ولا يُنشأ الاسمُ المعدودُ إلّا بعد اجتياز التحقّق</b>.</para>
    /// </summary>
    [PostgresFact]
    public async Task AFailedDump_LeavesNothingThatCountsAsABackup()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync("backupfail");

        // ‏قاعدةٌ لا وجودَ لها — والمضيفُ والاعتمادُ صحيحان، فيبلغ `pg_dump` الخادمَ ويفشل عنده.
        var doomed = new Npgsql.NpgsqlConnectionStringBuilder(scope.Db.Database.GetConnectionString())
        {
            Database = "brqosh_absent_" + Guid.NewGuid().ToString("N")[..12],
        }.ToString();

        var target = Path.Combine(_folder, "brqosh_20260101_000000.dump");

        await using var db = BrqoshDbContextFactory.Create(DatabaseProvider.PostgreSql, doomed);
        await Assert.ThrowsAsync<AccountingException>(
            () => new BackupService(db).CreateAsync(target));

        Assert.False(File.Exists(target),
            "‏بقيت بقيّةُ محاولةٍ فاشلةٍ على الاسم المعدود — وهي التي تُطفئ إنذارَ تأخّر النسخ.");

        var state = BackupScheduleService.ReadState(_folder, nowUtc: new DateTime(2026, 6, 1));
        Assert.Null(state.LastBackupUtc);
        Assert.True(state.IsOverdue);
    }

    // ── ن-٣٢ · التحقّقُ قبل الإتلاف ──────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب:</b> نسخةٌ حقيقيّةٌ تجتاز التحقّق.
    ///
    /// <para>وهو ألزمُ من الرفض: متحقّقٌ يرفض كلَّ شيءٍ يمنع الاستعادةَ تماماً — وذلك
    /// عطلٌ أشدُّ من الذي جاء يُصلحه.</para>
    /// </summary>
    [Fact]
    public async Task AGenuineBackup_PassesVerification()
    {
        var target = await TakeABackupAsync();

        await using var db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, $"Data Source={LivePath()}");

        await new BackupService(db).VerifyAsync(target);
    }

    /// <summary><b>وملفٌّ تالفٌ يُرفض</b> — والحجمُ كان الدليلَ الوحيدَ قبل اليوم.</summary>
    [Fact]
    public async Task ACorruptFile_FailsVerification_EvenThoughItIsNotEmpty()
    {
        var corrupt = Path.Combine(_folder, "brqosh_20260101_000000.db");
        await File.WriteAllTextAsync(corrupt, new string('x', 8192));

        await using var db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, $"Data Source={LivePath()}");

        var ex = await Assert.ThrowsAsync<AccountingException>(
            () => new BackupService(db).VerifyAsync(corrupt));

        Assert.Contains("النسخة الاحتياطية", ex.Message);
    }

    /// <summary>
    /// <b>القياسُ المركزيّ لـن-٣٢:</b> استعادةٌ من ملفٍّ تالفٍ تُرفض، <b>والقاعدةُ الحيّةُ
    /// تبقى كما هي</b>.
    ///
    /// <para>وهذا هو الفرقُ كلُّه. فالمسارُ القديم يحذف ثمّ يكتشف: على PostgreSQL
    /// <c>--clean --if-exists</c> يُسقط الجداولَ أوّلاً، وعلى SQLite <c>File.Copy</c>
    /// يدهس القاعدةَ الحيّة. <b>فمحاولةُ التعافي من عطلٍ كانت تُنتج عطلاً أكبر</b> — وبعد
    /// الحذفِ لا طريقَ رجوع.</para>
    /// </summary>
    [Fact]
    public async Task ARestoreFromACorruptFile_IsRefused_AndTheLiveDatabaseSurvives()
    {
        var live = LivePath();

        // ‏صفٌّ شاهدٌ في القاعدة الحيّة — **ولا يُقاس حجمُ الملفّ**: فتحُ SQLite وحدَه
        // ‏يُنمّي الملفَّ (تخصيصُ صفحاتٍ و‑wal)، فالحجمُ يتغيّر بلا أن يُدهس شيء.
        // ‏والمقصودُ بـ«نجت» أن يبقى ما فيها، لا أن تبقى بالبايت نفسِه.
        await using (var seed = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, $"Data Source={live}"))
        {
            await seed.Database.EnsureCreatedAsync();
            await seed.Database.ExecuteSqlRawAsync(
                "CREATE TABLE restore_canary(id INTEGER); INSERT INTO restore_canary VALUES (7);");
        }

        var corrupt = Path.Combine(_folder, "brqosh_20260101_000000.db");
        await File.WriteAllTextAsync(corrupt, new string('x', 8192));

        await using (var db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, $"Data Source={live}"))
        {
            await Assert.ThrowsAsync<AccountingException>(
                () => new BackupService(db).RestoreAsync(corrupt));
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        Assert.True(File.Exists(live), "‏دُهست القاعدةُ الحيّةُ باستعادةٍ من ملفٍّ تالف.");

        await using var reopened = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, $"Data Source={live}");
        var survived = await reopened.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM restore_canary")
            .SingleAsync();

        Assert.True(survived == 1,
            "‏ضاع الصفُّ الشاهد — فالاستعادةُ من ملفٍّ تالفٍ أتلفت القاعدةَ الحيّة.");
    }

    // ── مساعدات ──────────────────────────────────────────────────────────────

    private string LivePath() => Path.Combine(_folder, "live.db");

    private async Task<string> TakeABackupAsync()
    {
        var live = LivePath();
        await using (var seed = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, $"Data Source={live}"))
        {
            await seed.Database.EnsureCreatedAsync();
        }

        var target = Path.Combine(_folder, "brqosh_20260101_000000.db");

        await using var db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, $"Data Source={live}");
        var result = await new BackupService(db).CreateAsync(target);

        Assert.True(result.SizeBytes > 0);
        return target;
    }
}
