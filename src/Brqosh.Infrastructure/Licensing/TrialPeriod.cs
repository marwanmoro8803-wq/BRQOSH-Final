using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Brqosh.Infrastructure.Persistence;
using Microsoft.Win32;

namespace Brqosh.Infrastructure.Licensing;

/// <summary>حالةُ الفترة التجريبية.</summary>
public sealed record TrialState(DateTime StartedUtc, DateTime ExpiresUtc, int DaysLeft, bool Expired);

/// <summary>
/// <b>الفترةُ التجريبية — ٧ أيامٍ من أوّل تشغيل، ولا تعود بحذف البرنامج وإعادة تنصيبه.</b>
///
/// <para>يُسجَّل بدءُ التجربة في <b>أربعة مواضع</b>: جذرُ بيانات التطبيق، و
/// <c>%ProgramData%\BRQOSH</c>، و<c>%LOCALAPPDATA%\BRQOSH</c>، وسجلُّ المستخدم
/// (<c>HKCU\Software\BRQOSH</c>). كلُّ سجلٍّ ختمُه مشتقٌّ من معرّف الجهاز، فلا يُنسَخ
/// من جهازٍ إلى جهاز، ولا يُحرَّر بمحرّر نصوصٍ بلا أن يُكشَف. وعند القراءة يُؤخذ
/// <b>أبكرُ</b> بدءٍ سليمٍ — فمن محا بعضَ المواضع لا يمدّد تجربتَه، ومن أعاد
/// التنصيبَ يجد التجربةَ حيث انتهت.</para>
///
/// <para><b>حدٌّ يُقال صراحةً:</b> من يملك صلاحيةَ مسؤولٍ ويحذف المواضعَ الأربعةَ
/// ويحوّر الساعةَ يعيد التجربة. لا وقايةَ كاملةً بلا خدمةٍ متصلة؛ وهذه الطبقةُ
/// تمنع المحاولاتِ العاديّةَ كلَّها.</para>
/// </summary>
public static class TrialPeriod
{
    /// <summary>مدةُ التجربة — سبعةُ أيام.</summary>
    public const int Days = LicensePlanInfo.TrialDays;

    /// <summary>سجلٌّ واحد: بدءُ التجربة وختمُه وربطُه بالجهاز.</summary>
    public sealed record TrialStampRecord(DateTime StartedUtc, string MachineBind, string Mac);

    /// <summary>للاختبارات وحدَها — يستبدل المواضعَ الحقيقيةَ فلا تُمسّ سجلُّ ولا ملفاتُ الجهاز.</summary>
    internal static ITrialStampStore? StoreOverride { get; set; }

    /// <summary>عددُ مواضعِ الكتابة التي تعذّرت — للتشخيص لا للإنذار.</summary>
    internal static int FailedWrites;

    /// <summary>عددُ المواضع التي تعذّرت قراءتُها — للتشخيص لا للإنذار.</summary>
    internal static int FailedReads;

    /// <summary>واجهةُ موضعِ سجلٍّ واحد — حقيقيةٌ في التشغيل وبديلةٌ في الاختبار.</summary>
    internal interface ITrialStampStore
    {
        TrialStampRecord? Read();
        void Write(TrialStampRecord record);
    }

    /// <summary>حالةُ التجربة على وقتٍ فعّال — و<c>null</c> إن لم يبدأ أيُّ سجلٍّ سليم.</summary>
    public static TrialState? Status(DateTime effectiveNowUtc)
    {
        var started = EarliestStarted(effectiveNowUtc);
        return started is DateTime start ? StateFor(start, effectiveNowUtc) : null;
    }

    /// <summary>
    /// يضمن بدءَ التجربة: إن لم يوجد سجلٌّ سليم كُتب الآن في كلّ المواضع الممكنة،
    /// وإلا أُبقي الأبكرُ. يعيد حالةَ التجربة الناتجة.
    /// </summary>
    public static TrialState EnsureStarted(DateTime nowUtc)
    {
        var started = EarliestStarted(nowUtc);
        if (started is null)
        {
            started = nowUtc;
            var record = Stamp(started.Value);
            foreach (var store in Stores())
            {
                try
                {
                    store.Write(record);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                              or System.Security.SecurityException or ArgumentException)
                {
                    // ‏موضعٌ واحدٌ يتعذّر لا يُسقط التجربة — تُكتب المواضعُ الباقية، والعددُ يُقرأ للتشخيص.
                    FailedWrites++;
                }
            }
        }

        return StateFor(started.Value, nowUtc);
    }

    /// <summary>
    /// قرارُ الأبكر — دالةٌ نقيّةٌ تُقاس في الاختبارات: تُغربَل السجلاتُ التالفة
    /// أو سجلاتُ أجهزةٍ أخرى، ويُؤخذ أبكرُ بدءٍ. ويعيد <c>null</c> إن لم يبق سليمٌ.
    /// </summary>
    internal static DateTime? Resolve(IReadOnlyList<TrialStampRecord> records)
    {
        DateTime? earliest = null;
        foreach (var record in records)
        {
            if (record.MachineBind != MachineBind())
                continue;
            if (!VerifyMac(record))
                continue;
            if (earliest is null || record.StartedUtc < earliest)
                earliest = record.StartedUtc;
        }

        return earliest;
    }

    /// <summary>يقرأ المواضعَ كلَّها ويأخذ أبكرَ بدءٍ سليم.</summary>
    private static DateTime? EarliestStarted(DateTime nowUtc)
    {
        var records = new List<TrialStampRecord>();
        foreach (var store in Stores())
        {
            try
            {
                var record = store.Read();
                if (record is not null)
                    records.Add(record);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                          or JsonException or System.Security.SecurityException)
            {
                // ‏موضعٌ لا يُقرأ يُتجاوز — والباقي يفصل، والعددُ يُقرأ للتشخيص.
                FailedReads++;
            }
        }

        return Resolve(records);
    }

    private static TrialState StateFor(DateTime started, DateTime nowUtc)
    {
        // ‏ساعةٌ راجعةٌ قبل بدء التجربة لا تمدّدها — يُقاس ما بعد البدء وحدَه.
        var measured = nowUtc < started ? started : nowUtc;
        var expires = started.AddDays(Days);
        var remaining = expires - measured;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;
        var daysLeft = (int)Math.Ceiling(remaining.TotalDays);
        return new TrialState(started, expires, daysLeft, measured >= expires);
    }

    private static IReadOnlyList<ITrialStampStore> Stores()
    {
        if (StoreOverride is { } single)
            return [single];

        return
        [
            new FileStore(Path.Combine(BrqoshDataRoot.Path(), "trial.json")),
            new FileStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "BRQOSH", "trial.json")),
            new FileStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BRQOSH", "trial.json")),
            new RegistryStore(),
        ];
    }

    /// <summary>يبني سجلّاً سليماً لبدءٍ معلوم — وللاختبارات أن تصنع سجلاتِ الماضي به.</summary>
    internal static TrialStampRecord Stamp(DateTime startedUtc)
    {
        var bind = MachineBind();
        return new TrialStampRecord(startedUtc, bind, MacFor(startedUtc, bind));
    }

    private static bool VerifyMac(TrialStampRecord record) =>
        CryptographicOperations.FixedTimeEquals(
            HexBytes(MacFor(record.StartedUtc, record.MachineBind)), HexBytes(record.Mac));

    private static string MacFor(DateTime startedUtc, string machineBind)
    {
        var key = Encoding.UTF8.GetBytes(LicenseService.MachineId());
        var message = Encoding.UTF8.GetBytes($"v1|{startedUtc.Ticks}|{machineBind}");
        var hash = new HMACSHA256(key).ComputeHash(message);
        return Convert.ToHexString(hash)[..24];
    }

    private static string MachineBind() =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(LicenseService.MachineId())))[..12];

    private static byte[] HexBytes(string hex) => Convert.FromHexString(hex);

    /// <summary>موضعُ ملفٍّ JSON واحد.</summary>
    internal sealed class FileStore(string path) : ITrialStampStore
    {
        public TrialStampRecord? Read()
        {
            if (!File.Exists(path))
                return null;
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("StartedUtc", out var startedEl)
                || !root.TryGetProperty("MachineBind", out var bindEl)
                || !root.TryGetProperty("Mac", out var macEl))
                return null;
            if (!DateTime.TryParse(startedEl.GetString(), null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var started))
                return null;
            var bind = bindEl.GetString();
            var mac = macEl.GetString();
            return bind is null || mac is null
                ? null
                : new TrialStampRecord(started.ToUniversalTime(), bind, mac);
        }

        public void Write(TrialStampRecord record)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                StartedUtc = record.StartedUtc.ToString("O"),
                record.MachineBind,
                record.Mac,
            }));
        }
    }

    /// <summary>موضعُ قيمةِ سجلّ ويندوز.</summary>
    internal sealed class RegistryStore : ITrialStampStore
    {
        private const string KeyPath = @"Software\BRQOSH";

        public TrialStampRecord? Read()
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (key?.GetValue("TrialStartedUtc") is not string startedText)
                return null;
            var bind = key.GetValue("TrialMachineBind") as string;
            var mac = key.GetValue("TrialMac") as string;
            if (bind is null || mac is null
                || !DateTime.TryParse(startedText, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var started))
                return null;
            return new TrialStampRecord(started.ToUniversalTime(), bind, mac);
        }

        public void Write(TrialStampRecord record)
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            key.SetValue("TrialStartedUtc", record.StartedUtc.ToString("O"), RegistryValueKind.String);
            key.SetValue("TrialMachineBind", record.MachineBind, RegistryValueKind.String);
            key.SetValue("TrialMac", record.Mac, RegistryValueKind.String);
        }
    }
}
