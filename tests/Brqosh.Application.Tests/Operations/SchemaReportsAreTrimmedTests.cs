using Brqosh.Infrastructure.Operations;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (م-٥ · ن-٤٣) <b>تقاريرُ المخطّط تُقلَّم كما تُقلَّم سجلّاتُ الأعطال — وكانت لا تُقلَّم أبداً.</b>
///
/// <para><b>ما كان قبلها:</b> <c>BrqoshSchemaUpdater.WriteReportIfAny</c> يكتب
/// <c>schema-&lt;ختم&gt;.log</c> في <b>مجلّد السجلّات نفسِه</b>، و<c>CrashLog.Prune</c> يسرد
/// <c>crash-*.log</c> <b>وحدَها</b> — فالنمطُ يستثني تقاريرَ المخطّط بالكامل.</para>
///
/// <para><b>ومتى يعضّ:</b> قاعدةُ منشأةٍ فيها خطوةُ ترقيةٍ متسامحةٌ تفشل دائماً (امتدادٌ غيرُ
/// مثبَّت، أو صلاحيّةٌ ناقصة) — <b>فكلُّ إقلاعٍ يكتب ملفّاً جديداً</b>، وبعد سنتين من التشغيل
/// اليوميّ مئاتُ الملفّات. <b>وسجلّاتُ الأعطال الثلاثون تُدفن بينها</b>، فيفتح الفنّيُّ
/// «مجلد السجلات» ولا يعرف أيُّها الدليل.</para>
///
/// <para><b>وحصّةٌ مستقلّةٌ لا حصّةٌ مشتركة — وهو قرارٌ مقصود:</b> لو دخلت تقاريرُ المخطّط
/// في الحدّ الكلّيّ نفسِه (ثلاثين) <b>لأكل الفيضانُ حصّةَ الأعطال</b> — وهو العطلُ الذي
/// أُغلق في م-٣ · ن-٣٣ بعينه، معكوساً. فلكلّ عائلةٍ سقفُها.</para>
/// </summary>
public sealed class SchemaReportsAreTrimmedTests : IDisposable
{
    private const string Marker = "n43";

    /// <summary>
    /// السقفُ المتوقَّع — <b>مكتوبٌ هنا لا مُستورَدٌ من المنتج</b>، لسببين: ليجري الكناريُّ
    /// على الشجرة **قبل** الإصلاح (حيث لا ثابتَ أصلاً)، <b>ولأنّ اختباراً يستورد الرقمَ
    /// الذي يحرسه لا يحرس شيئاً</b> — وهو فخٌّ سجّله المستودعُ عن نفسه في CEEF-236.
    /// فإن رُفع السقفُ عمداً سقط هذا السطرُ وطُلبت مراجعةٌ بشريّة، وذلك هو المقصود.
    /// </summary>
    private const int ExpectedSchemaReportLimit = 10;
    private readonly List<string> _planted = [];

    public void Dispose()
    {
        foreach (var path in _planted)
            try { File.Delete(path); } catch (IOException) { /* ‏تنظيفٌ لا يُسقط اختباراً */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>يزرع تقريرَ مخطّطٍ بختمٍ متمايز — <b>فالمقيسُ سياسةُ التقليم لا سرعةُ الكتابة</b>.</summary>
    private string PlantSchemaReport(DateTime stamp)
    {
        Directory.CreateDirectory(CrashLog.Directory);
        var path = Path.Combine(CrashLog.Directory, $"schema-{Marker}-{stamp:yyyy-MM-dd_HHmmss.fff}.log");
        File.WriteAllText(path, "‏خطوةُ ترقيةٍ ابتُلع فشلُها (زُرعت لاختبار التقليم).");
        File.SetLastWriteTimeUtc(path, stamp);
        _planted.Add(path);
        return path;
    }

    private static int SchemaReportCount() =>
        Directory.Exists(CrashLog.Directory)
            ? Directory.GetFiles(CrashLog.Directory, "schema-*.log").Length
            : 0;

    /// <summary>
    /// <b>القياسُ المركزيّ:</b> فيضانُ تقاريرِ مخطّطٍ لا ينمو بلا حدّ.
    /// </summary>
    [Fact]
    public void AFloodOfSchemaReports_IsTrimmed()
    {
        var planted = 0;
        for (var i = 0; i < 40; i++)
        {
            PlantSchemaReport(new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc).AddSeconds(i));
            planted++;
        }

        // ‏شاهدُ ضبط: الزرعُ وقع فعلاً — فالعددُ التالي عن تقليمٍ لا عن كتابةٍ لم تحدث.
        Assert.Equal(40, planted);
        Assert.True(SchemaReportCount() >= 40,
            $"‏لم تُزرع التقارير أصلاً ({SchemaReportCount()}) — الاختبارُ يقيس مجلّداً خطأً.");

        // ‏وكتابةُ عطلٍ تُشغّل التقليم — وهو المدخلُ الوحيدُ إليه في المنتج.
        CrashLog.Write(new InvalidOperationException("‏عطلٌ يُشغّل التقليم"), "n43-trigger");

        var remaining = SchemaReportCount();
        Assert.True(remaining <= ExpectedSchemaReportLimit,
            $"‏تقاريرُ المخطّط لا تُقلَّم: بقي {remaining} من {planted}. "
            + "‏والمجلّدُ نفسُه تُقلَّم فيه سجلّاتُ الأعطال — **فالفيضانُ يدفنها ولا يُمحى**.");
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ ثانٍ: الأحدثُ هو الباقي</b> — فالتقليمُ يحفظ ما يُشخِّص لا ما يصادف.
    /// </summary>
    [Fact]
    public void TheNewestSchemaReports_AreTheOnesKept()
    {
        for (var i = 0; i < 30; i++)
            PlantSchemaReport(new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc).AddSeconds(i));

        var newest = PlantSchemaReport(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc));

        CrashLog.Write(new InvalidOperationException("‏عطلٌ يُشغّل التقليم"), "n43-newest");

        Assert.True(File.Exists(newest),
            "‏التقليمُ محا أحدثَ تقرير — وأوّلُ ما يُشخَّص به آخرُ وقوع.");
    }

    /// <summary>
    /// <b>وحصّةُ الأعطال لا تأكلها تقاريرُ المخطّط</b> — وهو سببُ الحصّتين المنفصلتين.
    /// </summary>
    [Fact]
    public void AFloodOfSchemaReports_DoesNotEvictACrashLog()
    {
        var crash = CrashLog.Write(new InvalidOperationException("‏السببُ الجذريّ"), "n43-rootcause");
        Assert.NotNull(crash);

        for (var i = 0; i < 40; i++)
            PlantSchemaReport(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc).AddSeconds(i));

        CrashLog.Write(new InvalidOperationException("‏عطلٌ يُشغّل التقليم"), "n43-rootcause");

        Assert.True(File.Exists(crash!),
            "‏فيضانُ تقاريرِ المخطّط محا سجلَّ عطل — **وحصّةٌ مشتركةٌ تجعل الضجيجَ يطرد الدليل**.");
    }
}
