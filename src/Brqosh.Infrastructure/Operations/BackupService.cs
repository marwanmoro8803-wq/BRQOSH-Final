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
    public sealed record BackupResult(string Path, long SizeBytes, string Provider);

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
    public async Task<BackupResult> CreateAsync(string targetPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)
            ?? throw new AccountingException("مسارُ النسخة الاحتياطية بلا مجلّد."));

        if (db.Database.IsNpgsql())
            await DumpPostgresAsync(targetPath, ct);
        else
            await VacuumSqliteAsync(targetPath, ct);

        var info = new FileInfo(targetPath);
        if (!info.Exists || info.Length == 0)
            throw new AccountingException(
                $"النسخةُ الاحتياطية لم تُكتب أو خرجت فارغة: {targetPath}");

        return new BackupResult(targetPath, info.Length,
            db.Database.IsNpgsql() ? "PostgreSQL" : "SQLite");
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

        if (db.Database.IsNpgsql())
            await RestorePostgresAsync(backupPath, ct);
        else
            await RestoreSqliteAsync(backupPath, ct);
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

        var stderr = await process.StandardError.ReadToEndAsync(ct);
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
