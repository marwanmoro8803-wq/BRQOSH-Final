using System.Diagnostics;
using System.IO.Compression;
using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// ‏(م-٥ · ن-٤٤) <b>«تعذّر أن أسأل» ليس «سألتُ فكان فاسداً».</b>
///
/// <para>غيابُ <c>pg_restore</c> عطلُ بيئةٍ لا حكمٌ على ملفّ. ولولا تمييزُه لجمعه
/// <see cref="BackupService.NewestVerifiedAsync"/> مع أحكام الفساد، <b>فقيل للمالك إنّ
/// نسخَه كلَّها فاسدة</b> — بعدد ما في المجلّد — والسببُ أنّ أداةً غيرُ مُنصَّبة.</para>
///
/// <para>ويرث <see cref="AccountingException"/> فلا يكسر ملتقِطاً قائماً.</para>
/// </summary>
public sealed class BackupVerifierUnavailableException(string message)
    : AccountingException(message);

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
public sealed class BackupService(BrqoshDbContext db, string? preRestoreFolder = null)
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

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-٥) <b>رفيقُ النسخة — ما لا تحمله القاعدة.</b>
    ///
    /// <para><b>البند:</b> النسخةُ الاحتياطيّة كانت <b>القاعدةَ وحدَها</b>. وأرشيفُ فواتير
    /// ZATCA ومفتاحُ التوقيع وشهادتُه <b>ملفّاتٌ على القرص لا صفوفٌ في جدول</b> — فاستعادةٌ
    /// كاملةُ النجاح تُنتج نظاماً <b>لا يستطيع التوقيع ولا إعادةَ إرسال المعلّق</b>،
    /// <b>وقد فقد الأرشيفَ الذي يُلزِم به النظام</b>. والمفتاحُ الخاصُّ لا يُولَّد من جديد:
    /// ضياعُه يعني تسجيلاً جديداً لدى الهيئة.</para>
    ///
    /// <para><b>ولاحقةٌ لا امتدادٌ بديل</b> — فيُرى الرفيقُ بجوار نسختِه، ويُحذف معها في
    /// التنظيف، ويُنسخ معها إلى الوجهات الخارجيّة.</para>
    /// </summary>
    internal const string CompanionSuffix = ".zatca.zip";

    /// <summary>شجرةُ ملفّات ZATCA تحت جذر البيانات — الأرشيفُ والمفاتيحُ معاً.</summary>
    internal static string CompanionSourceDirectory() =>
        Path.Combine(BrqoshDataRoot.Path(), "zatca");

    /// <summary>
    /// ‏(م-٥ · ن-٤٤) ‏مهلةٌ قبل كنسِ بقيّةٍ مرحليّة — <b>فملفٌّ يُكتب الآن ليس يتيماً</b>.
    /// وستُّ ساعاتٍ أطولُ من أيّ تفريغٍ واقعيّ، وأقصرُ من أن يبيت اليتيمُ ليلتين.
    /// </summary>
    private static readonly TimeSpan StagingOrphanGrace = TimeSpan.FromHours(6);

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

            // ‏(ح-٥) ‏**ثمّ الرفيقُ — وفشلُه يُسقط النسخة.**
            //
            // ‏وهو حكمُ هذا الملفّ نفسِه في `.partial`: نسخةٌ ناقصةٌ تقول «تمّت» **أسوأُ من
            // ‏غياب النسخة، إذ تمنع الشكوى**. فلو بقي الرفيقُ اختياريّاً لعاد العطلُ صامتاً:
            // ‏مجلّدٌ فيه أربعَ عشرةَ نسخةً خضراء، ولا واحدةٌ فيها مفتاحُ التوقيع.
            WriteCompanion(targetPath);

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
            DiscardStaging(staging);
        }
    }

    /// <summary>
    /// ‏يكنس الملفَّ المرحليَّ — <b>وإخفاقُ الكنس لا يُبدّل حكمَ العملية</b>: النسخةُ إمّا
    /// نُقلت باسمها فتمّت، أو سقطت فسقطت، وبقيّةٌ مقفلةٌ لا تُغيّر أيَّهما.
    ///
    /// <para><b>وموضعٌ واحدٌ لا موضعان</b>: كُتب هذا الكنسُ مرّتين — في النسخة ورفيقِها —
    /// <b>فأسقطت بوّابةُ الابتلاع الصامت الملفَّ بأربعةِ مواضعَ مأذونٍ لها باثنين</b>.
    /// والإذنُ لم يُرفَع: <b>حين تُسقطك بوّابةٌ فأصلِح ما أشارت إليه لا ما يُرضيها</b>.
    /// وهي أشارت إلى تكرارٍ حقيقيّ.</para>
    ///
    /// <para><b>ويبقى الكنسُ الشبكيُّ حارساً بعده</b>: <c>PruneOldBackups</c> يمسح ما بقي
    /// من مرحليّاتٍ بعد مهلةٍ — فملفٌّ تعذّر حذفُه هنا لا يبيت ليلتين.</para>
    /// </summary>
    private static void DiscardStaging(string staging)
    {
        try { if (File.Exists(staging)) File.Delete(staging); }
        catch (IOException) { /* ‏تنظيفٌ لا يُبدّل حكمَ العملية */ }
        catch (UnauthorizedAccessException) { /* ‏كذلك */ }
    }

    /// <summary>
    /// <b>يستعيد فوق القاعدة الحالية — عمليةٌ متلِفة لا رجعةَ فيها.</b>
    ///
    /// <para>لا يُستدعى إلا بعد تأكيدٍ صريحٍ من المالك. ومسؤوليةُ التأكيد على المستدعي:
    /// هذه الطبقةُ تُنفّذ ولا تسأل.</para>
    ///
    /// <para>‏(ح-١٣٠) <b>وعلى PostgreSQL: نسخةٌ لما سيُتلَف أوّلاً</b> (<see cref="DefaultPreRestoreFolder"/>)، <b>ثمّ مخطَّطٌ فارغ،
    /// ثمّ الاستعادة</b> — فقاعدةٌ أحدثُ من النسخة لا تتركها نصفَ مستعادة.</para>
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

        // ‏(ح-٥) ‏**وبعد القاعدة: ملفّاتُها.** وترتيبُه مقصود — فالقاعدةُ هي المتلِفةُ التي
        // ‏قد تسقط، والرفيقُ نسخُ ملفّاتٍ لا يُتلف شيئاً. واستعادةٌ بلا رفيقٍ تبقى استعادةً
        // ‏صحيحةً لنسخةٍ قديمةٍ أُخذت قبل هذا التعديل — **فغيابُه ليس فشلاً**.
        RestoreCompanion(backupPath);
    }

    /// <summary>
    /// ‏يكتب رفيقَ النسخة: شجرةُ ZATCA كلُّها في أرشيفٍ واحدٍ بجوار الملفّ.
    ///
    /// <para><b>ويُكتب جانباً ثمّ يُنقل</b> كالنسخة نفسِها — فأرشيفٌ مقطوعٌ يبدو رفيقاً
    /// سليماً حتى يوم الحاجة.</para>
    ///
    /// <para><b>والقراءةُ بـ<c>FileShare.ReadWrite</c></b>: الأرشيفُ مجلّدٌ حيٌّ يكتب فيه
    /// المُولِّدُ بينما يجري النسخ، <b>وقفلٌ واحدٌ كان سيُسقط النسخةَ الليليّة كلَّها</b>.</para>
    /// </summary>
    private static void WriteCompanion(string targetPath)
    {
        var companion = targetPath + CompanionSuffix;
        var staging = companion + StagingSuffix;
        var source = CompanionSourceDirectory();

        // ‏رفيقٌ قديمٌ لاسمٍ يُعاد استعمالُه لا يُترك يدّعي محتوًى ليس فيه.
        if (File.Exists(companion)) File.Delete(companion);

        var files = Directory.Exists(source)
            ? Directory.GetFiles(source, "*", SearchOption.AllDirectories)
            : [];

        // ‏ولا مفاتيحَ بعدُ ولا أرشيف — فلا رفيقَ يُكتب، ولا يُدّعى أنّ ثمّة ما حُفظ.
        if (files.Length == 0) return;

        try
        {
            if (File.Exists(staging)) File.Delete(staging);

            using (var zip = ZipFile.Open(staging, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    var relative = Path.GetRelativePath(source, file).Replace('\\', '/');
                    var entry = zip.CreateEntry(relative, CompressionLevel.Optimal);
                    using var input = new FileStream(
                        file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var output = entry.Open();
                    input.CopyTo(output);
                }
            }

            File.Move(staging, companion, overwrite: true);
        }
        finally
        {
            DiscardStaging(staging);
        }
    }

    /// <summary>
    /// ‏يُعيد شجرةَ ZATCA من الرفيق إن وُجد.
    ///
    /// <para><b>ولا يُحذف الموجود:</b> الاستخراجُ يكتب فوق المتطابق ويترك ما ليس في الأرشيف.
    /// فمفتاحٌ أحدثُ من النسخة لا يُمحى باستعادةِ دفاترَ أقدم — <b>وضياعُ مفتاحٍ خاصٍّ
    /// لا يُعوَّض بتسجيلٍ جديدٍ لدى الهيئة وحدَه</b>.</para>
    /// </summary>
    private static void RestoreCompanion(string backupPath)
    {
        var companion = backupPath + CompanionSuffix;
        if (!File.Exists(companion)) return;

        var target = CompanionSourceDirectory();
        Directory.CreateDirectory(target);

        // ‏و`ExtractToDirectory` يرفض مدخلاً يخرج عن المجلّد الهدف — فلا يُكتب خارجَ الجذر.
        ZipFile.ExtractToDirectory(companion, target, overwriteFiles: true);
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

        // ‏(م-٥ · ن-٤٤) **والبقايا المرحليّةُ تُكنَس بالنمط لا بالاسم.**
        //
        // ‏ن-٣٦ حدّت المجلّدَ بأربعَ عشرةَ نسخةً لأنّه يشارك القرصَ مع القاعدة الحيّة.
        // ‏**وملفُّ `.partial` لا يدخل ذلك الحدّ**: لا النمطُ ذو الامتداد يراه، ولا
        // ‏`NewestBackupUtc` يعدّه — **فهو بحجم نسخةٍ كاملةٍ ولا يُحصيه شيء**. و`finally`
        // ‏في `CreateAsync` يبتلع فشلَ حذفه، ولا يُنفَّذ أصلاً إن مات المسارُ دون فكّ المكدّس.
        // ‏فيُكنَس هنا — **حيث يلتقي مُنتِجا المجلّد** — وبمهلةٍ تُجنّب ملفّاً يُكتب الآن.
        stale.AddRange(new DirectoryInfo(directory)
            .GetFiles($"brqosh_*{extension}{StagingSuffix}")
            .Where(f => f.LastWriteTimeUtc < DateTime.UtcNow - StagingOrphanGrace));

        var pruned = 0;
        var failed = 0;
        foreach (var file in stale)
        {
            try
            {
                // ‏(ح-٥) ‏**والرفيقُ يُحذف مع نسختِه** — وإلّا نما بلا حدٍّ على القرص نفسِه
                // ‏الذي حُدّت النسخُ من أجله، وبقي يدّعي نسخةً لم تعد موجودة.
                var companion = file.FullName + CompanionSuffix;
                if (File.Exists(companion)) File.Delete(companion);

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

        // ‏(م-٥ · ن-٤٤) **ملفٌّ فارغٌ ليس قاعدةً — وSQLite يقول إنّه سليم.**
        //
        // ‏مقيس: `PRAGMA integrity_check` على ملفٍّ صفريٍّ يُعيد `ok`، لأنّ SQLite يفتح
        // ‏الفارغَ قاعدةً فارغةً صحيحة. **وهذا الفحصُ هو الحارسُ الوحيدُ أمام `RestoreAsync`**،
        // ‏و`RestoreSqliteAsync` ينسخ فوق القاعدة الحيّة بـ`overwrite: true` — **فملفٌّ صفريٌّ
        // ‏كان يجتاز التحقّقَ ثمّ يمحو الدفترَ**. و`NewestBackupUtc` يُسقط الصفريَّ منذ ن-٣١،
        // ‏**والفحصُ لم يكن يُسقطه** — فاتّفقا أخيراً.
        if (new FileInfo(backupPath).Length == 0)
            throw new AccountingException(
                $"ملفُّ النسخة الاحتياطية فارغ (صفرُ بايت): {backupPath} — وليس قاعدةً تُستعاد.");

        if (db.Database.IsNpgsql())
            await VerifyPostgresArchiveAsync(backupPath, ct);
        else
            await VerifySqliteFileAsync(backupPath, ct);
    }

    /// <summary>
    /// ‏(م-٥ · ن-٤٤) <b>أحدثُ نسخةٍ <i>أُثبتت</i> — لا أحدثُ ملفٍّ يبدأ اسمُه بـ<c>brqosh_</c>.</b>
    ///
    /// <para><b>ما كان:</b> شاشةُ النسخ تختار بـ<c>GetFiles("brqosh_*")</c> — <b>بلا امتداد</b> —
    /// وتُسلّم ما تجده إلى النقل خارج الجهاز. وهو <b>الموضعُ الوحيدُ في المستودع</b> الذي
    /// يمسح مجلّدَ النسخ بنمطٍ بلا امتداد؛ وكلُّ موضعٍ آخر يستعمل <c>brqosh_*.dump</c>
    /// و<c>brqosh_*.db</c>. <b>فنقض ثابتاً أعلنه المستودعُ بنفسه</b> عند
    /// <see cref="StagingSuffix"/>: «بقايا محاولةٍ مقطوعةٍ تصير غيرَ معدودةٍ <b>بالتصميم</b>».
    /// وقِيس أنّ النمطَ العاري يلتقط خمسةَ عشرَ صنفاً لا يلتقطها الامتدادان — منها
    /// <c>.dump.partial</c> و<c>.db-wal</c> و<c>.db-journal</c> و<c>.zip</c> و<c>.tmp</c>.</para>
    ///
    /// <para><b>والاسمُ وحدَه لا يكفي</b>: ملفٌّ صحيحُ الامتداد قد يكون فاسدَ المحتوى. فيُضاف
    /// الفحصُ الذي يملكه المستودعُ أصلاً (<see cref="VerifyAsync"/>)، ويُمشى على الأحدث فالأحدث.</para>
    ///
    /// <para><b>وحدُّه مُعلَنٌ ومقيس — ولا يُقرأ أوسعَ ممّا يقيس:</b> على PostgreSQL
    /// <c>pg_restore --list</c> <b>يقبل بادئةً من الأرشيف طولُها ٠٫٢٪</b> (٣٠٨٣ بايت من
    /// ١٫٥٨ ميجابايت، رمزُ خروجٍ صفر وفهرسٌ من ٢٤ سطراً) بينما الاستعادةُ الحقيقيّةُ ترفضها
    /// بـ«could not find block ID … possibly corrupt archive». <b>فهذا يُثبت أنّ الملفَّ
    /// أرشيفٌ، ولا يُثبت أنّه كامل</b> — وإثباتُ الاكتمال يلزمه استعادةٌ فعليّة (ح-٦).</para>
    /// </summary>
    public async Task<string> NewestVerifiedAsync(string backupFolder, CancellationToken ct = default)
    {
        var extension = db.Database.IsNpgsql() ? ".dump" : ".db";

        var all = new DirectoryInfo(backupFolder)
            .GetFiles($"brqosh_*{extension}")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToList();

        if (all.Count == 0)
            throw new AccountingException("لا نسخةَ لنقلها — خُذْ نسخةً أوّلاً.");

        // ‏**والمشيُ محدود.** الفحصُ يقرأ الأرشيفَ كاملاً، فمجلّدٌ فيه أربعَ عشرةَ نسخةً
        // ‏فاسدةً يُنفق دقائقَ على قرصٍ قبل أن يقول شيئاً. وثلاثةٌ تُغطّي «نسخةُ اليوم
        // ‏فاسدة» وما قبلها، **وما بعد ذلك عطلٌ يُبلَّغ لا يُلتَفّ عليه**.
        var examined = all.Take(NewestCandidatesExamined).ToList();
        var rejected = new List<string>();

        foreach (var file in examined)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await VerifyAsync(file.FullName, ct);
                return file.FullName;
            }
            catch (BackupVerifierUnavailableException)
            {
                // ‏**بيئةٌ لا تُسأل ليست حكماً على البيانات.** ولو ابتُلعت هنا لقيل للمالك
                // ‏«فُحصت نسخُك وسقطت كلُّها» وسببُها أنّ `pg_restore` غيرُ مُنصَّب.
                throw;
            }
            catch (AccountingException ex)
            {
                rejected.Add($"{file.Name} — {ex.Message}");
            }
        }

        throw new AccountingException(
            $"لا نسخةَ صالحةً للنقل — فُحصت أحدثُ {rejected.Count} من {all.Count} وسقطت كلُّها:\n  "
            + string.Join("\n  ", rejected));
    }

    /// <summary>‏كم نسخةً تُفحَص قبل الإعلان — انظر <see cref="NewestVerifiedAsync"/>.</summary>
    internal const int NewestCandidatesExamined = 3;

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

    /// <summary>‏(ح-١٣٠) نسخُ ما قبل الاستعادة — بجوار النسخ لا فيها، فلا تدخل حدَّها ولا تُطفئ إنذارَ التأخّر.</summary>
    public static string DefaultPreRestoreFolder() =>
        Path.Combine(BrqoshDataRoot.Path(), "backups", "pre-restore");

    /// <summary>
    /// ‏(ح-١٣٠) يُفرغ <c>public</c> ممّا يملكه دورُ التطبيق — الجداولُ والعروضُ والتسلسلاتُ <b>بـ<c>CASCADE</c></b> — ولا يحذف
    /// المخطَّطَ نفسَه: ملكيّتُه لمالك القاعدة في PostgreSQL 15+ وحدَه، وقبله لـ<c>postgres</c>.
    /// </summary>
    internal const string ClearPublicSchemaSql = """
        DO $$
        DECLARE r record;
        BEGIN
            FOR r IN SELECT c.relname, c.relkind FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                     WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p', 'v', 'm', 'S', 'f')
                     -- ‏تسلسلُ عمود الهويّة لا يُحذف قبل جدوله: العروضُ ثمّ الجداولُ (تأخذ تسلسلاتِها) ثمّ ما بقي.
                     ORDER BY CASE c.relkind WHEN 'v' THEN 0 WHEN 'm' THEN 1 WHEN 'S' THEN 3 ELSE 2 END
            LOOP
                EXECUTE format('DROP %s IF EXISTS public.%I CASCADE',
                    CASE r.relkind WHEN 'v' THEN 'VIEW' WHEN 'm' THEN 'MATERIALIZED VIEW'
                                   WHEN 'S' THEN 'SEQUENCE' WHEN 'f' THEN 'FOREIGN TABLE' ELSE 'TABLE' END,
                    r.relname);
            END LOOP;
        END $$;
        """;

    private async Task RestorePostgresAsync(string backupPath, CancellationToken ct)
    {
        var tool = PgToolLocator.Find("pg_restore", out var searched)
            ?? throw new AccountingException(BuildToolMissingMessage("pg_restore", searched));

        var builder = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());

        // ‏(ح-١٣٠ · data-upgrade-performance#1) **أوّلاً: نسخةٌ مُتحقَّقٌ منها لما سيُتلَف.** فاستعادةُ الملفّ الخطأ، أو سقوطُها
        // ‏في منتصفها، لا تُضيّع دفاترَ اليوم. **وفشلُها يُسقط الاستعادةَ قبل أن يُمسّ شيء.**
        await CreateAsync(Path.Combine(preRestoreFolder ?? DefaultPreRestoreFolder(),
            $"brqosh_{DateTime.UtcNow:yyyyMMdd_HHmmss}_before_restore.dump"), ct);

        // ‏**ثمّ مخطَّطٌ فارغ — فالاستعادةُ هي المُثبَتة.** `--clean` يحذف ما في الأرشيف وحدَه وبلا `CASCADE`: فجدولٌ أحدثُ
        // ‏من النسخة (رُقّيت القاعدةُ بعدها) يعتمد على جدولٍ فيها **يمنع حذفَه، فتخرج الاستعادةُ بخطأٍ والقاعدةُ نصفُ مستعادة.**
        await db.Database.ExecuteSqlRawAsync(ClearPublicSchemaSql, ct);

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
        // ‏(ن-٤٤) **«تعذّر أن أسأل» ليس «سألتُ فكان فاسداً».** أداةٌ غائبةٌ عطلُ بيئة،
        // ‏ولو رُميت كـ`AccountingException` لجمعها `NewestVerifiedAsync` حكماً على ملفّ،
        // ‏فقيل للمالك إنّ نسخَه كلَّها فاسدة — أربعَ عشرةَ مرّةً — والسببُ أنّ الأداةَ غيرُ مُنصَّبة.
        var tool = PgToolLocator.Find("pg_restore", out var searched)
            ?? throw new BackupVerifierUnavailableException(BuildToolMissingMessage("pg_restore", searched));

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
