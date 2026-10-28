using System.Diagnostics;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// <b>العثورُ على <c>pg_dump</c>/<c>pg_restore</c> — ومطابقةُ إصداريهما بالخادم.</b>
///
/// <para><b>ولماذا ملفٌّ كامل لهذا:</b> عطلان حقيقيّان يقتلان النسخَ الاحتياطيّ في الميدان
/// ولا يظهران في التطوير:</para>
/// <list type="number">
///   <item><b>الأداةُ ليست على المسار.</b> مثبِّتُ PostgreSQL على ويندوز لا يُضيف
///     <c>bin</c> إلى <c>PATH</c> افتراضياً. فالبحثُ يشمل مجلّدات التثبيت المعروفة.</item>
///   <item><b>الإصدارُ أقدم من الخادم.</b> <c>pg_dump 16</c> يرفض خادمَ 17 صراحةً
///     («server version mismatch»). وجهازٌ عليه إصداران — وهو الشائع بعد الترقية — يلتقط
///     الأقدمَ من المسار فيفشل النسخُ كلَّ ليلة. فيُختار <b>الأحدث</b> بين ما وُجد.</item>
/// </list>
///
/// <para>ولا يُبتلع الفشل: حين لا تُوجد أداةٌ صالحة تُقال الأسبابُ بأسمائها — المسارات
/// التي فُتّشت والإصدارات التي وُجدت — لا «تعذّر النسخ».</para>
/// </summary>
public static class PgToolLocator
{
    private static readonly string[] SearchRoots =
    [
        @"C:\Program Files\PostgreSQL",
        @"C:\Program Files (x86)\PostgreSQL",
    ];

    public sealed record Tool(string Path, Version Version);

    /// <summary>
    /// (<c>data-upgrade-performance#6</c> · قرارُ المالك 2026-10-18) <b>الأدواتُ المشحونةُ مع البرنامج</b> — المُنصِّبُ يضعها في
    /// <c>pgtools</c> بجوار التنفيذيّ، فيعمل النسخُ المجدولُ على كلّ جهازٍ من أوّل يوم بلا تثبيتٍ يدويّ.
    /// </summary>
    public static string BundledDirectory => Path.Combine(AppContext.BaseDirectory, "pgtools");

    /// <summary>أحدثُ نسخةٍ موجودة من الأداة — أو <c>null</c> مع بيانِ ما فُتّش.</summary>
    public static Tool? Find(string toolName, out IReadOnlyList<string> searched) =>
        Find(toolName, BundledDirectory, ReadVersion, out searched);

    /// <summary>درزُ الاختبار: مجلّدُ المشحون وقارئُ الإصدار يُمرَّران — والسلوكُ نفسه.</summary>
    public static Tool? Find(string toolName, string bundledDirectory, Func<string, Version?> versionProbe,
        out IReadOnlyList<string> searched)
    {
        var looked = new List<string>();
        var found = new List<Tool>();

        // ‏(data-upgrade-performance#6) ‏المشحونةُ أوّلاً في التفتيش — ويبقى الاختيارُ للأحدث: تثبيتٌ محلّيٌّ أحدثُ (خادمٌ رُقّي) يُقدَّم عليها.
        var bundled = Path.Combine(bundledDirectory, $"{toolName}.exe");
        looked.Add(bundledDirectory);
        if (File.Exists(bundled) && versionProbe(bundled) is Version bundledVersion)
            found.Add(new Tool(bundled, bundledVersion));

        foreach (var root in SearchRoots)
        {
            looked.Add(root);
            if (!Directory.Exists(root))
                continue;

            foreach (var versionDir in Directory.EnumerateDirectories(root))
            {
                var candidate = Path.Combine(versionDir, "bin", $"{toolName}.exe");
                if (File.Exists(candidate) && versionProbe(candidate) is Version v)
                    found.Add(new Tool(candidate, v));
            }
        }

        // ‏وأخيراً المسار — قد يكون التثبيت في مكانٍ غير معتاد.
        var onPath = FromPath(toolName, versionProbe);
        looked.Add("PATH");
        if (onPath is not null)
            found.Add(onPath);

        searched = looked;
        return found.OrderByDescending(t => t.Version).FirstOrDefault();
    }

    private static Tool? FromPath(string toolName, Func<string, Version?> versionProbe)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in paths)
        {
            string candidate;
            try { candidate = Path.Combine(dir, $"{toolName}.exe"); }
            catch { continue; }

            if (File.Exists(candidate) && versionProbe(candidate) is Version v)
                return new Tool(candidate, v);
        }

        return null;
    }

    private static Version? ReadVersion(string toolPath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(toolPath, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10_000);

            // ‏مثال: "pg_dump (PostgreSQL) 16.14"
            var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)\.(\d+)");
            return match.Success
                ? new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value))
                : null;
        }
        catch
        {
            return null;
        }
    }
}
