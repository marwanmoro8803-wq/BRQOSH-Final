using System.IO;
using System.Text;

namespace Brqosh.Desktop.Tests;

/// <summary>
/// (م-٤ · ن-١٥) <b>ذيلُ الإقلاع كان بلا نهايةٍ حاسمة — فعطلٌ فيه يترك عمليّةً خالدةً بلا نافذة.</b>
///
/// <para><b>والسلسلةُ كلُّها مقيسة:</b> <c>OnStartup</c> هو <c>async void</c>، فكلُّ ما بعد
/// أوّلِ <c>await</c> يجري <b>متابعةً على المُرسِل</b>. ورميةٌ هناك لا تصعد إلى <c>Main</c>
/// بل تُسلَّم إلى <c>DispatcherUnhandledException</c>، و<c>args.Handled = true</c> يطوي بقيّةَ
/// الذيل. ومع <c>ShutdownMode="OnExplicitShutdown"</c> في <c>App.xaml:4</c> <b>لا شيءَ يُنهي
/// العملية</b>: فرميةٌ في <c>new MainWindow(setup!)</c> تترك عمليّةً حيّةً <b>لا نافذةَ لها
/// ولا سبيلَ إلى إغلاقها</b>، ومؤقّتاها مُسلَّحان قبل السطر الذي يرمي
/// (<c>MainWindow.xaml.cs:61</c> و<c>:72</c>) وتنظيفُهما معلَّقٌ على <c>Closed</c> — <b>حدثٌ
/// لا ترفعه نافذةٌ لم تُعرض</b>. فتجري نسخةٌ احتياطيّةٌ كلَّ نصفِ ساعةٍ في عمليّةٍ لا يراها أحد.</para>
///
/// <para><b>وتصحيحُ مبرّرِ البند:</b> قيل إنّ العلّةَ «استثناءُ حالةٍ فاسدة». <b>وذلك لا يبلغ
/// هذا المعالجَ أصلاً على .NET 9</b>: <c>StackOverflowException</c> تُنهي العمليّةَ فوراً بلا
/// تسليم، و<c>AccessViolationException</c> لا تُسلَّم للمعالجات المُدارة. <b>وقيس أنّه لا
/// عودةَ إليها</b>: بحثٌ عن <c>legacyCorruptedStateExceptionsPolicy</c> في <c>src</c>
/// و<c>Directory.Build.props</c> يُعيد صفراً — <b>وشاهدُ ضبطٍ موجب</b> أنّ الماسحَ نفسَه يجد
/// <c>DispatcherUnhandledException</c> في الشجرة نفسِها. فالعلّةُ الحقيقيّةُ بنيويّةٌ لا صنفيّة.</para>
///
/// <para><b>ولماذا الحارسُ هنا لا داخل المعالج — وهو لبُّ الإصلاح:</b> عَلَمٌ يُستشار داخل
/// <c>DispatcherUnhandledException</c> <b>لا يُفرّق</b> بين «ذيلُ الإقلاع نفسُه انهار»
/// و«معالجُ زرٍّ <c>async void</c> في نافذةٍ مشروطةٍ انهار» — والثاني <b>يجب أن يستمرّ</b>:
/// <c>SetupWizardWindow:64</c> و<c>DatabaseConnectionWindow:37</c> و<c>LicenseActivationWindow:22</c>
/// — <b>وثلاثتُها بلا <c>catch</c> إطلاقاً</b> (مقيس: صفرٌ في كلِّ ملفّ) وتجري قبل ظهور
/// الصدفة، <b>فإنهاؤها يُبدّل «أعِد المحاولة» بـ«خرج التطبيق وضاع ما أُدخل»</b>.</para>
///
/// <para><b>ولا تنفكّ رمياتُها إلى هنا — بمسارَين لا واحد:</b> الأُولَيان
/// <c>async void</c>، فـ<c>AsyncVoidMethodBuilder.SetException</c> يبعثهما <b>عمليّةَ
/// إرسالٍ منفصلة</b>. <b>والثالثةُ <c>void</c> متزامنة</b> — قيلت <c>async void</c> في
/// سلسلة المراجعة وليست كذلك — ورميتُها يلتقطها <c>Dispatcher.WrappedInvoke</c> في حلقة
/// الحوار المتداخلة. <b>فالنتيجةُ واحدة والآليّةُ اثنتان</b>، والتمييزُ بنيويٌّ لا شرطيّ.</para>
///
/// <para><b>ولماذا ماسحُ بنيةٍ لا قياسٌ سلوكيّ:</b> ما يُقاس هنا هو «هل العبارةُ داخل حارس»
/// — <b>وهي خاصّيّةٌ نحويّةٌ لا سلوكيّة</b>. وقيادةُ <c>OnStartup</c> حيّاً تستلزم قاعدةً
/// ومُرشداً ونوافذَ مشروطة، <b>وما كانت لتقيس إلّا هذه الخاصّيّةَ نفسَها</b>. ولذلك يُحلَّل
/// العمقُ بمُحلِّلٍ حقيقيّ — <b>لا بمطابقةِ نصّ</b> — <b>ويُبرهَن أنّ المُحلِّلَ يُميّز</b>
/// بشاهدَي ضبطٍ: موجبٍ يجده حارساً قائماً، وسالبٍ يجده عارياً.</para>
/// </summary>
public class StartupFailureHasAnExitPathTests
{
    private static readonly string Source = ReadDesktopFile("App.xaml.cs");

    // ── القياسُ المركزيّ ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>عبارتا الذيل الحاسمتان داخل حارسٍ ينتهي بـ<c>Shutdown</c>.</b>
    ///
    /// <para><c>_setupStore.Load()</c> <b>تقع قبل أوّلِ <c>await</c></b> — وهي في القياس
    /// عمداً: مُولِّدُ حالة <c>async void</c> يلتقط الرمياتِ المتزامنةَ كذلك ويبعثها إلى
    /// المعالج نفسِه، <b>فحارسٌ يفتح بعد الحلقة وحدَها يترك مئةَ سطرٍ مكشوفة</b> — وفيها
    /// فكُّ JSON من القرص، وهو يرمي على ملفٍّ محجوزٍ أو تالف.</para>
    ///
    /// <para>و<c>new MainWindow(setup!)</c> هي الأخطر: مؤقّتا الخمولِ والنسخِ يُسلَّحان في
    /// بانيها <b>قبل</b> <c>Navigate("dashboard", …)</c>، فرميةٌ هناك تترك مؤقّتَين حيَّين في
    /// عمليّةٍ بلا نافذة. و<c>main.Show()</c> هي العتبةُ التي بعدها يصير الطيُّ سلوكاً صحيحاً.</para>
    /// </summary>
    [Theory]
    [InlineData("var setup = _setupStore.Load();")]
    [InlineData("var main = new MainWindow(setup!);")]
    [InlineData("main.Show();")]
    public void TheStartupTail_IsInsideAGuardThatShutsDown(string statement)
    {
        var guard = InnermostGuardOf(statement);

        Assert.True(guard is not null,
            $"‏«{statement}» خارجَ كلِّ حارسٍ في OnStartup — فرميةٌ فيها تُطوى في المعالج، " +
            "‏ومع ShutdownMode=OnExplicitShutdown تبقى عمليّةٌ حيّةٌ بلا نافذة.");

        Assert.True(guard!.Contains("Shutdown(", StringComparison.Ordinal),
            "‏الحارسُ يلتقط ولا يُنهي — والتقاطٌ بلا إنهاءٍ هو العلّةُ نفسُها بمكانٍ آخر.");
    }

    // ── شاهدا الضبط: أنّ المُحلِّلَ يُميّز ──────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ موجب:</b> المُحلِّلُ يجد حارساً <b>كان قائماً قبل هذا الإصلاح</b>.
    ///
    /// <para>فلولاه لَما فُرّق بين «مُحلِّلٍ يقرأ البنية» و«مُحلِّلٍ يقول نعم دائماً».
    /// و<c>DesktopAccountingFacade</c> داخل <c>try</c> ينتهي <c>catch</c>ُه بـ<c>Shutdown()</c>
    /// منذ CEEF-200 — <b>فهو محكٌّ لم يكتبه هذا الإصلاح</b>.</para>
    /// </summary>
    [Fact]
    public void TheParser_FindsAGuardThatPredatesThisFix()
    {
        var guard = InnermostGuardOf("using var facade = new DesktopAccountingFacade();");

        Assert.True(guard is not null, "‏المُحلِّلُ عمي عن حارسٍ قائم — فنتيجتُه أعلاه بلا قيمة.");
        Assert.Contains("Shutdown(", guard!, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>وشاهدُ ضبطٍ سالب — وهو الألزم:</b> المُحلِّلُ يقول «عارية» حين تكون عارية.
    ///
    /// <para><c>base.OnStartup(e);</c> أوّلُ سطرٍ في الدالّة، <b>قبل كلِّ <c>try</c></b>.
    /// ومُحلِّلٌ يُعيد «محروسة» لكلّ شيءٍ كان ليُخضِرَّ القياسَ المركزيَّ على الشجرة المعطوبة
    /// — <b>وهو بالضبط ما يجعل ماسحَ بنيةٍ بلا شاهدٍ سالبٍ زينةً لا قياساً</b>.</para>
    ///
    /// <para><b>ويُقصد بقاؤها عارية:</b> تسجيلُ المعالجات لا يُغلَّف، وإلّا لَما وُجد معالجٌ
    /// يلتقط ما بعده.</para>
    /// </summary>
    [Fact]
    public void TheParser_ReportsAnUnguardedStatementAsUnguarded() =>
        Assert.Null(InnermostGuardOf("base.OnStartup(e);"));

    // ── ما لا يجوز أن يُكسَر بهذا الإصلاح ─────────────────────────────────────

    /// <summary>
    /// <b>والمعالجُ يظلّ يطوي بالافتراض — شرطاً لا تفصيلاً.</b>
    ///
    /// <para><b>والحجّةُ النوافذُ المشروطة:</b> ثلاثٌ منها تجري قبل ظهور الصدفة —
    /// <c>SetupWizardWindow:64</c> و<c>DatabaseConnectionWindow:37</c> و
    /// <c>LicenseActivationWindow:22</c> — ومعالجاتُها <c>async void</c> <b>بلا
    /// <c>catch</c> إطلاقاً</b>. فالطيُّ هو ما يُبقي «أعِد المحاولة» ممكناً، <b>وإنهاءٌ
    /// هناك يُبدّله بـ«خرج التطبيق وضاع ما أُدخل»</b> في مُرشد التهيئة الأوّل بالذات.</para>
    ///
    /// <para><b>وتصحيحُ استشهادٍ تداولته سلسلةُ المراجعة كلُّها:</b> قيل إنّ
    /// <c>Ui/XamlUiLocalizer.cs:101</c> «يرمي عمداً معتمِداً على الطيّ». <b>وهو مقلوب</b>:
    /// السطرُ <b>ذيلُ تعليقٍ عنوانُه «<c>AddOrUpdate</c> لا <c>Add</c>»</b> — أي تعليلُ
    /// <b>تجنّبِ</b> الرمية لا الاعتمادِ عليها. والملفُّ فيه <b>صفرُ <c>throw</c></b>
    /// (شاهدُ ضبطٍ موجب: <c>Ui/EmptyStateHelper.cs:15</c> و<c>:40</c> يحملان رميتين في
    /// المجلّد نفسِه)، و<c>XamlUiLocalizerRefreshTests:52</c> يُوكّد
    /// <c>Assert.Null(thrown)</c> — <b>أي يحرس ألّا تقع</b>. <b>وقُرئ آخرُ سطرٍ في
    /// التعليق معزولاً عن عنوانه</b>، وهو عينُ ما يُحذّر منه عرفُ هذا التدقيق:
    /// لا يُصدَّق تعليقٌ ولا اسمُ دالّة.</para>
    ///
    /// <para><b>وللحارس اختبارٌ ذاتيٌّ يُسقطه إن عمي</b> — وهو لازمٌ هنا بالذات: صياغةٌ
    /// تعدّ الكتلَ وحدَها <b>يمرّ عليها شرطٌ بلا قوسين</b>، <b>وتلك صيغةٌ مستعملةٌ في
    /// <c>App.xaml.cs</c> نفسِه</b> (معالجُ النطاق) ولا <c>.editorconfig</c> يمنعها. فتُطبَّق
    /// ثلاثُ طفراتٍ على نسخةٍ في الذاكرة — <b>لا على الملفّ</b> — ويجب أن تُرفض ثلاثتُها.</para>
    /// </summary>
    [Fact]
    public void TheDispatcherHandler_StillSwallowsUnconditionally()
    {
        Assert.True(SwallowIsUnconditional(Source),
            "‏طيُّ المعالج صار مشروطاً — فالنوافذُ المشروطةُ في الإقلاع تفقد «أعِد المحاولة».");

        Assert.Equal(1, Source.Split(Swallow).Length - 1);

        // ‏(أ) شرطٌ بلا قوسين — لا يُنشئ كتلةً، فحارسُ العمق وحدَه أعمى عنه.
        Assert.False(SwallowIsUnconditional(Mutate("if (_shellIsUp) " + Swallow)),
            "‏شرطٌ بلا قوسين مرّ — فالحارسُ يعدّ الكتلَ ولا يقرأ ما قبل العبارة.");

        // ‏(ب) مخرجٌ مبكّرٌ قبل الطيّ — والطيُّ نفسُه يبقى بلا شرطٍ ظاهر.
        Assert.False(SwallowIsUnconditional(Mutate("if (_startupFailed) { Shutdown(1); return; } " + Swallow)),
            "‏مخرجٌ مبكّرٌ مرّ — وهو الطيُّ المشروطُ بعينه بصيغةٍ أخرى.");

        // ‏(ج) إسنادُ قيمةٍ محسوبةٍ بدل `true`.
        Assert.False(SwallowIsUnconditional(Mutate("args.Handled = _shellIsUp;")),
            "‏إسنادٌ مشروطٌ مرّ على الحارس.");
    }

    private const string Swallow = "args.Handled = true;";

    private static string Mutate(string replacement) =>
        Source.Replace(Swallow, replacement, StringComparison.Ordinal);

    /// <summary>
    /// هل <c>args.Handled = true;</c> يقع بلا شرطٍ في جسم المعالج؟ — ثلاثةُ فحوصٍ
    /// لا واحد: <b>لا كتلةَ تحويه</b>، <b>ولا نصَّ قبله على سطره</b>، <b>ولا مخرجَ
    /// مبكّرٌ في جسم المعالج</b>.
    /// </summary>
    private static bool SwallowIsUnconditional(string source)
    {
        var cleaned = Blank(source);

        var anchor = cleaned.IndexOf("DispatcherUnhandledException +=", StringComparison.Ordinal);
        if (anchor < 0) return false;

        var lambda = BlockOf(anchor, cleaned);
        var target = cleaned.IndexOf(Swallow, lambda.Open, StringComparison.Ordinal);
        if (target < 0 || target > lambda.Close) return false;

        // ‏(١) لا كتلةَ متفرّعةٌ بين جسم المعالج والعبارة.
        if (BlocksEnclosing(cleaned, lambda.Open, target).Any(b => b.Open > lambda.Open))
            return false;

        // ‏(٢) ولا نصَّ بين آخرِ فاصلٍ والعبارة — وهو ما يُسقط الشرطَ بلا قوسين.
        var j = target - 1;
        while (j >= 0 && cleaned[j] is not (';' or '{' or '}')) j--;
        if (cleaned[(j + 1)..target].Trim().Length != 0) return false;

        // ‏(٣) ولا مخرجَ مبكّرٌ يسبقه في جسم المعالج.
        var before = cleaned[lambda.Open..target];
        return !before.Contains("return", StringComparison.Ordinal)
            && !before.Contains("Shutdown(", StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ولا <c>Environment.Exit</c> في سطح المكتب.</b>
    ///
    /// <para>فهو <b>يتخطّى كلَّ <c>finally</c> وكلَّ <c>using</c></b>: معاملاتٌ مفتوحةٌ لا
    /// تُرجَع، ومقابضُ ملفّاتٍ لا تُغلق، ونسخةٌ احتياطيّةٌ نصفُها على القرص. <b>وقد اقتُرح
    /// <c>Environment.Exit(70)</c> على نفاد الذاكرة</b>، ومسارُه المقيسُ هو فكُّ ترميزِ صورةٍ
    /// يختارها المستخدم (<c>CompanySettingsPage.xaml.cs:143-147</c>) — <b>فملفُّ شعارٍ تالفٌ
    /// كان ليقتل عمليّةً محاسبيّةً ويترك دفاترَها مفتوحة</b>. و<c>Shutdown</c> يُنهي بترتيب.</para>
    ///
    /// <para><b>وشاهدُ ضبطٍ موجب:</b> الماسحُ نفسُه يجد <c>Environment.</c> في الشجرة نفسِها،
    /// <b>فالصفرُ صفرٌ مقيسٌ لا ماسحٌ أعمى</b> — وذلك درسُ «أدواتُ القياس تكذب صامتةً».</para>
    /// </summary>
    [Fact]
    public void TheDesktop_NeverKillsTheProcessOutright()
    {
        var files = Directory.EnumerateFiles(DesktopRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal))
            .ToList();

        var offenders = files
            .Where(f => Blank(File.ReadAllText(f)).Contains("Environment.Exit", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f))
            .ToList();

        Assert.True(offenders.Count == 0,
            "‏قتلٌ فوريٌّ للعمليّة في: " + string.Join("، ", offenders) +
            " — ويتخطّى كلَّ finally وusing. والإنهاءُ المرتّب هو Shutdown.");

        // ‏شاهدُ ضبطٍ موجب: الماسحُ يقرأ ملفّاتٍ حقيقيّةً فيها ما نعلم أنّه فيها.
        Assert.Contains(files, f =>
            Blank(File.ReadAllText(f)).Contains("Environment.", StringComparison.Ordinal));
    }

    // ── مُحلِّلُ العمق ─────────────────────────────────────────────────────────

    /// <summary>
    /// يُعيد نصَّ <c>catch</c> أقربِ <c>try</c> يحوي العبارةَ داخل <c>OnStartup</c>،
    /// أو <c>null</c> إن كانت عارية.
    /// </summary>
    private static string? InnermostGuardOf(string statement)
    {
        var cleaned = Blank(Source);
        var body = OnStartupBody(cleaned);

        var target = cleaned.IndexOf(statement, body.Open, StringComparison.Ordinal);
        Assert.InRange(target, body.Open, body.Close);

        (int Open, int Close)? innermost = null;
        foreach (var block in BlocksEnclosing(cleaned, body.Open, target))
        {
            if (!IsTry(cleaned, block.Open)) continue;
            if (innermost is null || block.Open > innermost.Value.Open) innermost = block;
        }

        if (innermost is null) return null;

        // ‏نصُّ الـcatch: من غلقِ الـtry إلى غلقِ الكتلة التي تليه.
        var catchOpen = cleaned.IndexOf('{', innermost.Value.Close + 1);
        if (catchOpen < 0) return string.Empty;

        var catchBlock = BlockOf(catchOpen, cleaned);
        return cleaned[catchBlock.Open..catchBlock.Close];
    }

    /// <summary>كلُّ الكتل التي تحوي <paramref name="target"/> داخل جسمٍ يبدأ عند <paramref name="from"/>.</summary>
    private static List<(int Open, int Close)> BlocksEnclosing(string cleaned, int from, int target)
    {
        var open = new Stack<int>();
        var enclosing = new List<(int, int)>();

        for (var i = from; i < cleaned.Length; i++)
        {
            if (cleaned[i] == '{') open.Push(i);
            else if (cleaned[i] == '}')
            {
                var start = open.Pop();
                if (start < target && i > target) enclosing.Add((start, i));
                if (open.Count == 0) break;
            }
        }

        return enclosing;
    }

    /// <summary>عددُ الكتل المفتوحةِ والمغلقةِ بين عمقٍ وعبارة — صفرٌ يعني «على العمق نفسِه».</summary>
    private static int BlocksBetween(int from, int target)
    {
        var cleaned = Blank(Source);
        return BlocksEnclosing(cleaned, from, target).Count(b => b.Open > from);
    }

    /// <summary>هل الكتلةُ المفتوحةُ هنا جسمُ <c>try</c>؟ — يُقرأ ما بين آخرِ فاصلٍ والقوس.</summary>
    private static bool IsTry(string cleaned, int openBrace)
    {
        var j = openBrace - 1;
        while (j >= 0 && cleaned[j] is not (';' or '{' or '}')) j--;

        var head = cleaned[(j + 1)..openBrace].Trim();
        return head == "try";
    }

    private static (int Open, int Close) OnStartupBody(string cleaned)
    {
        var signature = cleaned.IndexOf("protected override async void OnStartup", StringComparison.Ordinal);
        Assert.True(signature >= 0, "‏لم يُعثر على OnStartup — تغيّر توقيعُها فبطل القياس.");
        return BlockOf(cleaned.IndexOf('{', signature), cleaned);
    }

    private static (int Open, int Close) BlockOf(int anchor, string? cleaned = null)
    {
        cleaned ??= Blank(Source);
        var open = cleaned.IndexOf('{', anchor);
        var depth = 0;

        for (var i = open; i < cleaned.Length; i++)
        {
            if (cleaned[i] == '{') depth++;
            else if (cleaned[i] == '}' && --depth == 0) return (open, i);
        }

        throw new InvalidOperationException("‏كتلةٌ غيرُ مُغلقة — المصدرُ لا يُحلَّل.");
    }

    /// <summary>
    /// يمسح التعليقاتِ والنصوصَ الحرفيّةَ مُبقياً الأطوالَ والأسطر.
    ///
    /// <para><b>وهذا شرطٌ لا احتياط:</b> <c>$"…{ex.Message}"</c> يحمل قوسين معقوفين،
    /// و<c>'{'</c> يحمل واحداً — <b>وعدُّهما يُزيح العمقَ فيُبلّغ القياسُ كذباً</b>.</para>
    /// </summary>
    private static string Blank(string source)
    {
        var b = new StringBuilder(source);

        void Wipe(int from, int to)
        {
            for (var k = from; k < to && k < source.Length; k++)
                if (source[k] != '\n' && source[k] != '\r') b[k] = ' ';
        }

        for (var i = 0; i < source.Length; i++)
        {
            var next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (source[i] == '/' && next == '/')
            {
                var end = source.IndexOf('\n', i);
                if (end < 0) end = source.Length;
                Wipe(i, end);
                i = end;
            }
            else if (source[i] == '/' && next == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                Wipe(i, end);
                i = end - 1;
            }
            else if (source[i] == '"' || source[i] == '\'')
            {
                var quote = source[i];
                var verbatim = quote == '"' && i > 0 && source[i - 1] == '@';
                var j = i + 1;

                while (j < source.Length)
                {
                    if (!verbatim && source[j] == '\\') { j += 2; continue; }
                    if (source[j] != quote) { j++; continue; }
                    if (verbatim && j + 1 < source.Length && source[j + 1] == quote) { j += 2; continue; }
                    break;
                }

                Wipe(i, Math.Min(j + 1, source.Length));
                i = j;
            }
        }

        return b.ToString();
    }

    private static string DesktopRoot() => Path.Combine(FindRepoRoot(), "src", "Brqosh.Desktop");

    private static string ReadDesktopFile(string name) =>
        File.ReadAllText(Path.Combine(DesktopRoot(), name));

    private static string FindRepoRoot()
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
}
