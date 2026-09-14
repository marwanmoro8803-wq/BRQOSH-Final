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

            // ‏(م-٣ · ن-٣٣) ‏**الاسمُ كان يدهس نفسَه — فبلاغاتٌ تضيع بلا أثر.**
            //
            // ‏كان `crash-{ثانية}-{خيط}.log`، و`WriteAllText` يقصّ الموجود. فعطلان في
            // ‏الثانية الواحدة على الخيط نفسِه ⟵ **ملفٌّ واحد**، والأوّلُ — وهو غالباً
            // ‏السببُ الجذريّ — يُمحى بالذي تلاه. فأُضيفت أجزاءُ الثانية.
            //
            // ‏**والسياقُ في الاسم شرطٌ للحصّة أدناه**، لا زينة: بغيره لا يُعرف أيُّ الملفّات
            // ‏من عطلٍ متكرّرٍ واحدٍ وأيُّها من أعطالٍ مختلفة.
            var path = Path.Combine(Directory,
                $"crash-{DateTime.Now:yyyy-MM-dd_HHmmss.fff}{ContextSeparator}{Slug(context)}"
                + $"{ContextSeparator}{Environment.CurrentManagedThreadId}.log");

            File.WriteAllText(path, Compose(exception, context), new UTF8Encoding(true));

            Prune();
            PruneSchemaReports();
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
    private const string ContextSeparator = "__";

    /// <summary>حصّةُ كلّ سياق — فلا يبتلع عطلٌ متكرّرٌ الأدلّةَ كلَّها.</summary>
    private const int PerContextLimit = 5;

    /// <summary>الحدُّ الكلّيّ — يحرس القرص.</summary>
    private const int TotalLimit = 30;

    /// <summary>
    /// (م-٣ · ن-٣٣) <b>حصّةٌ لكلّ سياقٍ ثمّ حدٌّ كلّيّ — وكان الحدُّ الكلّيُّ وحدَه.</b>
    ///
    /// <para><b>العطل:</b> <c>Skip(30)</c> على الأحدث فالأقدم، <b>بلا حدٍّ لكلّ سياق</b>.
    /// فعطلٌ يتكرّر — إخفاقُ إرسالٍ دوريّ، أو ناشرُ أحداثٍ يرمي في كلّ ترحيل — يكتب ثلاثين
    /// سجلاً <b>ويمحو كلَّ عطلٍ سبقه</b>، بما فيه السببُ الجذريُّ الذي أنتجه.</para>
    ///
    /// <para><b>وتصحيحُ صياغة البند:</b> قيل «يمحو دليلَ الجلسة في ثوانٍ»، <b>وذلك غيرُ
    /// دقيقٍ حسابيّاً</b>: الاسمُ القديم بدقّة الثانية كان يدهس نفسَه، فثلاثون ملفّاً تلزمها
    /// ثلاثون ثانيةً لا ثوانٍ. <b>والدهسُ عطلٌ ثانٍ كان يستر الأوّل</b> — وقد أُصلح أعلاه،
    /// وبإصلاحه <b>صار المحوُ ممكناً فعلاً</b>. فالطبقتان تلزمان معاً.</para>
    ///
    /// <para><b>ولمَ الحصّةُ لا الحدُّ الزمنيّ:</b> حدٌّ زمنيٌّ يحرس القديمَ ويترك الفيضانَ
    /// يملأ القرص؛ والحصّةُ تحدّ <b>الفيضانَ نفسَه</b> فتُبقي مكاناً لأعطالٍ مختلفة —
    /// وتنوّعُ الأسباب هو ما يُشخِّص، لا عددُ نسخِ سببٍ واحد.</para>
    /// </summary>
    /// <summary>
    /// (م-٥ · ن-٤٣) <b>سقفُ تقارير المخطّط — وكانت بلا سقفٍ إطلاقاً.</b>
    ///
    /// <para><c>BrqoshSchemaUpdater</c> يكتب <c>schema-*.log</c> في <b>هذا المجلّد نفسِه</b>،
    /// <b>ونمطُ التقليم كان <c>crash-*.log</c> وحدَه</b>. فقاعدةٌ فيها خطوةُ ترقيةٍ متسامحةٌ
    /// تفشل دائماً تكتب ملفّاً <b>كلَّ إقلاع</b> ولا يُمحى شيء — <b>فتُدفن سجلّاتُ الأعطال
    /// الثلاثون بين مئاتٍ متطابقة</b>، ويفتح الفنّيُّ المجلّدَ ولا يعرف أيُّها الدليل.</para>
    /// </summary>
    public const int SchemaReportLimit = 10;

    /// <summary>
    /// <b>حصّةٌ لكلّ عائلةٍ لا حصّةٌ مشتركة — وهو قرارٌ مقصود.</b>
    ///
    /// <para>لو دخلت تقاريرُ المخطّط في الحدّ الكلّيّ نفسِه <b>لأكل الفيضانُ حصّةَ
    /// الأعطال</b> — وهو بعينه العطلُ الذي أُغلق في م-٣ · ن-٣٣، معكوساً: هناك كان عطلٌ
    /// متكرّرٌ يطرد أعطالاً مختلفة، وهنا كان ضجيجُ الترقية يطرد الأعطالَ كلَّها.</para>
    /// </summary>
    public static void PruneSchemaReports() =>
        PruneFamily("schema-*.log", perContextLimit: int.MaxValue, totalLimit: SchemaReportLimit);

    private static void Prune() =>
        PruneFamily("crash-*.log", PerContextLimit, TotalLimit);

    private static void PruneFamily(string pattern, int perContextLimit, int totalLimit)
    {
        try
        {
            var files = new DirectoryInfo(Directory).GetFiles(pattern);

            var keep = files
                .GroupBy(ContextOf, StringComparer.OrdinalIgnoreCase)
                .SelectMany(g => g.OrderByDescending(f => f.LastWriteTimeUtc).Take(perContextLimit))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(totalLimit)
                .Select(f => f.FullName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files.Where(f => !keep.Contains(f.FullName)))
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
    /// السياقُ من اسم الملفّ. <b>وملفٌّ بالاسم القديم يُنسب إلى سياقٍ فارغ</b> — فتجتمع
    /// ملفّاتُ ما قبل الترقية في حصّةٍ واحدة، وذلك مقصود: لا تُعرف سياقاتُها.
    /// </summary>
    private static string ContextOf(FileInfo file)
    {
        var parts = Path.GetFileNameWithoutExtension(file.Name)
            .Split(ContextSeparator, StringSplitOptions.None);
        return parts.Length >= 3 ? parts[1] : string.Empty;
    }

    /// <summary>
    /// اسمُ ملفٍّ آمنٌ من السياق. <b>والسياقُ نصٌّ يُمرّره المستدعي</b>، وقد يحمل مسافةً أو
    /// شرطةً مائلةً أو محرفاً لا يقبله نظامُ الملفّات — فيُقصر على ما يصلح، ويُحدَّد طولُه.
    /// </summary>
    private static string Slug(string? context)
    {
        if (string.IsNullOrWhiteSpace(context)) return "unknown";

        var sb = new StringBuilder(context.Length);
        foreach (var ch in context.Trim())
        {
            if (sb.Length >= 40) break;
            sb.Append(char.IsAsciiLetterOrDigit(ch) || ch is '-' or '.' ? ch : '-');
        }

        return sb.Length == 0 ? "unknown" : sb.ToString();
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
