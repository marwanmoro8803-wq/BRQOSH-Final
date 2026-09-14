namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// (م-٦ · ن-٢٢ · تبعيّة) <b>كلُّ أداةٍ تحلّ اتّصالَ الإنتاج تسأل قبل أن تلمس.</b>
///
/// <para><b>ولماذا وُلدت هذه البوّابة:</b> ن-٢٢ ركّب <c>LiveBooksGuard</c> على
/// <c>FullScenarioAudit</c> <b>وحدَها</b>. والمقيسُ بعد ذلك أنّ <b>أربعَ</b> أدواتٍ تحلّ
/// الاتّصالَ من <c>DatabaseConfigFile</c> — أي من <b>دفاتر المُشغِّل</b> — وثلاثٌ منها كانت
/// بلا حارس:</para>
///
/// <list type="bullet">
///   <item><c>DbStartupProbe</c> — <b>وهي أعنفُها</b>: تُنشئ منشأةً، وتُجري تعديلاتِ مخطّطٍ
///   (DDL)، وتزرع حسابَ مدير — <b>بلا سؤالٍ قطّ</b>. واسمُها «مِسبار» يُقرأ قراءةً.</item>
///   <item><c>PrintEvidence</c> — «مُصدِّرُ أدلّة» يُظنّ قارئاً، <b>وكلُّ تصديرٍ يكتب صفَّ
///   تدقيقٍ في سلسلةٍ مربوطةٍ بالتجزئة</b> لا يُحذف منها صفٌّ بلا كسرِها.</item>
///   <item><c>PasswordReset</c> — فيها سؤالُ تأكيدٍ، <b>لكنّه لا يُسمّي القاعدة</b>: يطبع
///   المزوّدَ لا المضيفَ واسمَ القاعدة، <b>فمن أخطأ الهدفَ يؤكّد بثقة</b>.</item>
/// </list>
///
/// <para><b>والدرسُ الذي تُجسّده:</b> إصلاحٌ يُركَّب على المستدعي الذي كُشف فيه العطلُ
/// <b>يترك إخوتَه</b>. وهو نمطٌ سُجّل في هذه الحملة مرّتين قبل اليوم (<c>ن-١٥</c>:
/// «نطاقُ إصلاحي أضيقُ من العلّة بمئة سطر»، و<c>ن-٠٩</c>: «دالّةٌ اسمُها وصل ولا تصل»).
/// <b>فالبوّابةُ هنا تحرس الصنفَ كلَّه لا الحالةَ المكشوفة</b>، وتلتقط الأداةَ الخامسة يومَ
/// تُكتب.</para>
///
/// <para><b>وحدُّها مكتوبٌ فيها ولا يُتجاوز:</b> تقيس <b>مسارَ <c>DatabaseConfigFile</c>
/// وحدَه</b> — وهو المسارُ الذي يبلغ دفاترَ المُشغِّل بلا أن يُذكر في سطر الأوامر. أداةٌ
/// تصل إلى قاعدةٍ حيّةٍ بطريقٍ آخر (سلسلةُ اتّصالٍ من وسيطٍ أو متغيّرِ بيئة) <b>خارج هذا
/// الحدّ</b>، ولا تُقرأ خضرةُ هذه البوّابة تزكيةً لها.</para>
/// </summary>
public class EveryLiveBooksCallerAsksFirstTests
{
    /// <summary>حلُّ اتّصالِ المُشغِّل — الطريقُ الذي يبلغ الدفاترَ بلا ذكرٍ في سطر الأوامر.</summary>
    private const string ResolvesLiveBooks = "DatabaseConfigFile";

    /// <summary>الحارسُ الذي يشترط الإذنَ باسمِ القاعدة.</summary>
    private const string Guard = "LiveBooksGuard.RequireConsentFor";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("لم يُعثر على جذر المستودع (Brqosh.sln).");
    }

    /// <summary>
    /// مصدرُ الملفّات لأداةٍ واحدة — <b>و<c>bin</c> و<c>obj</c> مستثنيان</b>. وبلا هذا
    /// الاستثناء يقرأ المسحُ ثنائيّاتٍ مبنيّةً فيجد كلَّ اسمٍ في كلّ أداةٍ <b>ويمرّ دائماً</b>
    /// — <b>وهو ما وقع لي حرفيّاً وأنا أقيس هذا البند</b>.
    /// </summary>
    private static List<string> SourcesOf(string toolDirectory) =>
        Directory.EnumerateFiles(toolDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

    private static List<(string Name, List<string> Sources)> Tools()
    {
        var root = Path.Combine(RepoRoot(), "tools");
        Assert.True(Directory.Exists(root), $"‏مجلّدُ الأدوات مفقود: {root}");

        return Directory.EnumerateDirectories(root)
            .Select(d => (Name: Path.GetFileName(d), Sources: SourcesOf(d)))
            .Where(t => t.Sources.Count > 0)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static bool Mentions(List<string> sources, string needle) =>
        sources.Any(f => File.ReadAllText(f).Contains(needle, StringComparison.Ordinal));

    /// <summary>
    /// <b>شاهدُ ضبطٍ من الصنف نفسِه، ويجري أوّلاً.</b> المسحُ يرى أدواتٍ حقيقيّةً،
    /// <b>ومنها واحدةٌ على الأقلّ تحلّ اتّصالَ الإنتاج</b>. <b>وبلا هذا</b> كان مجلّدٌ فارغٌ
    /// أو مرشّحٌ مكسورٌ يجعل البوّابةَ تمرّ بلا أن تفحص شيئاً — <b>وهو ن-٥٣ بعينه</b>.
    /// </summary>
    [Fact]
    public void TheScanActuallySeesTheTools()
    {
        var tools = Tools();

        Assert.True(tools.Count >= 10, $"‏المسحُ وجد {tools.Count} أداةً فقط — هل انكسر المسار؟");

        var live = tools.Where(t => Mentions(t.Sources, ResolvesLiveBooks)).ToList();
        Assert.True(live.Count > 0, "‏لا أداةَ تحلّ اتّصالَ الإنتاج — فالفحصُ أدناه بلا موضوع.");

        // ‏**وشاهدٌ على أنّ الاستثناءَ نافذ**: لا ملفَّ مصدرٍ من bin أو obj في القائمة.
        Assert.DoesNotContain(tools.SelectMany(t => t.Sources),
            f => f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryToolThatResolvesLiveBooks_AsksForConsentFirst()
    {
        var unguarded = Tools()
            .Where(t => Mentions(t.Sources, ResolvesLiveBooks) && !Mentions(t.Sources, Guard))
            .Select(t => t.Name)
            .ToList();

        Assert.True(unguarded.Count == 0,
            "‏أدواتٌ تفتح دفاترَ المُشغِّل بلا إذنٍ باسمِ القاعدة: " + string.Join("، ", unguarded)
            + $" — أضف {Guard} قبل إنشاء السياق.");
    }

    /// <summary>
    /// ‏يُميّز <b>كلمةً مزروعةً</b> من <b>اسمِ متغيّرِ بيئةٍ</b> ومن <b>رسالةٍ للمستخدم</b>.
    ///
    /// <para><b>وهذا التمييزُ ثمنُ عضّة:</b> أوّلُ صياغةٍ لهذه البوّابة سقطت على
    /// <c>const string PasswordEnv = "BRQOSH_PG_PASSWORD";</c> — <b>وهو اسمُ متغيّرٍ لا
    /// سرّ</b>. <b>والجوابُ تضييقُ الكاشف لا توسيعُ قائمةِ سماح</b>: قائمةُ السماح تُسكت
    /// الحالةَ وتُبقي العمى، والمُميِّزُ يُصلح الحكم.</para>
    ///
    /// <para><b>وثلاثةُ حدودٍ مُسمّاة:</b> القيمةُ بلا فراغ (فالرسالةُ فيها فراغ) · وستّةُ
    /// محارفَ فأكثر (فـ<c>""</c> و<c>"x"</c> ليستا سرّاً) · <b>وليست على هيئة معرّفٍ
    /// بحروفٍ كبيرة</b> (فذاك اسمُ متغيّرِ بيئة).</para>
    /// </summary>
    private static bool LooksLikeASeededPassword(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
            return false;

        var match = System.Text.RegularExpressions.Regex.Match(
            trimmed, @"(?:const\s+string|var|string)\s+\w*(?i:pass(?:word)?)\w*\s*=\s*""([^""]*)""");
        if (!match.Success)
            return false;

        var value = match.Groups[1].Value;
        return value.Length >= 6
            && !value.Any(char.IsWhiteSpace)
            && !System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Z0-9_]+$");
    }

    /// <summary>
    /// <b>واختبارٌ ذاتيٌّ يُسقط البوّابةَ إن عمي مُميِّزُها.</b> فبوّابةٌ لا ترى شيئاً تقول
    /// PASS، <b>وهو الدرسُ المسجّل في هذا المستودع باسمه</b> (أنماطُ الأسرار كانت ميّتةً
    /// وتقول PASS، م-٣٤).
    /// </summary>
    [Theory]
    // ‏يجب أن تُلتقط:
    [InlineData("const string probePass = \"ProbeAdmin9!\";", true)]
    [InlineData("var password = \"S3cret-Pa55x\";", true)]
    [InlineData("string adminPassword = \"hunter2hunter\";", true)]
    // ‏ويجب ألّا تُلتقط:
    [InlineData("const string PasswordEnv = \"BRQOSH_PG_PASSWORD\";", false)]
    [InlineData("const string PasswordVariable = \"BRQOSH_TEST_PASSWORD\";", false)]
    [InlineData("var passwordError = \"كلمة المرور مرفوضة\";", false)]
    [InlineData("// const string probePass = \"ProbeAdmin9!\";", false)]
    [InlineData("var password = Environment.GetEnvironmentVariable(\"BRQOSH_PROBE_PASSWORD\");", false)]
    public void TheDetectorTellsASecretFromAnEnvironmentVariableName(string line, bool expected)
    {
        Assert.Equal(expected, LooksLikeASeededPassword(line));
    }

    /// <summary>
    /// <b>ولا كلمةَ مرورٍ حرفيّةً تزرع حساباً.</b> حسابٌ يُزرع بكلمةٍ منشورةٍ في المستودع
    /// <b>مفتوحٌ لكلّ قارئ، ويبقى في القاعدة بعد انتهاء التشغيلة</b> — <b>والصمتُ هو
    /// الخطر</b>: لا أحدَ يُقفَل خارج حسابه فلا أحدَ يسأل.
    /// </summary>
    [Fact]
    public void NoToolSeedsAnAccountWithALiteralPassword()
    {
        var offenders = (
            from tool in Tools()
            from file in tool.Sources
            from line in File.ReadAllLines(file)
            where LooksLikeASeededPassword(line)
            select $"{tool.Name}/{Path.GetFileName(file)}: {line.Trim()}").ToList();

        Assert.True(offenders.Count == 0,
            "‏كلماتُ مرورٍ حرفيّةٌ في أدوات التشغيل:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }
}
