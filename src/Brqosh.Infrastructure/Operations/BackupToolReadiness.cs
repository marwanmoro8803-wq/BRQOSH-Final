using System.Globalization;

namespace Brqosh.Infrastructure.Operations;

/// <summary>حكمُ جاهزيّة أدوات النسخ الاحتياطيّ — ورسالةٌ تقول ما يُفعل.</summary>
public sealed record BackupToolVerdict(bool Ready, string Message);

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٥ · ‏`installer-deployment-ops#10` · ‏`data-upgrade-performance#6`) <b>أدواتُ النسخ تُفحص قبل أوّل نسخة.</b>
///
/// <para>‏<see cref="BackupService"/> لا يسأل عن <c>pg_dump</c>/<c>pg_restore</c> إلّا لحظةَ النسخ (<see cref="PgToolLocator"/>)،
/// ‏والمُنصِّبُ لا يشحنهما ولا يفحصهما — <b>فأوّلُ ما يُعرف غيابُهما أوّلُ ليلةٍ لا تُنسخ فيها الدفاتر</b>. والحكمُ هنا يُعرض في
/// ‏«حالة النظام» من أوّل يوم. <b>والأقدمُ من الخادم كالغائب</b>: PostgreSQL يرفض <c>pg_dump</c> أقدمَ من خادمه صراحةً، ويقبل الأحدث.</para>
/// </summary>
public static class BackupToolReadiness
{
    /// <summary>الحكمُ من الأداتين الموجودتين (أو الغائبتين) وإصدار الخادم الرئيسيّ إن عُرف — دالّةٌ صافيةٌ تُقاس.</summary>
    public static BackupToolVerdict Assess(PgToolLocator.Tool? dump, PgToolLocator.Tool? restore, int? serverMajor)
    {
        var wanted = serverMajor is int m ? $"{m} أو أحدث" : "بإصدار الخادم أو أحدث";

        if (dump is null || restore is null)
        {
            var missing = string.Join(" و", new[] { dump is null ? "pg_dump" : null, restore is null ? "pg_restore" : null }
                .Where(n => n is not null));
            return new BackupToolVerdict(false,
                $"{missing} غير موجودة على هذه المحطّة — النسخُ الاحتياطيُّ لن يعمل. ثبّت أدوات عميل PostgreSQL {wanted} "
                + "(مثبِّتُ PostgreSQL الرسميّ بخيار Command Line Tools).");
        }

        if (serverMajor is int server && (dump.Version.Major < server || restore.Version.Major < server))
            return new BackupToolVerdict(false,
                $"pg_dump {dump.Version} وpg_restore {restore.Version} أقدمُ من الخادم {server} — يرفضهما PostgreSQL. "
                + $"ثبّت أدوات عميل PostgreSQL {wanted}.");

        return new BackupToolVerdict(true,
            $"pg_dump {dump.Version} · pg_restore {restore.Version}"
            + (serverMajor is int s ? $" — صالحتان لخادم {s}" : ""));
    }

    /// <summary>الحكمُ على هذه المحطّة — يبحث بـ<see cref="PgToolLocator"/> كما يبحث النسخُ نفسُه.</summary>
    public static BackupToolVerdict Check(int? serverMajor) =>
        Assess(PgToolLocator.Find("pg_dump", out _), PgToolLocator.Find("pg_restore", out _), serverMajor);

    /// <summary>الإصدارُ الرئيسيُّ من <c>server_version_num</c> (مثل <c>170002</c> ⟵ ١٧) — و<c>null</c> لما لا يُقرأ.</summary>
    public static int? ServerMajor(string? serverVersionNum) =>
        int.TryParse(serverVersionNum, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 10000
            ? n / 10000
            : null;
}
