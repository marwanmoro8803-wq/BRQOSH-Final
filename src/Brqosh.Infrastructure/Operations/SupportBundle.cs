using System.IO.Compression;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// (م-٥ · ن-٤٢) <b>ملفٌّ واحدٌ يُرسَل إلى الدعم — وكان المُشغِّلُ يُترَك أمام مجلّد.</b>
///
/// <para><b>ما كان قبلها:</b> شاشةُ التشخيص ثلاثةُ أزرار. «نسخ التقرير» ينسخ صفوفَ الجدول
/// نصّاً إلى الحافظة، و«فتح مجلد السجلات» كلُّ ما يفعله
/// <c>Process.Start(CrashLog.Directory)</c> — <b>يفتح المستكشفَ ويترك المستخدم</b>.</para>
///
/// <para><b>والسيناريو الذي يعضّ:</b> عطلٌ متقطّعٌ عند منشأةٍ في مدينةٍ أخرى والدعمُ بلا
/// وصولٍ عن بُعد. يُطلب «نسخ التقرير» فيصل ثلاثةَ عشرَ سطراً فيها «سجلات أعطال محفوظة: 30»
/// — <b>رقمٌ بلا محتوى</b>. ثمّ يُطلب فتحُ المجلّد وإرفاقُ الملفّات، <b>فيُرسَل ملفٌّ واحدٌ
/// أو لا شيء</b> — ومن يختار من ثلاثين ملفّاً بأسماءٍ متشابهةٍ يختار خطأً.</para>
///
/// <para><b>ولماذا هنا لا في الـcode-behind:</b> ما يُكتب في شاشةٍ لا يُقاس إلّا بتشغيل
/// الشاشة. <b>والبناءُ منطقٌ خالص</b> — مجلّدٌ ونصٌّ يصيران أرشيفاً — فيُكتب حيث يُختبَر،
/// ولا تحمل الشاشةُ إلّا اختيارَ المسار.</para>
/// </summary>
public static class SupportBundle
{
    /// <summary>اسمُ ملفّ التقرير داخل الأرشيف — <b>أوّلُ ما يفتحه الفنّيّ</b>.</summary>
    public const string ReportEntryName = "00-status-report.txt";

    /// <summary>
    /// يبني أرشيفاً يحوي تقريرَ الحالة وكلَّ ملفّات مجلّد السجلّات.
    /// </summary>
    /// <param name="targetZipPath">مسارُ الأرشيف المطلوب إنشاؤه.</param>
    /// <param name="statusReport">نصُّ تقرير الحالة كما يراه المُشغِّل على الشاشة.</param>
    /// <param name="logDirectory">مجلّدُ السجلّات — يُقبل غيابُه.</param>
    /// <returns>كم ضُمّ وكم تعذّر — <b>والثاني يُبلَّغ ولا يُبتلع</b>.</returns>
    public static (int Added, int Unreadable) Create(
        string targetZipPath, string statusReport, string logDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetZipPath);

        var folder = Path.GetDirectoryName(Path.GetFullPath(targetZipPath));
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        // ‏**يُستبدَل لا يُضاف إليه**: أرشيفٌ سابقٌ بالاسم نفسِه يجعل الفنّيَّ يقرأ سجلَّ
        // ‏مكالمةٍ ماضية ويظنّه الحاليّ — وهو أسوأُ من ألّا يصل شيء.
        if (File.Exists(targetZipPath))
            File.Delete(targetZipPath);

        using var archive = ZipFile.Open(targetZipPath, ZipArchiveMode.Create);

        // ‏التقريرُ أوّلاً واسمُه يبدأ بصفرَين — **فيقع في رأس القائمة مهما رُتّبت**.
        var reportEntry = archive.CreateEntry(ReportEntryName, CompressionLevel.Optimal);
        using (var writer = new StreamWriter(reportEntry.Open(), new System.Text.UTF8Encoding(true)))
            writer.Write(statusReport);

        if (!Directory.Exists(logDirectory))
            return (0, 0);

        var added = 0;
        var unreadable = 0;

        foreach (var file in Directory.GetFiles(logDirectory))
        {
            try
            {
                archive.CreateEntryFromFile(file, "logs/" + Path.GetFileName(file), CompressionLevel.Optimal);
                added++;
            }
            catch (IOException)
            {
                // ‏ملفٌّ محجوزٌ لحظةَ النسخ **لا يُسقط الحزمة كلَّها** — والباقي أنفعُ من لا شيء.
                // ‏**ولا يُبتلع صامتاً**: يُعدّ ويُعاد، فتقول الشاشةُ للمُشغِّل كم تعذّر —
                // ‏وحزمةٌ ناقصةٌ يظنّها كاملةً أسوأُ من حزمةٍ تقول ما ينقصها.
                unreadable++;
            }
        }

        return (added, unreadable);
    }

    /// <summary>اسمٌ مقترحٌ يحمل الجهازَ والختمَ — <b>فمكالمتان لا يتشابه ملفّاهما</b>.</summary>
    public static string SuggestedFileName(DateTime now) =>
        $"brqosh-support-{Environment.MachineName}-{now:yyyy-MM-dd_HHmm}.zip";
}
