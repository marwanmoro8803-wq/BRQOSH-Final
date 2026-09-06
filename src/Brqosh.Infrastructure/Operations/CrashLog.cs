using System.Reflection;
using System.Text;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Infrastructure.Operations;

/// <summary>
/// (CEEF-231 · ط5-٦) <b>سجلُّ الأعطال — وبغيره لا يُشخَّص عطلٌ في الميدان.</b>
///
/// <para><b>ما كان:</b> معالجا الاستثناء في <c>App.xaml.cs</c> يعرضان <c>ex.Message</c>
/// في صندوقٍ عربيٍّ <b>ولا يكتبان حرفاً على القرص</b>. فمنشأةٌ يقع عندها عطلٌ تتصل
/// بالدعم فتقول «ظهرت رسالة» — <b>ولا أثرَ لِما وقع</b>: لا مكدّس، ولا الشاشة التي
/// كانت مفتوحة، ولا إصدار، ولا محرّك القاعدة. <b>فيُطلب منها إعادةُ العطل</b>، وهي
/// تمسك دفاترَها الحقيقية.</para>
///
/// <para><b>والمكدّسُ هو الملفّ كلُّه.</b> رسالةُ الاستثناء تقول «ماذا» ولا تقول «أين» —
/// و«Object reference not set» رسالةٌ لا تُميّز موضعاً من ألف. <b>فيُكتب المكدّسُ كاملاً
/// مع كلّ استثناءٍ داخليّ</b>، لأنّ الرسالةَ الظاهرة عادةً غلافُ ما وقع لا هو.</para>
///
/// <para><b>ولا يُكتب في مجلّد التثبيت</b>: <c>Program Files</c> لا يُكتب فيه بلا صلاحيةِ
/// مدير — <b>فسجلٌّ هناك لا يُكتب في اللحظة التي يلزم فيها</b>. ويُكتب في جذر البيانات
/// نفسِه الذي تُكتب فيه النسخُ الاحتياطية.</para>
///
/// <para><b>وكتابةُ السجلّ لا ترمي أبداً.</b> فهي تجري داخل معالج استثناء، <b>واستثناءٌ
/// في المعالج يُخفي الاستثناءَ الأصليَّ ويُسقط التطبيق بلا رسالةٍ أصلاً</b>.</para>
/// </summary>
public static class CrashLog
{
    /// <summary>مجلّدُ السجلّات — <b>في جذر البيانات لا في مجلّد التثبيت</b>.</summary>
    public static string Directory =>
        Path.Combine(BrqoshDataRoot.Path(), "logs");

    /// <summary>
    /// يكتب عطلاً ويُعيد مسارَ الملفّ — <b>و<c>null</c> إن تعذّرت الكتابة نفسُها</b>.
    /// </summary>
    /// <param name="context">
    /// أين وقع — <b>وهو ما يُضيّق البحث من ألف موضعٍ إلى واحد</b>.
    /// </param>
    public static string? Write(Exception exception, string context)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);

            var path = Path.Combine(Directory,
                $"crash-{DateTime.Now:yyyy-MM-dd_HHmmss}-{Environment.CurrentManagedThreadId}.log");

            File.WriteAllText(path, Compose(exception, context), new UTF8Encoding(true));

            Prune();
            return path;
        }
        catch (Exception)
        {
            // ‏**ولا يرمي أبداً**: هذه تجري داخل معالج استثناء، **واستثناءٌ هنا يُخفي
            // ‏الأصليَّ ويُسقط التطبيق بلا رسالةٍ أصلاً**. والفشلُ يُبلَّغ بـ`null`
            // ‏فيعرض المستدعي الرسالةَ وحدَها.
            return null;
        }
    }

    /// <summary>نصُّ السجلّ — <b>مُستخرَجٌ ليُقاس بلا قرص</b>.</summary>
    public static string Compose(Exception exception, string context)
    {
        var text = new StringBuilder();

        text.AppendLine("BRQOSH — سجل عطل");
        text.AppendLine($"الوقت      : {DateTime.Now:yyyy-MM-dd HH:mm:ss} ({TimeZoneInfo.Local.Id})");
        text.AppendLine($"الموضع     : {context}");
        text.AppendLine($"الإصدار    : {Version()}");
        text.AppendLine($"النظام     : {Environment.OSVersion} · {(Environment.Is64BitProcess ? "x64" : "x86")}");
        text.AppendLine($"دوت نت     : {Environment.Version}");
        text.AppendLine($"المستخدم   : {Environment.UserName}@{Environment.MachineName}");
        text.AppendLine($"جذر البيانات: {SafeDataRoot()}");
        text.AppendLine(new string('─', 70));

        // ‏**وكلُّ استثناءٍ داخليٍّ يُكتب**: الرسالةُ الظاهرةُ غلافُ ما وقع عادةً لا هو —
        // ‏و«حدث خطأ أثناء الحفظ» تُخفي `23505` الذي يقول أيَّ قيدِ تفرّدٍ انكسر.
        var depth = 0;
        for (var current = exception; current is not null; current = current.InnerException)
        {
            text.AppendLine($"[{depth}] {current.GetType().FullName}");
            text.AppendLine($"    {current.Message}");

            if (!string.IsNullOrWhiteSpace(current.StackTrace))
                text.AppendLine(current.StackTrace);

            text.AppendLine();
            depth++;
        }

        return text.ToString();
    }

    /// <summary>
    /// يُبقي أحدثَ ثلاثين سجلاً.
    /// </summary>
    /// <remarks>
    /// <b>وسجلٌّ ينمو بلا حدٍّ يملأ قرصَ المستخدم</b> — وعطلٌ يتكرّر كلَّ ثانيةٍ يكتب
    /// ألوفاً في دقائق. <b>والأحدثُ هو المفيد</b>: أوّلُ الأعطال يُشخَّص من آخرِ وقوعه.
    /// </remarks>
    private static void Prune()
    {
        try
        {
            var stale = new DirectoryInfo(Directory)
                .GetFiles("crash-*.log")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(30);

            foreach (var file in stale)
            {
                try { file.Delete(); } catch (IOException) { }
            }
        }
        catch (Exception)
        {
            // ‏تنظيفٌ يفشل لا يمنع كتابةَ السجلّ — وهو الغرضُ الأصليّ.
        }
    }

    /// <summary>
    /// (م-١ · ن-٣٧) <b>هويّةُ النسخة — من البصمة لا من الرقم المُدوَّر.</b>
    ///
    /// <para><c>AssemblyVersion</c> يُدوَّر عمداً (‏<c>1.0.42.0</c>) ولا يحمل ما يُميّز بناءً
    /// من بناء. و<c>InformationalVersion</c> يحمل بصمةَ الالتزام كاملةً عبر SourceLink
    /// (‏<c>1.0.42+64922842…</c>) — <b>وهي الوحيدةُ التي تُجيب «أيُّ كودٍ يعمل عند هذا
    /// العميل؟»</b>، وهو أوّلُ سؤالٍ في كلّ مكالمةِ دعم.</para>
    ///
    /// <para>والسقوطُ إلى <c>AssemblyVersion</c> مقصودٌ لا كسول: تجميعةٌ بُنيت بلا SourceLink
    /// تُعطي رقماً بلا بصمة، وذلك خيرٌ من «غير معروف».</para>
    /// </summary>
    private static string Version()
    {
        var entry = Assembly.GetEntryAssembly() ?? typeof(CrashLog).Assembly;
        var informational = entry.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational)) return informational;
        return entry.GetName().Version?.ToString() ?? "غير معروف";
    }

    private static string SafeDataRoot()
    {
        try { return BrqoshDataRoot.Path(); }
        catch (Exception) { return "غير معروف"; }
    }
}
