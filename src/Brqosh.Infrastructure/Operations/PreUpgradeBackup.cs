using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// ‏ <b>الترقيةُ تنسخ الدفاترَ قبل أن تُهاجرها.</b>
///
/// <para>‏<b>ما كان:</b> إصدارٌ جديدٌ يُقلع على قاعدةٍ فيها هجراتٌ معلَّقة فيُطبّقها <c>MigrateAsync</c> فوراً —
/// <b>بلا نسخةٍ قبلها</b>. والنسخةُ المجدولةُ قد تكون بنت يومٍ أو أسبوع، أو لم تُؤخذ أصلاً (أدواتُ PostgreSQL غائبة،
/// <c>data-upgrade-performance#6</c>). <b>فهجرةٌ تُفسد بياناتٍ لا رجوعَ منها إلّا إلى ما قبل عملِ ذلك اليوم.</b></para>
///
/// <para>‏<b>والحكم:</b> لا هجرةَ على دفاترَ قائمةٍ حتى تُكتب نسخةٌ <b>مُتحقَّقٌ منها</b> (<see cref="BackupService.CreateAsync"/>:
/// تُكتب جانباً وتُقرأ بالمحرّك ثمّ تُنقل باسمها). <b>وفي الإنتاج يمنع فشلُها الترقيةَ</b> برسالةٍ تقول ما يُفعل —
/// فالبديلُ ترقيةٌ بلا نقطةِ رجوع. وخارجَ الإنتاج يُسجَّل ويمضي، كي لا يُوقف خادمُ اختبارٍ بلا أدوات.</para>
///
/// <para>‏<b>ومجلّدٌ مستقلّ</b> (<c>backups/pre-upgrade</c>): النسخةُ لا تدخل حدَّ الأربعَ عشرةَ اليوميّة فتُكنَس
/// قبل أن يُحتاج إليها، ولا تُطفئ إنذارَ «تأخّر النسخ» وهي ليست من الجدول.</para>
/// </summary>
public sealed class PreUpgradeBackup(
    BrqoshDbContext db,
    string? folder = null,
    Func<string, CancellationToken, Task>? write = null)
{
    /// <summary>
    /// ‏<b>المخرجُ الصريحُ الوحيد</b> — لمن يأخذ نسخةً بنفسه على مستوى الخادم (لقطةُ قرصٍ أو خدمةٌ مُدارة).
    /// يُضبط لمرّةٍ ويُسجَّل تجاوزُه في سجلّ الأعطال، فلا يمضي صامتاً.
    /// </summary>
    public const string OptOutVariable = "BRQOSH_UPGRADE_WITHOUT_BACKUP";

    public static string DefaultFolder() =>
        Path.Combine(BrqoshDataRoot.Path(), "backups", "pre-upgrade");

    /// <summary>
    /// ‏يكتب النسخةَ إن كان ثمّة ما يُهاجَر، ويُعيد مسارَها — أو <c>null</c> إن لم يكن (أو تُجووز بالمخرج الصريح).
    /// </summary>
    public async Task<string?> TakeAsync(
        IReadOnlyList<string> pendingMigrations, bool isProduction, CancellationToken ct = default)
    {
        if (pendingMigrations.Count == 0)
            return null;

        if (Environment.GetEnvironmentVariable(OptOutVariable) == "1")
        {
            CrashLog.Write(new InvalidOperationException(
                $"‏تُرقّى القاعدةُ ({pendingMigrations.Count} هجرة) بلا نسخةٍ قبلها — {OptOutVariable}=1."),
                "pre-upgrade-backup-skipped");
            return null;
        }

        var target = Path.Combine(folder ?? DefaultFolder(),
            FileName(pendingMigrations[0], DateTime.UtcNow, db.Database.IsNpgsql()));
        try
        {
            if (write is not null)
                await write(target, ct);
            else
                await new BackupService(db).CreateAsync(target, ct);
            return target;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!isProduction)
            {
                CrashLog.Write(ex, "pre-upgrade-backup");
                return null;
            }

            throw new InvalidOperationException(RefusalMessage(pendingMigrations.Count, ex.Message), ex);
        }
    }

    /// <summary>
    /// ‏اسمُ النسخة يقول <b>قبل أيّ هجرةٍ</b> أُخذت — فاستعادتُها تُعيد القاعدةَ إلى ما قبلها بالضبط.
    /// </summary>
    internal static string FileName(string firstPending, DateTime utc, bool postgres) =>
        $"brqosh_{utc:yyyyMMdd_HHmmss}_before_{firstPending}.{(postgres ? "dump" : "db")}";

    internal static string RefusalMessage(int pending, string reason) =>
        $"لم تُرقَّ قاعدةُ البيانات: فيها {pending} هجرةً معلَّقة، وتعذّرت النسخةُ الاحتياطيّةُ التي تُؤخذ قبل الترقية — "
        + $"{reason} لم يتغيّر شيءٌ في الدفاتر. ثبّت أدواتِ PostgreSQL للعميل (pg_dump) بإصدار الخادم أو أحدث ثمّ أعد التشغيل؛ "
        + $"أو — إن أخذتَ نسخةً بنفسك — اضبط {OptOutVariable}=1 لهذا الإقلاع وحدَه.";
}
