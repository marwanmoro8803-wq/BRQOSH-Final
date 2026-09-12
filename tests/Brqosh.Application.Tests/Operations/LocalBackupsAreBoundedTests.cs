using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (م-٤ · ن-٣٦) <b>مجلّدُ النسخ المحلّيّ لم يكن يُقلَّم أبداً — والقرصُ الذي يملؤه هو قرصُ الدفتر.</b>
///
/// <para><b>والضررُ ليس «قرصَ المُشغِّل»:</b> <c>MainWindow</c> يضع النسخَ في
/// <c>BrqoshDataRoot.Path()/backups</c>، و<c>DbContextFactory</c> يضع قاعدةَ SQLite الحيّة
/// في <c>BrqoshDataRoot.Directory()/brqosh.db</c> — <b>الجذرُ نفسُه، فالقرصُ نفسُه</b>.
/// <b>فمجلّدٌ ينمو بلا حدٍّ يُوقف القاعدةَ التي وُجد ليحميها.</b></para>
///
/// <para><b>ومُنتِجان لا واحد:</b> المُجدوِل كلَّ أربعٍ وعشرين ساعة، وزرُّ النسخ اليدويُّ
/// عند كلّ ضغطة — وكلاهما يكتب في المجلّد نفسِه <b>باسمٍ يحمل لحظتَه فلا يُدهَس شيء</b>.
/// <b>فحدٌّ يوضع في المُجدوِل وحدَه يترك الثانيَ بلا حدّ</b> — ولذلك وُضع في
/// <c>BackupService.CreateAsync</c>، وهو المصبُّ الذي يمرّان به معاً.</para>
///
/// <para><b>ولمَ الاحتفاظُ لا فحصُ المساحة:</b> <c>CreateAsync</c> يمحو الملفَّ المرحليَّ
/// في <c>finally</c> على كلّ مسار، <b>فنسخةٌ تفشل على قرصٍ ممتلئ تُحرّر بايتاتها</b>.
/// <b>والذي يملأ القرصَ هو النسخةُ الناجحةُ المتراكمة</b>، لا الفاشلة. وحارسُ مساحةٍ
/// باحتياطيٍّ للقاعدة كان يُبدّل قرصاً ممتلئاً <b>بنسخٍ مرفوضةٍ دائماً بلا تجاوز</b> —
/// وذلك أسوأ.</para>
/// </summary>
public sealed class LocalBackupsAreBoundedTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "brqosh-retain-" + Guid.NewGuid().ToString("N")[..8]);

    public LocalBackupsAreBoundedTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true); }
        catch (IOException) { /* ‏تنظيفٌ لا يُسقط اختباراً */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>القياسُ المركزيّ — ويستدعي <c>CreateAsync</c> مباشرةً باسم الزرّ اليدويّ.</b>
    ///
    /// <para><b>وهذا شرطٌ لا تفصيل:</b> كناريٌّ يقود <c>RunIfDueAsync</c> وحدَه <b>يخضرّ على
    /// حدٍّ موضوعٍ داخل المُجدوِل</b> — وهو الإصلاحُ المُضلَّلُ الذي يترك الزرَّ اليدويَّ
    /// بلا حدّ. فلا يُفرَّق بين الإصلاحين إلّا بقيادة المصبِّ نفسِه.</para>
    /// </summary>
    [Fact]
    public async Task AManualBackup_PrunesTheFolderToTheRetentionLimit()
    {
        const int planted = 5;
        PlantOldBackups(BackupService.LocalBackupsKept + planted, "db");

        var result = await TakeABackupAsync("brqosh_2026-06-01_1200.db");

        // ‏المزروعةُ + الجديدةُ − المحفوظُ. ويُشتقّ ولا يُثبَّت رقماً: تغييرُ الحدّ لاحقاً
        // ‏يجب أن يُبقيَ القياسَ صحيحاً، لا أن يُسقطه لسببٍ لا علاقةَ له بالعطل.
        Assert.Equal(BackupService.LocalBackupsKept, Directory.GetFiles(_folder, "brqosh_*.db").Length);
        Assert.Equal(planted + 1, result.Pruned);
        Assert.Equal(0, result.PruneFailures);
    }

    /// <summary>
    /// <b>والأقدمُ هو ما يُحذف</b> — لا «خمسةٌ ما».
    ///
    /// <para>فحدٌّ يحذف الأحدثَ يُبقي العددَ صحيحاً <b>ويُتلف نقطةَ الاستعادة الوحيدة التي
    /// تنفع</b>. والعددُ وحدَه لا يكشف ذلك.</para>
    /// </summary>
    [Fact]
    public async Task ThePrune_RemovesTheOldest_AndKeepsTheNewest()
    {
        var planted = PlantOldBackups(BackupService.LocalBackupsKept + 3, "db");
        var oldest = planted[^1];
        var newestPlanted = planted[0];

        await TakeABackupAsync("brqosh_2026-06-01_1200.db");

        Assert.False(File.Exists(oldest), "‏بقي الأقدمُ — فالترتيبُ معكوس.");
        Assert.True(File.Exists(newestPlanted), "‏حُذف الأحدثُ — فالحدُّ يُتلف ما ينفع.");
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب:</b> نسخُ مزوّدٍ آخر في المجلّد نفسِه <b>لا تُمسّ</b>.
    ///
    /// <para>والامتدادُ جزءٌ من النمط — على سابقة <c>OffsiteBackupService</c>: مجلّدٌ يجمع
    /// نسخَ قاعدتين <b>لا تُحذف نسخُ إحداهما بتنظيف الأخرى</b>.</para>
    /// </summary>
    [Fact]
    public async Task ThePrune_DoesNotTouchAnotherProvidersBackups()
    {
        PlantOldBackups(BackupService.LocalBackupsKept + 5, "db");
        var dumps = PlantOldBackups(3, "dump");

        await TakeABackupAsync("brqosh_2026-06-01_1200.db");

        Assert.All(dumps, d => Assert.True(File.Exists(d),
            "‏حُذفت نسخةُ مزوّدٍ آخر — وحذفٌ يتجاوز نمطَه يُتلف نسخَ غيره."));
    }

    /// <summary>
    /// <b>وشاهدٌ ثانٍ:</b> مجلّدٌ دون الحدّ لا يُحذف منه شيء.
    ///
    /// <para>فبغيره لا يُميَّز «تقليمٌ يعمل» عن «حذفٌ في كلّ مرّة».</para>
    /// </summary>
    [Fact]
    public async Task AFolderUnderTheLimit_LosesNothing()
    {
        PlantOldBackups(3, "db");

        var result = await TakeABackupAsync("brqosh_2026-06-01_1200.db");

        Assert.Equal(0, result.Pruned);
        Assert.Equal(4, Directory.GetFiles(_folder, "brqosh_*.db").Length);
    }

    /// <summary>
    /// <b>والمُجدوِل يمرّ بالمصبِّ نفسِه</b> — تغطيةٌ لا تمييز.
    ///
    /// <para>ويُقال صراحةً: هذا التوكيدُ <b>يخضرّ كذلك على حدٍّ موضوعٍ داخل المُجدوِل</b>،
    /// فلا يُميّز الإصلاحَ الصحيحَ وحدَه. والمُميِّزُ هو الأوّل.</para>
    /// </summary>
    [Fact]
    public async Task TheScheduler_AlsoPrunes_AndReportsWhatItDeleted()
    {
        const int planted = 2;
        PlantOldBackups(BackupService.LocalBackupsKept + planted, "db");
        var live = await SeedLiveAsync();

        await using var db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, $"Data Source={live}");

        var outcome = await new BackupScheduleService(new BackupService(db))
            .RunIfDueAsync(_folder, isPostgres: false, nowUtc: new DateTime(2026, 6, 1));

        Assert.True(outcome.Ran, outcome.Failure ?? "‏لم تُنفَّذ النسخة.");
        Assert.Equal(planted + 1, outcome.Pruned);
    }

    // ── مساعدات ──────────────────────────────────────────────────────────────

    /// <summary>يزرع نسخاً بطوابعَ متمايزةٍ — الأحدثُ أوّلاً في القائمة المُعادة.</summary>
    private List<string> PlantOldBackups(int count, string extension)
    {
        var planted = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(_folder, $"brqosh_old{i:D3}.{extension}");
            File.WriteAllBytes(path, new byte[1024]);
            File.SetLastWriteTimeUtc(path, new DateTime(2026, 5, 1).AddMinutes(-i));
            planted.Add(path);
        }

        return planted;
    }

    private async Task<string> SeedLiveAsync()
    {
        var live = Path.Combine(_folder, "live.db");
        if (File.Exists(live)) return live;

        await using var seed = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, $"Data Source={live}");
        await seed.Database.EnsureCreatedAsync();
        return live;
    }

    private async Task<BackupService.BackupResult> TakeABackupAsync(string fileName)
    {
        var live = await SeedLiveAsync();

        await using var db = BrqoshDbContextFactory.Create(
            DatabaseProvider.Sqlite, $"Data Source={live}");

        return await new BackupService(db).CreateAsync(Path.Combine(_folder, fileName));
    }
}
