using System.Security.Cryptography;
using System.Text.Json;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// (جاهزيّةُ الإنتاج · H) <b>مفتاحُ النسخ الرئيسيّ لهذه المنشأة — ويُولد ويُدوَّر مع مفتاح استعادة المدير.</b>
///
/// <para><b>ما يُحفظ</b> في <c>security/backup-keyring.json</c> تحت جذر البيانات:</para>
/// <list type="bullet">
/// <item>المفتاحُ العامّ الرئيسيّ — يكفي وحدَه لتشفير كلّ نسخةٍ مجدولة، بلا سرّ.</item>
/// <item>مقطعُ الاستعادة (<see cref="RecoveryWrap"/>) — المفتاحُ الخاصّ مغلَّفاً بمفتاح الاستعادة، ويُنسخ في رأس كلّ نسخة.</item>
/// <item>المفتاحُ الخاصّ محميّاً بـDPAPI لمستخدم ويندوز — <b>لراحة الجهاز نفسِه فقط</b>: فحصُ النسخة بعد أخذها، والاستعادةُ على
/// الجهاز نفسِه بلا سؤال، وإعادةُ التغليف عند تدوير المفتاح. <b>والنسخةُ لا تعتمد عليه</b>: على جهازٍ جديدٍ تُفتح بمفتاح الاستعادة
/// وحدَه من رأسها. والقاعدةُ الحيّةُ على الجهاز نفسِه أصلاً، فلا يكشف هذا المقطعُ شيئاً لا يكشفه الجهاز.</item>
/// </list>
///
/// <para><b>متى يُنشأ:</b> عند أوّل إصدارٍ لمفتاح الاستعادة (الإعداد، أو «إعادة إصدار المفتاح»، أو الاستعادة به). تنصيبٌ قديمٌ أُصدر
/// مفتاحُه قبل هذا الإصدار تبقى نسخُه <b>غيرَ مشفّرة</b> حتى يُعاد إصدارُ المفتاح — وشاشةُ النسخ تقول ذلك (<see cref="Exists"/>).</para>
/// </summary>
public sealed class BackupKeyring
{
    private const int FormatVersion = 1;

    public byte[] MasterPoint { get; }
    public RecoveryWrap Recovery { get; }
    private readonly string? _protectedPrivate;

    private BackupKeyring(byte[] masterPoint, RecoveryWrap recovery, string? protectedPrivate)
    {
        MasterPoint = masterPoint;
        Recovery = recovery;
        _protectedPrivate = protectedPrivate;
    }

    /// <summary>
    /// منفذُ عزلٍ للاختبار — <c>AsyncLocal</c> لا متغيّرُ بيئة، فلا يتسرّب جذرُ اختبارٍ إلى اختبارٍ يجري معه (نمطُ <c>CrashLog</c>).
    /// </summary>
    internal static readonly AsyncLocal<string?> RootOverrideSlot = new();

    private static string DefaultRoot =>
        RootOverrideSlot.Value is { Length: > 0 } isolated ? isolated : BrqoshDataRoot.Path();

    /// <summary>مسارُ الملفّ تحت جذر البيانات.</summary>
    public static string PathFor(string? dataRoot = null) =>
        System.IO.Path.Combine(dataRoot ?? DefaultRoot, "security", "backup-keyring.json");

    /// <summary>هل تُشفَّر النسخُ على هذا التنصيب؟</summary>
    public static bool Exists(string? dataRoot = null) => File.Exists(PathFor(dataRoot));

    /// <summary>يقرأ الحلقة، و<c>null</c> إن لم تُنشأ بعد.</summary>
    public static BackupKeyring? Load(string? dataRoot = null)
    {
        var path = PathFor(dataRoot);
        if (!File.Exists(path)) return null;

        var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(path))
                     ?? throw new InvalidDataException($"ملفُّ مفتاح النسخ تالف: {path}");
        if (stored.Version != FormatVersion)
            throw new InvalidDataException($"إصدارُ ملفّ مفتاح النسخ غيرُ معروف ({stored.Version}): {path}");

        return new BackupKeyring(
            Convert.FromBase64String(stored.MasterPoint),
            RecoveryWrap.ReadFrom(Convert.FromBase64String(stored.Recovery)),
            stored.ProtectedPrivate);
    }

    /// <summary>
    /// المفتاحُ الخاصّ على هذا الجهاز (DPAPI) — <c>null</c> إن لم يُحفظ أو لم يُفكّ (مستخدمُ ويندوز آخر، أو نظامٌ غيرُ ويندوز).
    /// </summary>
    public ECDiffieHellman? OpenLocal()
    {
        if (string.IsNullOrEmpty(_protectedPrivate) || !LocalSecretProtector.IsProtected(_protectedPrivate))
            return null;

        string plain;
        try
        {
            plain = LocalSecretProtector.Unprotect(_protectedPrivate);
        }
        catch (CryptographicException ex)
        {
            CrashLog.Write(ex, "backup-keyring-local-open");
            return null;
        }

        if (string.IsNullOrEmpty(plain)) return null;
        var d = Convert.FromBase64String(plain);
        try { return BackupCipher.ImportPrivate(d, MasterPoint); }
        finally { CryptographicOperations.ZeroMemory(d); }
    }

    /// <summary>
    /// يُستدعى كلّما صدر مفتاحُ استعادةٍ جديد: يُنشئ الحلقةَ أوّلَ مرّة، أو يُعيد تغليفَ المفتاح الخاصّ بالمفتاح الجديد
    /// <b>ويُعيد تغليفَ مقطع الاستعادة في كلّ نسخةٍ مشفّرةٍ قائمة</b> بلا إعادة تشفير المحتوى.
    /// </summary>
    /// <returns>عددُ النسخ التي أُعيد تغليفُها.</returns>
    public static int OnRecoveryKeyIssued(string recoveryKey, string? dataRoot = null)
    {
        var root = dataRoot ?? DefaultRoot;
        var existing = Load(root);
        using var master = existing?.OpenLocal() ?? ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        // ‏حلقةٌ قائمةٌ لا يُفكّ مفتاحُها الخاصّ هنا (مستخدمُ ويندوز آخر): يُولد مفتاحٌ رئيسيٌّ جديدٌ للنسخ القادمة،
        // ‏والنسخُ السابقة تبقى تُفتح بمفتاح الاستعادة الذي كان قائماً عند أخذها — ولا يُدّعى أنّها أُعيد تغليفُها.
        var point = BackupCipher.PointOf(master);
        var d = master.ExportParameters(true).D!;
        int rewrapped;
        try
        {
            var recovery = RecoveryWrap.Create(d, recoveryKey, point, UserAuthService.Pbkdf2Iterations);
            Save(root, point, recovery, LocalSecretProtector.ProtectionIsAvailable
                ? LocalSecretProtector.Protect(Convert.ToBase64String(d))
                : null);
            rewrapped = RewrapBackups(root, point, recovery);
        }
        finally { CryptographicOperations.ZeroMemory(d); }

        return rewrapped;
    }

    /// <summary>مجلّداتُ النسخ التي يملكها البرنامج: النسخُ اليدويّة والمجدولة، ونسخُ ما قبل الاستعادة.</summary>
    public static IEnumerable<string> BackupFolders(string root)
    {
        yield return System.IO.Path.Combine(root, "backups");
        yield return System.IO.Path.Combine(root, "backups", "pre-restore");
    }

    // ‏النسختان (PostgreSQL وSQLite) ورفيقُهما من مستندات الهيئة.
    private static readonly string[] RewrapPatterns = ["brqosh_*.dump", "brqosh_*.db", "brqosh_*.zatca.zip"];

    private static int RewrapBackups(string root, byte[] point, RecoveryWrap recovery)
    {
        var count = 0;
        foreach (var folder in BackupFolders(root).Where(Directory.Exists))
        {
            // ‏بامتداداتٍ صريحة: نمطٌ بلا امتداد يلتقط `.partial` و`-wal` (حارسُ `OffMachineCopyCarriesAProvenBackupTests`).
            foreach (var file in RewrapPatterns.SelectMany(pattern => Directory.EnumerateFiles(folder, pattern)))
            {
                try
                {
                    if (BackupCipher.IsEncrypted(file) && BackupCipher.RewrapRecovery(file, point, recovery))
                        count++;
                }
                catch (IOException ex)
                {
                    // ‏ملفٌّ محجوزٌ يبقى بمقطعه القديم — يُفتح بالمفتاح السابق — ويُسجَّل فلا يُقرأ الصمتُ نجاحاً.
                    CrashLog.Write(ex, "backup-keyring-rewrap");
                }
            }
        }
        return count;
    }

    private static void Save(string root, byte[] point, RecoveryWrap recovery, string? protectedPrivate)
    {
        var path = PathFor(root);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(new Stored(
            FormatVersion, Convert.ToBase64String(point), Convert.ToBase64String(recovery.ToBytes()), protectedPrivate));
        var staging = path + ".partial";
        File.WriteAllText(staging, json);
        File.Move(staging, path, overwrite: true);
    }

    private sealed record Stored(int Version, string MasterPoint, string Recovery, string? ProtectedPrivate);
}
