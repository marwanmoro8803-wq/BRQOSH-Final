using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;

namespace Brqosh.Application.Tests.Operations;

/// <summary>
/// (م-٣ · ن-٣٣) <b>عطلٌ يتكرّر كان يمحو دليلَ ما سبقه — والسببُ الجذريُّ أوّلُ ما يُمحى.</b>
///
/// <para><b>عطلان في ملفٍّ واحد، والثاني كان يستر الأوّل:</b></para>
/// <list type="number">
///   <item><b>الاسمُ يدهس نفسَه.</b> كان <c>crash-{ثانية}-{خيط}.log</c>، و<c>WriteAllText</c>
///     يقصّ الموجود. فعطلان في الثانية الواحدة على الخيط نفسِه ⟵ <b>ملفٌّ واحد</b>،
///     والأوّلُ يُمحى بالذي تلاه — <b>وهو غالباً السببُ الجذريّ</b>.</item>
///   <item><b>والتقليمُ بالعدد وحدَه:</b> <c>Skip(30)</c> على الأحدث فالأقدم، بلا حدٍّ لكلّ
///     سياق. فعطلٌ متكرّرٌ يكتب ثلاثين سجلاً <b>ويمحو كلَّ عطلٍ سبقه</b>.</item>
/// </list>
///
/// <para><b>وتصحيحُ صياغة البند:</b> قيل «يمحو دليلَ الجلسة في ثوانٍ»، <b>وذلك غيرُ دقيقٍ
/// حسابيّاً</b> — الاسمُ بدقّة الثانية كان يدهس نفسَه، فثلاثون ملفّاً تلزمها ثلاثون ثانية.
/// <b>والمفارقةُ أنّ إصلاحَ الدهس هو ما يجعل المحوَ ممكناً فعلاً</b>: فالطبقتان تلزمان معاً،
/// وإصلاحُ الأولى وحدَها كان يُنتج العطلَ الذي وُصف.</para>
///
/// <para><b>ولمَ حصّةٌ لكلّ سياقٍ لا حدٌّ زمنيّ:</b> الحدُّ الزمنيُّ يحرس القديمَ ويترك
/// الفيضانَ يملأ القرص. <b>والحصّةُ تحدّ الفيضانَ نفسَه</b> فتُبقي مكاناً لأعطالٍ مختلفة —
/// <b>وتنوّعُ الأسباب هو ما يُشخِّص، لا عددُ نسخِ سببٍ واحد</b>.</para>
/// </summary>
public sealed class CrashLogKeepsTheRootCauseTests : IDisposable
{
    private const string RootCause = "root-cause";
    private const string Flood = "flood";

    /// <summary>
    /// (م-٥ · ن-٥٤) <b>مجلّدٌ خاصٌّ بهذا الصفّ — وكان يتشارك مجلّدَ الجهاز مع الحزمة كلِّها.</b>
    ///
    /// <para><b>مقيسٌ أنّه كان يسقط متقطّعاً</b>: <c>EXIT=1</c> في تشغيلةٍ كاملةٍ و<c>EXIT=0</c>
    /// في التي تلتها بلا أيّ تغيير، و<c>0/6</c> فشلٍ منفرداً. والسبب: المجلّدُ عامٌّ على
    /// الجهاز، وصفوفٌ أخرى تكتب فيه أثناء التشغيل (قِيست سجلّاتُ <c>OutboxDispatchHook</c>
    /// فيه)، <b>فيُقلَّم ما كتبه هذا الصفُّ لتوّه ويسقط على سلوكٍ صحيح</b>.</para>
    ///
    /// <para><b>والعزلُ بالمجلّد لا بتليين الاختبار</b>: ما يقيسه الصفُّ سياسةُ التقليم،
    /// <b>وتليينُه ليحتمل الضجيجَ يُفقده ما جاء يقيسه</b>.</para>
    /// </summary>
    private readonly string _isolated =
        Path.Combine(Path.GetTempPath(), "n54-" + Guid.NewGuid().ToString("N"));

    public CrashLogKeepsTheRootCauseTests()
    {
        Directory.CreateDirectory(_isolated);
        CrashLog.DirectoryOverrideSlot.Value = _isolated;
        Clear();
    }

    public void Dispose()
    {
        Clear();
        CrashLog.DirectoryOverrideSlot.Value = null;
        try { Directory.Delete(_isolated, recursive: true); }
        catch (IOException) { /* ‏تنظيفٌ لا يُسقط اختباراً */ }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// <b>والعزلُ نافذٌ فعلاً — لا مُعلَنٌ فحسب.</b> بلا هذا الشاهد يبقى الصفُّ يكتب في
    /// مجلّد الجهاز ويبدو معزولاً، <b>فيعود التقطّعُ ولا يُعرف لماذا</b>.
    /// </summary>
    [Fact]
    public void TheLogDirectory_IsIsolatedFromTheMachine()
    {
        Assert.Equal(_isolated, CrashLog.Directory);

        var machineRoot = Path.Combine(BrqoshDataRoot.Path(), "logs");
        Assert.NotEqual(machineRoot, CrashLog.Directory);

        // ‏وشاهدُ ضبط: الكتابةُ تقع في المعزول فعلاً، لا في مجلّدٍ ثالث.
        var written = CrashLog.Write(new InvalidOperationException("عزل"), "n54-isolation");
        Assert.NotNull(written);
        Assert.StartsWith(_isolated, written!, StringComparison.Ordinal);
    }

    /// <summary>
    /// (م-٥ · ن-٥٤) <b>دفقةٌ في الميلّي ثانية الواحدة — والقياسُ حتميٌّ لا احتماليّ.</b>
    ///
    /// <para><c>TwoCrashesInTheSameInstant_AreBothKept</c> كان يكتب اثنين فيسقط <b>واحدةً من
    /// كلّ خمس</b> — لأنّ التصادمَ يقع حين تقع الكتابتان في الميلّي ثانية نفسِها، <b>وذلك
    /// لا يُضمن</b>. فظُنّ العطلُ تقطّعاً في الاختبار وهو <b>العطلُ الأصليُّ قائماً بدقّةٍ
    /// أنعم</b>.</para>
    ///
    /// <para><b>وعشرون كتابةً متتاليةً تضمن التصادم</b> بلا انتظارٍ ولا عشوائيّة: إن كان
    /// الاسمُ يدهس نفسَه فالملفّاتُ أقلُّ من العشرين حتماً. <b>واختبارٌ يصيب واحدةً من خمسٍ
    /// لا يحرس شيئاً</b> — يُقرأ تقطّعاً فيُهمَل.</para>
    /// </summary>
    [Fact]
    public void ABurstWithinOneMillisecond_KeepsEveryReport()
    {
        const int burst = 20;

        var paths = new List<string>();
        for (var i = 0; i < burst; i++)
            paths.Add(CrashLog.Write(new InvalidOperationException($"دفقة {i}"), RootCause)!);

        Assert.All(paths, Assert.NotNull);

        // ‏شاهدُ ضبط: كلُّها في المجلّد المعزول — فالعدُّ عن كتابةٍ لا عن مجلّدٍ آخر.
        Assert.All(paths, p => Assert.StartsWith(_isolated, p, StringComparison.Ordinal));

        Assert.Equal(burst, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        // ‏**وحصّةُ السياق تُقلّم بعدها** — فالباقي على القرص خمسةٌ بحكم `PerContextLimit`،
        // ‏وهو سلوكٌ مقصود. والمقيسُ هنا **أنّ الأسماء لم تتصادم**، لا أنّ الكلَّ باقٍ.
        Assert.Equal(burst, paths.Count);

        // ‏**والمسارُ المُعاد قائمٌ على القرص** — فالتقليمُ لا يمحو ما كُتب للتوّ وإن تساوت الأوقاتُ في الميلّي ثانية.
        Assert.True(File.Exists(paths[^1]), "‏قُلّم التقريرُ المكتوبُ للتوّ — والمستدعي يعرض مساراً لا وجودَ له.");
    }

    private static void Clear()
    {
        if (!Directory.Exists(CrashLog.Directory)) return;
        foreach (var file in Directory.GetFiles(CrashLog.Directory, "crash-*.log"))
            try { File.Delete(file); } catch (IOException) { /* ‏تنظيفٌ لا يُسقط اختباراً */ }
    }

    /// <summary>
    /// <b>القياسُ الأوّل:</b> عطلان متتاليان في اللحظة نفسِها يُنتجان ملفّين لا واحداً.
    ///
    /// <para>وهذا العطلُ <b>أخطرُ من المحو</b>: المحوُ يُبقي أحدثَ ثلاثين، والدهسُ يُبقي
    /// <b>الأخيرَ وحدَه</b> من دفقةٍ كاملة — فيضيع البلاغُ الأوّلُ بلا أثرٍ يدلّ عليه.</para>
    /// </summary>
    [Fact]
    public void TwoCrashesInTheSameInstant_AreBothKept()
    {
        var first = CrashLog.Write(new InvalidOperationException("الأوّل"), RootCause);
        var second = CrashLog.Write(new InvalidOperationException("الثاني"), RootCause);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
        Assert.True(File.Exists(first!), "‏دُهس السجلُّ الأوّل بالثاني — ويضيع البلاغُ بلا أثر.");
        Assert.Contains("الأوّل", File.ReadAllText(first!));
    }

    /// <summary>
    /// <b>القياسُ المركزيّ:</b> فيضانٌ من سياقٍ واحدٍ لا يمحو عطلاً مختلفاً سبقه.
    ///
    /// <para>وتُزرع ملفّاتُ الفيضان بطوابعَ متمايزةٍ بدل انتظار أربعين ثانيةً حقيقيّة —
    /// <b>فالمقيسُ سياسةُ التقليم لا سرعةُ الكتابة</b>.</para>
    /// </summary>
    [Fact]
    public void AFloodFromOneContext_DoesNotEraseAnEarlierDifferentCrash()
    {
        Directory.CreateDirectory(CrashLog.Directory);

        // ‏السببُ الجذريُّ: أقدمُ ملفٍّ في المجلّد.
        var rootCause = Plant(RootCause, new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc));

        // ‏ثمّ أربعون سجلاً من عطلٍ واحدٍ يتكرّر — كلُّها أحدثُ منه.
        for (var i = 0; i < 40; i++)
            Plant(Flood, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc).AddSeconds(i));

        // ‏وكتابةٌ جديدةٌ تُشغّل التقليم.
        CrashLog.Write(new InvalidOperationException("محفّز"), "trigger");

        Assert.True(File.Exists(rootCause),
            "‏مُحي السببُ الجذريُّ بفيضانِ عطلٍ متكرّر — وهو أوّلُ ما يُبحث عنه.");
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب — وهو ألزمُ من القياس:</b> الفيضانُ يُقلَّم ولا يُمحى بالكامل.
    ///
    /// <para>فسياسةٌ تحذف الفيضانَ كلَّه تحرس القرصَ وتُضيّع الخبر: تكرارُ عطلٍ أربعين مرّةً
    /// <b>معلومةٌ تشخيصيّةٌ بنفسها</b>. ويُحرس الطرفان: يبقى منه شيء، ولا يبقى كلُّه.</para>
    /// </summary>
    [Fact]
    public void TheFloodItself_IsCappedButNotErased()
    {
        Directory.CreateDirectory(CrashLog.Directory);

        for (var i = 0; i < 40; i++)
            Plant(Flood, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc).AddSeconds(i));

        CrashLog.Write(new InvalidOperationException("محفّز"), "trigger");

        var remaining = Directory.GetFiles(CrashLog.Directory, $"crash-*__{Flood}__*.log").Length;

        Assert.True(remaining >= 1, "‏مُحي الفيضانُ كلُّه — وتكرارُ العطل معلومةٌ تشخيصيّة.");
        Assert.True(remaining <= 10,
            $"‏بقي {remaining} سجلاً من عطلٍ واحد — والحصّةُ لم تعمل، فيبتلع الأدلّةَ.");
    }

    /// <summary>
    /// <b>والسياقُ نصٌّ يُمرّره المستدعي</b> — وقد يحمل مسافةً أو شرطةً مائلة. فيُقصر على ما
    /// يقبله نظامُ الملفّات، <b>وإلّا رمت الكتابةُ داخل معالجِ استثناءٍ فأسقطت التطبيقَ صامتاً</b>.
    /// </summary>
    [Theory]
    [InlineData("مسار/فيه شرطة")]
    [InlineData("سياق فيه مسافات")]
    [InlineData("")]
    public void AnUnsafeContext_StillProducesAWritableFile(string context)
    {
        var path = CrashLog.Write(new InvalidOperationException("اختبار"), context);

        Assert.NotNull(path);
        Assert.True(File.Exists(path!));
    }

    private static string Plant(string context, DateTime stampUtc)
    {
        var path = Path.Combine(CrashLog.Directory,
            $"crash-{stampUtc:yyyy-MM-dd_HHmmss.fff}__{context}__{Guid.NewGuid().ToString("N")[..6]}.log");

        File.WriteAllText(path, $"عطلٌ مزروعٌ للقياس — {context}");
        File.SetLastWriteTimeUtc(path, stampUtc);
        return path;
    }
}
