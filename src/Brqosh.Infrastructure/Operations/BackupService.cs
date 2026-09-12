using System.Diagnostics;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// (CEEF-229 · م3-٢) <b>نسخٌ احتياطيّ واستعادة — وكانا غائبَين عن المنتج كلِّه.</b>
///
/// <para><b>البند:</b> بحثٌ في المستودع عن <c>Backup</c> و<c>Restore</c> و<c>pg_dump</c>
/// أعاد صفراً. ومنشأةٌ تُمسك دفاترَها الحقيقية بلا طريقِ عودة لا تُسلَّم — والعطلُ هنا
/// ليس خطأً في حساب، بل <b>فقدُ الدفاتر كلِّها</b> عند أوّل قرصٍ يتلف.</para>
///
/// <para><b>والتعريفُ الحاكم:</b> النسخةُ الاحتياطية ليست ملفّاً يُكتب، بل <b>ملفٌّ يُثبَت
/// أنّه يُستعاد</b>. ولذلك يُقاس هذا الملفّ بهوية <b>ح-٦</b>: تُؤخذ نسخة، ثمّ <b>تُتلَف
/// القاعدة</b>، ثمّ تُستعاد، ثمّ تُطابَق بصمةُ الدفاتر. ونسخةٌ لا تجتاز ذلك أمنيةٌ لا نسخة.</para>
/// </summary>
public sealed class BackupService(BrqoshDbContext db)
{
    /// <param name="Pruned">
    /// (م-٤ · ن-٣٦) عددُ النسخ القديمة التي حُذفت في هذه العمليّة.
    /// </param>
    /// <param name="PruneFailures">
    /// ما تعذّر حذفُه — <b>يُعدّ ولا يُبتلع</b>. فملفٌّ مقفلٌ لا يُسقط النسخةَ التي تمّت،
    /// <b>لكنّ تكرارَه يعني أنّ التنظيفَ لا يعمل والقرصَ يمتلئ رغم الحدّ</b>.
    /// </param>
    public sealed record BackupResult(
        string Path, long SizeBytes, string Provider, int Pruned = 0, int PruneFailures = 0);

    /// <summary>
    /// (م-٤ · ن-٣٦) <b>كم نسخةً محلّيّةً تبقى.</b>
    ///
    /// <para><b>والعددُ قرارُ مخاطرة لا ذوق:</b> مجلّدُ النسخ والقاعدةُ الحيّة على
    /// <b>جذر البيانات نفسِه</b> افتراضاً (<c>BrqoshDataRoot</c>) — أي القرصِ نفسِه.
    /// <b>فمجلّدٌ ينمو بلا حدٍّ يُوقف القاعدةَ التي وُجد ليحميها.</b> وأربعَ عشرةَ نسخةً
    /// تُبقي أسبوعين من نقاط الاستعادة بجدولٍ يوميّ — وهو ما يكفي لاكتشاف فسادٍ صامتٍ
    /// والرجوعِ قبله، بلا أن يصير المجلّدُ هو الخطر.</para>
    /// </summary>
    internal const int LocalBackupsKept = 14;

    /// <summary>
    /// يكتب نسخةً احتياطيةً كاملة إلى <paramref name="targetPath"/>.
    ///
    /// <para><b>PostgreSQL:</b> <c>pg_dump --format=custom</c> — صيغةٌ تسمح لـ<c>pg_restore</c>
    /// بـ<c>--clean</c> فتُعيد البناءَ فوق قاعدةٍ قائمة، ولا تحتاج تحريرَ نصٍّ يدوياً.</para>
    ///
    /// <para><b>SQLite:</b> <c>VACUUM INTO</c> — نسخةٌ <b>متّسقةٌ ذرّياً</b> يأخذها المحرّك
    /// نفسُه. ونسخُ الملفّ بـ<c>File.Copy</c> بينما التطبيقُ يعمل يُنتج ملفّاً مقطوعاً في
    /// منتصف معاملة، أو يُهمل ملفَّ <c>-wal</c> فيسقط ما لم يُدمَج بعد — وهي نسخةٌ تبدو
    /// سليمةً ولا تُفتح.</para>
    /// </summary>
    /// <summary>
    /// (م-٣ · ن-٣١) <b>لاحقةُ الملفّ المرحليّ — وهي نصفُ العلاج، بنيةً لا فحصاً.</b>
    ///
    /// <para><c>NewestBackupUtc</c> يبحث بنمطَي <c>brqosh_*.dump</c> و<c>brqosh_*.db</c>.
    /// واسمٌ ينتهي بـ<c>.partial</c> <b>لا يُطابق أيّاً منهما</b> — فبقايا محاولةٍ مقطوعةٍ
    /// تصير <b>غيرَ معدودةٍ بالتصميم</b>، لا بفحصٍ يُنسى أو يُخفَّف.</para>
    /// </summary>
    private const string StagingSuffix = ".partial";

    public async Task<BackupResult> CreateAsync(string targetPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)
            ?? throw new AccountingException("مسارُ النسخة الاحتياطية بلا مجلّد."));

        // ‏(م-٣ · ن-٣١) ‏**تُكتب النسخةُ جانباً، وتُثبَت، ثمّ تُنقل باسمها.**
        //
        // ‏كانت تُكتب في مكانها مباشرةً. و`RunAsync` يرمي عند رمزِ خروجٍ غيرِ صفر —
        // ‏**لكنّ ما كتبه `pg_dump` قبل أن يفشل يبقى على القرص**. ثمّ يقرؤه
        // ‏`BackupScheduleService.NewestBackupUtc` فيجده أكبرَ من صفر ويعدّه نسخة،
        // ‏**فيُطفئ إنذارَ «تأخّر النسخ» إلى الأبد** — والمنشأةُ تظنّ نفسَها محميّةً
        // ‏بملفٍّ لا يُستعاد. **وهو أسوأُ من غياب النسخة، إذ يمنع الشكوى.**
        //
        // ‏وتعليقُ `NewestBackupUtc` نفسُه كان يعترف بالخطر — «بقايا محاولةٍ مقطوعةٍ قد
        // ‏تبقى في المجلّد فتُوهم بحماية» — ثمّ يحرس `Length == 0` وحدَه.
        var staging = targetPath + StagingSuffix;

        try
        {
            if (File.Exists(staging)) File.Delete(staging);

            if (db.Database.IsNpgsql())
                await DumpPostgresAsync(staging, ct);
            else
                await VacuumSqliteAsync(staging, ct);

            var info = new FileInfo(staging);
            if (!info.Exists || info.Length == 0)
                throw new AccountingException(
                    $"النسخةُ الاحتياطية لم تُكتب أو خرجت فارغة: {targetPath}");

            // ‏**والحجمُ ليس دليلاً.** يُسأل المحرّكُ نفسُه: أتقرأ هذا الملفَّ أم لا؟
            await VerifyAsync(staging, ct);

            File.Move(staging, targetPath, overwrite: true);

            // ‏(م-٤ · ن-٣٦) ‏**التنظيفُ بعد الترقية لا قبلها** — وهو مبدأٌ مكتوبٌ في
            // ‏`OffsiteBackupService`: «تنظيفُ القديم ثمّ فشلُ الجديد **يترك المنشأةَ بلا
            // ‏نسخةٍ واحدة**». فلا يُحذف شيءٌ حتى تُثبَت الجديدةُ وتُنقل باسمها.
            //
            // ‏**وهنا لا في المُجدوِل:** المجلّدُ له **مُنتِجان** — المُجدوِل كلَّ أربعٍ
            // ‏وعشرين ساعة، وزرُّ النسخ اليدويُّ عند كلّ ضغطة — وكلاهما يكتب باسمٍ يحمل
            // ‏لحظتَه فلا يُدهَس شيء. **وحدٌّ في المُجدوِل وحدَه يترك الثانيَ بلا حدّ**،
            // ‏و`CreateAsync` هو المصبُّ الذي يمرّان به معاً.
            var (pruned, pruneFailures) = PruneOldBackups(targetPath);

            var promoted = new FileInfo(targetPath);
            return new BackupResult(targetPath, promoted.Length,
                db.Database.IsNpgsql() ? "PostgreSQL" : "SQLite", pruned, pruneFailures);
        }
        finally
        {
            // ‏لا يبقى نصفُ نسخةٍ على القرص بحالٍ — نجحت العمليةُ أم أخفقت.
            try { if (File.Exists(staging)) File.Delete(staging); }
            catch (IOException) { /* ‏تنظيفٌ لا يُبدّل حكمَ العملية */ }
            catch (UnauthorizedAccessException) { /* ‏كذلك */ }
        }
    }

    /// <summary>
    /// <b>يستعيد فوق القاعدة الحالية — عمليةٌ متلِفة لا رجعةَ فيها.</b>
    ///
    /// <para>لا يُستدعى إلا بعد تأكيدٍ صريحٍ من المالك. ومسؤوليةُ التأكيد على المستدعي:
    /// هذه الطبقةُ تُنفّذ ولا تسأل.</para>
    /// </summary>
    public async Task RestoreAsync(string backupPath, CancellationToken ct = default)
    {
        if (!File.Exists(backupPath))
            throw new AccountingException($"ملفُّ النسخة الاحتياطية غير موجود: {backupPath}");

        // ‏(م-٣ · ن-٣٢) ‏**يُقرأ الملفُّ قبل أن تُتلَف القاعدة، لا بعدها.**
        //
        // ‏`pg_restore --clean --if-exists` **يحذف قبل أن يكتب**. فلو كان الملفُّ مقطوعاً
        // ‏لسقط في منتصف الطريق **وقد أُتلفت القاعدةُ أصلاً** — أي أنّ محاولةَ التعافي من
        // ‏عطلٍ تُنتج عطلاً أكبر، ولا طريقَ رجوع.
        //
        // ‏**ولا يُضاف `--exit-on-error` إلى الأمر المتلِف:** إيقافُه عند أوّل خطأٍ يترك
        // ‏القاعدةَ **نصفَ مستعادة**، وذلك أسوأُ من إتمامٍ يُبلّغ أخطاءه. فالحارسُ يقع
        // ‏**قبل** الإتلاف لا داخلَه.
        await VerifyAsync(backupPath, ct);

        if (db.Database.IsNpgsql())
            await RestorePostgresAsync(backupPath, ct);
        else
            await RestoreSqliteAsync(backupPath, ct);
    }

    /// <summary>
    /// (م-٤ · ن-٣٦) يحذف الأقدمَ ويُبقي <see cref="LocalBackupsKept"/>.
    ///
    /// <para><b>والامتدادُ جزءٌ من النمط</b> — على سابقة <c>OffsiteBackupService</c>: مجلّدٌ
    /// يجمع نسخَ قاعدتين لا تُحذف نسخُ إحداهما بتنظيف الأخرى.</para>
    ///
    /// <para><b>وإخفاقُ الحذف يُعدّ ولا يُبتلع.</b> ملفٌّ مقفلٌ لا يُسقط نسخةً تمّت —
    /// <b>لكنّ صمتاً عنه يجعل مجلّداً لا يُنظَّف يبدو منظَّفاً</b>، فيمتلئ القرصُ رغم
    /// وجود الحدّ. والعددُ يُعاد في الحصيلة ليُرى.</para>
    /// </summary>
    private static (int Pruned, int Failed) PruneOldBackups(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath);
        var extension = Path.GetExtension(targetPath);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(extension))
            return (0, 0);

        var stale = new DirectoryInfo(directory)
            .GetFiles($"brqosh_*{extension}")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Skip(LocalBackupsKept)
            .ToList();

        var pruned = 0;
        var failed = 0;
        foreach (var file in stale)
        {
            try
            {
                file.Delete();
                pruned++;
            }
            catch (IOException)
            {
                failed++;
            }
            catch (UnauthorizedAccessException)
            {
                failed++;
            }
        }

        return (pruned, failed);
    }

    /// <summary>
    /// (م-٣ · ن-٣٢) <b>يتحقّق أنّ ملفَّ نسخةٍ يُقرأ — بلا لمسِ قاعدةٍ حيّة.</b>
    ///
    /// <para><b>البند:</b> لم يكن في الكود المشحون ما يتحقّق أنّ نسخةً تُستعاد،
    /// <b>واستدعاءُ <c>pg_restore</c> الوحيد متلِفٌ فوق القاعدة الحيّة</b>
    /// (<c>--clean --if-exists</c> على <c>--dbname</c> من سلسلة الاتصال نفسِها).
    /// فلا سبيلَ لاختبار نسخةٍ إلّا بتدمير الإنتاج — <b>فلا تُختبَر أبداً</b>، وتبقى
    /// «النسخةُ الاحتياطيّة» أمنيةً حتى يوم الحاجة.</para>
    ///
    /// <para><b>و<c>pg_restore --list</c> يقرأ فهرسَ الأرشيف ولا يتّصل بخادمٍ إطلاقاً</b> —
    /// فيكشف المقطوعَ والتالفَ وما ليس أرشيفاً أصلاً. وهو ما يُميّز «ملفٌّ أكبرُ من صفر»
    /// عن «أرشيفٌ يُقرأ».</para>
    ///
    /// <para><b>وحدُّه مُعلَن:</b> يُثبت سلامةَ الفهرس وبنيةِ الأرشيف. <b>ولا يُثبت أنّ كلّ
    /// صفٍّ فيه سليم</b> — ذلك يلزمه استعادةٌ فعليّةٌ إلى قاعدةٍ منفصلة، وهي هويّةُ ح-٦
    /// المقيسة في <c>BackupRestoreIdentityTests</c>.</para>
    /// </summary>
    public async Task VerifyAsync(string backupPath, CancellationToken ct = default)
    {
        if (!File.Exists(backupPath))
            throw new AccountingException($"ملفُّ النسخة الاحتياطية غير موجود: {backupPath}");

        if (db.Database.IsNpgsql())
            await VerifyPostgresArchiveAsync(backupPath, ct);
        else
            await VerifySqliteFileAsync(backupPath, ct);
    }

    // ── PostgreSQL ───────────────────────────────────────────────────────────

    private async Task DumpPostgresAsync(string targetPath, CancellationToken ct)
    {
        var tool = PgToolLocator.Find("pg_dump", out var searched)
            ?? throw new AccountingException(BuildToolMissingMessage("pg_dump", searched));

        var builder = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        await RunAsync(tool.Path,
            ["--format=custom", "--no-owner", "--no-privileges",
             $"--host={builder.Host}", $"--port={builder.Port}",
             $"--username={builder.Username}", $"--dbname={builder.Database}",
             $"--file={targetPath}"],
            builder.Password, "pg_dump", ct);
    }

    private async Task RestorePostgresAsync(string backupPath, CancellationToken ct)
    {
        var tool = PgToolLocator.Find("pg_restore", out var searched)
            ?? throw new AccountingException(BuildToolMissingMessage("pg_restore", searched));

        var builder = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());

        // ‏الاتصالاتُ المفتوحة تمنع الحذف — ويُغلق التجمّعُ قبل الاستعادة.
        NpgsqlConnection.ClearAllPools();

        await RunAsync(tool.Path,
            ["--clean", "--if-exists", "--no-owner", "--no-privileges",
             $"--host={builder.Host}", $"--port={builder.Port}",
             $"--username={builder.Username}", $"--dbname={builder.Database}",
             backupPath],
            builder.Password, "pg_restore", ct);
    }

    // ── SQLite ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>pg_restore --list</c> — يقرأ فهرسَ الأرشيف ولا يتّصل بخادم. ولا يُمرَّر
    /// <c>--dbname</c> ولا مضيفٌ ولا كلمةُ مرور: <b>ما لا يُمرَّر لا يُتلَف</b>.
    /// </summary>
    private static async Task VerifyPostgresArchiveAsync(string backupPath, CancellationToken ct)
    {
        var tool = PgToolLocator.Find("pg_restore", out var searched)
            ?? throw new AccountingException(BuildToolMissingMessage("pg_restore", searched));

        try
        {
            await RunAsync(tool.Path, ["--list", backupPath], password: null, "pg_restore --list", ct);
        }
        catch (AccountingException ex)
        {
            throw new AccountingException(
                $"ملفُّ النسخة الاحتياطية لا يُقرأ أرشيفاً صالحاً: {backupPath}"
                + " — ولا يُستعاد ما لا يُقرأ. والاستعادةُ لم تبدأ، فالقاعدةُ الحاليّة سليمة. "
                + ex.Message, ex);
        }
    }

    /// <summary>
    /// <c>PRAGMA integrity_check</c> على الملفّ نفسِه.
    ///
    /// <para><b>والتجميعُ مُعطَّلٌ صراحةً</b> (<c>Pooling=False</c>) مع تفريغِ التجمّع بعده:
    /// اتصالٌ مُجمَّعٌ يُبقي مقبضَ الملفّ مفتوحاً بعد التخلّص منه، <b>فيفشل <c>File.Move</c>
    /// التالي على ويندوز</b> برميةِ مشاركة. <b>وهو عطلٌ لا يظهر على لينكس</b> فيمرّ في CI
    /// ويسقط عند العميل — وقد رُصد في تصميمٍ سابقٍ لهذا الإصلاح قبل كتابته.</para>
    /// </summary>
    private static async Task VerifySqliteFileAsync(string backupPath, CancellationToken ct)
    {
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();

        string? verdict;
        try
        {
            await using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check";
            verdict = (await command.ExecuteScalarAsync(ct))?.ToString();
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
        {
            throw new AccountingException(
                $"ملفُّ النسخة الاحتياطية لا يُفتح قاعدةً صالحة: {backupPath} — {ex.Message}", ex);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        }

        if (!string.Equals(verdict, "ok", StringComparison.OrdinalIgnoreCase))
            throw new AccountingException(
                $"فحصُ سلامة النسخة الاحتياطية ردّ «{verdict}» لا «ok»: {backupPath}");
    }

    private async Task VacuumSqliteAsync(string targetPath, CancellationToken ct)
    {
        if (File.Exists(targetPath))
            File.Delete(targetPath);

        // ‏المسارُ يُمرَّر معاملاً لا يُدمَج في النصّ — ولو دُمج لفُتح بابُ حقنٍ عبر اسم ملفّ.
        await db.Database.ExecuteSqlRawAsync(
            "VACUUM INTO {0}", new object[] { targetPath }, ct);
    }

    private async Task RestoreSqliteAsync(string backupPath, CancellationToken ct)
    {
        var live = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(
            db.Database.GetConnectionString()).DataSource;

        if (string.IsNullOrWhiteSpace(live) || live == ":memory:")
            throw new AccountingException(
                "الاستعادة على قاعدةٍ في الذاكرة لا معنى لها — القاعدةُ تزول بإغلاق التطبيق.");

        await db.Database.CloseConnectionAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        File.Copy(backupPath, live, overwrite: true);
        foreach (var sidecar in new[] { $"{live}-wal", $"{live}-shm" })
            if (File.Exists(sidecar))
                File.Delete(sidecar);

        await Task.CompletedTask;
    }

    // ── تشغيلُ الأداة ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>كلمةُ المرور في بيئة العملية لا في سطر الأوامر.</b> فسطرُ الأوامر يظهر في قائمة
    /// العمليات لأيّ مستخدمٍ على الجهاز — وتسريبُ كلمة مرور قاعدة الإنتاج ليس تفصيلاً.
    /// </summary>
    private static async Task RunAsync(
        string toolPath, string[] arguments, string? password, string label, CancellationToken ct)
    {
        var info = new ProcessStartInfo(toolPath)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        if (!string.IsNullOrEmpty(password))
            info.Environment["PGPASSWORD"] = password;

        using var process = Process.Start(info)
            ?? throw new AccountingException($"تعذّر تشغيل {label}.");

        // ‏(م-٣ · ن-٣٢) ‏**يُستنزف المجريان معاً — وإلّا تجمّدت الأداةُ في حاجز الأنبوب.**
        //
        // ‏كان `stdout` مُعاد التوجيه **ولا يُقرأ أبداً**. ومرّ ذلك سليماً لأنّ
        // ‏`pg_dump --file=` يكتب في ملفّ و`pg_restore --clean` لا يُخرج شيئاً يُذكر.
        // ‏**لكنّ `pg_restore --list` يكتب فهرسَ الأرشيف كلَّه في `stdout`**: يمتلئ الحاجز،
        // ‏فتتوقّف الأداةُ عن الكتابة، ولا تخرج، ولا يعود `ReadToEndAsync` على `stderr`.
        // ‏**جمودٌ تامّ** — رُصد بتشغيلٍ علق، لا بقراءةِ شيفرة.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        var stderr = await stderrTask;
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new AccountingException(
                $"فشل {label} (رمز {process.ExitCode}): {stderr.Trim()}");
    }

    private static string BuildToolMissingMessage(string tool, IReadOnlyList<string> searched) =>
        $"لم يُعثر على «{tool}» — وهو جزءٌ من تثبيت PostgreSQL ولا يعمل النسخُ الاحتياطيّ بدونه.\n"
        + "فُتّشت المواضع:\n- " + string.Join("\n- ", searched)
        + "\n\nثبِّت أدوات PostgreSQL، أو أضِف مجلّد bin إلى PATH.";
}
