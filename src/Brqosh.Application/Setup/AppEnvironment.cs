namespace Brqosh.Application.Setup;

/// <summary>وضع تشغيل التطبيق — يتحكم في سلوك قاعدة البيانات والترخيص.</summary>
public enum AppEnvironment
{
    Development = 0,
    Test = 1,
    Production = 2,
}

public static class AppEnvironmentConfig
{
    private static AppEnvironment? _cached;
    private static AppEnvironment? _override;
    private static AppEnvironment? _testDefault;

    public static AppEnvironment Current
    {
        get
        {
            if (_override is AppEnvironment forced)
                return forced;
            _cached ??= Resolve();
            return _cached.Value;
        }
    }

    public static bool IsProduction => Current == AppEnvironment.Production;

    /// <summary>
    /// ‏(ح-٧٥) <b>هل هذه ثنائيّةٌ مشحونة؟</b> — <c>BRQOSH_SHIPPED</c> يُعرَّف حين يبني <c>installer/publish.ps1</c>
    /// ‏بـ<c>-p:BrqoshShipped=true</c> وحدَه (<c>Directory.Build.props</c>). وفيها لا تُقرأ البيئةُ أصلاً.
    /// </summary>
    public static bool IsShippedBuild =>
#if BRQOSH_SHIPPED
        true;
#else
        false;
#endif

    /// <summary>للاختبارات فقط — يتجاوز متغير البيئة دون تلويث العملية إن أُزيل في finally.</summary>
    public static void ForceForTests(AppEnvironment? environment)
    {
        _override = environment;
        _cached = null;
    }

    /// <summary>
    /// <b>الافتراضُ الذي تعود إليه الاختباراتُ عند «إعادة الضبط»</b> (٢٠٢٦-١٠-٢٧).
    ///
    /// <para><b>ما كان:</b> <see cref="ResetForTests"/> يمسح التجاوزَ فيعود القرارُ إلى
    /// <see cref="Resolve"/> — وهو يقرأ <b>المصدرَ الرسميَّ على مستوى الجهاز</b>. فكلُّ اختبارٍ
    /// يُعيد الضبطَ في مُهيّئه (‏`ProductionSchemaGuardTests` مثلاً) كان <b>يُلغي وضعَ
    /// الاختبار للتجميعة كلِّها</b> طالما أنّ المنتجَ منصَّبٌ على الجهاز: قِيس 1184 إخفاقاً في
    /// مجموعة Application على جهازٍ يحمل `BRQOSH_ENV=production` في نطاق الجهاز.</para>
    ///
    /// <para><b>والقاعدة الآن:</b> «إعادة الضبط» تعني العودةَ إلى ما أعلنته التجميعةُ
    /// (<see cref="AppEnvironment.Test"/>)، لا إلى قراءةِ الجهاز. ومن أراد قراءةَ الجهاز حقّاً
    /// فبابُه الصريح <see cref="ForceRealForTests"/> — فتُقاس السياسةُ الحقيقيّة بطلبٍ لا
    /// بأثرٍ جانبيّ.</para>
    /// </summary>
    public static void SetTestDefault(AppEnvironment environment)
    {
        _testDefault = environment;
        _cached = null;
    }

    /// <summary>يعود إلى افتراض التجميعة المعلَن (<see cref="SetTestDefault"/>) لا إلى قراءة الجهاز.</summary>
    public static void ResetForTests()
    {
        _override = _testDefault;
        _cached = null;
    }

    /// <summary>
    /// **قراءةُ الجهاز الحقيقيّة** — للأدلّة التي تقيس السياسةَ ضدّ مصادرِ النظام فعلاً.
    /// وتُعاد بعدها الحالةُ بـ<see cref="ResetForTests"/> في `finally`.
    /// </summary>
    public static void ForceRealForTests()
    {
        _override = null;
        _cached = null;
    }

    /// <summary>
    /// يفشل إلى الأمان: غياب <c>BRQOSH_ENV</c> يعني <b>إنتاجاً</b> لا تطويراً.
    ///
    /// كان الافتراض <c>Development</c>، والمثبِّت (<c>installer/brqosh.wxs</c>) لا يضبط المتغير،
    /// واختصارا قائمة ابدأ وسطح المكتب يشيران إلى <c>Brqosh.Desktop.exe</c> مباشرةً — فكل
    /// تركيب حقيقيّ كان يعمل في وضع التطوير. وقيمةٌ افتراضية واحدة كانت تُطفئ حرّاساً
    /// مكتوبةً وصحيحة: كشفُ رمز OTP على الشاشة (<c>InAppOtpNotificationChannel</c>)،
    /// و<b>دورُ الواجهة البرمجيّة الافتراضيّ يصير مديراً بدل مُطّلع</b>
    /// (<c>ApiSecuritySettings.ResolveApiRole</c>: <c>isProduction ? Viewer : Administrator</c>)،
    /// وقبولُ SQLite بدل PostgreSQL.
    ///
    /// <para>‏(ن-٤٦) <b>وصُحّح هنا ادّعاءٌ بائت:</b> كان السطرُ يقول «تجاوزُ RBAC بـ<c>BRQOSH_TEST</c>
    /// (<c>SecurityContext</c>)». وقِيس أنّ <c>AutomatedTestBypass</c> <b>لا تصريحَ له في
    /// <c>src</c> إطلاقاً</b> — حُذف في CEEF-229 · م2-١ — وشاهدُ الضبط أنّ النمطَ نفسَه يجد
    /// <c>SecurityContext.CurrentRole</c>. فالانحدارُ قائمٌ لكن من البابِ المذكورِ أعلاه.</para>
    ///
    /// التطوير والاختبار يجب أن يُعلنا عن نفسيهما صراحةً: إمّا <c>BRQOSH_ENV</c>، أو
    /// <c>BRQOSH_TEST=1</c> الذي يضبطه harness الاختبارات (انظر <c>test.runsettings</c>
    /// و<c>TestEnvironmentInitializer</c> في مشاريع الاختبار).
    /// </summary>
    private static AppEnvironment Resolve() => DecideForBuild(
        IsShippedBuild,
        ResolveAuthoritative(),
        Environment.GetEnvironmentVariable("BRQOSH_ENV"),
        Environment.GetEnvironmentVariable("BRQOSH_TEST"));

    /// <summary>
    /// ‏(ح-٧٥ · security-licensing-tenancy#0 · installer-deployment-ops#7) <b>في الثنائيّة المشحونة: إنتاجٌ، بلا شرط.</b>
    ///
    /// <para><b>ما كان:</b> المصدرُ الرسميُّ (متغيّرُ الجهاز · ملفُّ العلامة) يحتاج مسؤولَ Windows ليُكتب — <b>ومسؤولُ
    /// Windows المحلّيُّ هو صاحبُ الجهاز في المنشأة الصغيرة</b>. فكتابتُه «development» تجعل <see cref="Decide"/>
    /// يُرجع تطويراً، فتعود الفترةُ التجريبيّة ويُفتح <c>BRQOSH_UITEST</c>: <b>دخولٌ آليٌّ بحساب المدير بلا كلمة
    /// مرور، وترخيصٌ مُتجاوَز</b>. و«معطّلٌ قسرياً في الإنتاج» كان صحيحاً — والإنتاجُ نفسُه قابلٌ للخفض.</para>
    ///
    /// <para><b>والقرار:</b> الثنائيّةُ التي يبنيها <c>installer/publish.ps1</c> لا تسأل. وغيرُها يسأل كما كان —
    /// فالاختباراتُ والبوّاباتُ والتشغيلاتُ الحيّةُ بلا تغيير. <b>ودالةٌ نقيّةٌ</b> ليُقاس الفرعُ المشحونُ بكلّ
    /// تركيبات المدخلات، وهو لا يُبنى في أيّ تشغيلة اختبار.</para>
    /// </summary>
    internal static AppEnvironment DecideForBuild(bool shipped, AppEnvironment? authoritative, string? processEnv, string? testFlag) =>
        shipped ? AppEnvironment.Production : Decide(authoritative, processEnv, testFlag);

    /// <summary>
    /// قرار البيئة — دالة نقيّة، كل مدخلاتها ظاهرة.
    ///
    /// <para>أُخرج القرار من قراءة النظام عمداً: سياسة «لا خفض» يجب أن تُختبَر بكل تركيباتها،
    /// واختبارٌ يكتب في سجلّ النظام أو مجلد التثبيت يحتاج صلاحية مسؤول ويُلوّث جهاز المطوّر —
    /// فيُكتب حارسٌ ضعيف أو لا يُكتب حارس. الفصل يجعل السياسة مقيسة والقراءة رقيقة.</para>
    ///
    /// <para><b>القاعدة:</b> متى أعلن المصدر الرسميّ إنتاجاً فلا شيء يخفضه. وفيما عدا ذلك
    /// يُحترم إعلان العملية، والافتراض إنتاج.</para>
    ///
    /// <para><b>(ن-٤٦) وبندٌ ثالثٌ يُعلَن هنا ولا يُستنتَج من موضع تنفيذه:</b> مصدرٌ رسميٌّ
    /// <b>لا يُفهَم نصُّه</b> يُقرأ إنتاجاً — <b>لا لأنّه أعلنه، بل لأنّ الأشدَّ هو القراءةُ
    /// الوحيدةُ المسموحة</b> عند الشكّ. <b>وهو يتقدّم على إعلان العمليّة</b>، لأنّ متغيّرَ
    /// نطاقِ الجهاز يُورَّث حرفيّاً إلى كتلة بيئة العمليّة: فالنصُّ المعطوبُ نفسُه يصل إلى
    /// الفرع المتساهل، ولو اكتُفي بإسقاط المصدر الرسميّ لعاد الخفضُ من ذلك الباب.
    /// انظر <see cref="ParseAuthoritative"/>.</para>
    /// </summary>
    internal static AppEnvironment Decide(AppEnvironment? authoritative, string? processEnv, string? testFlag)
    {
        if (authoritative is AppEnvironment.Production)
            return AppEnvironment.Production;

        if (!string.IsNullOrWhiteSpace(processEnv))
            return Parse(processEnv);

        if (string.Equals(testFlag, "1", StringComparison.Ordinal))
            return AppEnvironment.Test;

        return authoritative ?? AppEnvironment.Production;
    }

    /// <summary>
    /// البيئة من مصدرٍ <b>لا يملكه المستخدم</b>.
    ///
    /// <para>إصلاح الموجة الأولى جعل الافتراض إنتاجاً، وبقي شقٌّ: المستخدم يستطيع أن
    /// <b>يخفض</b> تركيباً إنتاجياً بـ<c>setx BRQOSH_ENV development</c> — ولا يحتاج صلاحية
    /// مسؤول Windows لأن <c>setx</c> يكتب في نطاق المستخدم، ونطاقُ المستخدم <b>يتغلّب</b>
    /// على نطاق الجهاز الذي يكتبه المثبِّت. وبخفضٍ واحد يعود كشفُ رمز OTP على الشاشة،
    /// ويُقبل SQLite بدل PostgreSQL، ويصير تجاوز الصلاحيات ممكناً.</para>
    ///
    /// <para>المصدران الرسميّان — وكلاهما يحتاج صلاحية مسؤول للكتابة: متغيّر البيئة بنطاق
    /// <b>الجهاز</b> (يكتبه المثبِّت في <c>installer/brqosh.wxs</c>)، وملفٌّ بجوار الملف
    /// التنفيذيّ في مجلد التثبيت. ومتى قال أيٌّ منهما «إنتاج» فلا يخفضه شيء.</para>
    ///
    /// <para><b>الاتجاه المسموح واحد:</b> رفعُ التشدّد لا خفضه. فمتغيّر المستخدم يبقى نافعاً
    /// على جهاز مطوّر بلا مصدر رسميّ، ولا يُضعف تركيباً حقيقياً.</para>
    /// </summary>
    private static AppEnvironment? ResolveAuthoritative()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var machine = Environment.GetEnvironmentVariable("BRQOSH_ENV", EnvironmentVariableTarget.Machine);
                if (!string.IsNullOrWhiteSpace(machine))
                    return ParseAuthoritative(machine);
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
            {
                // ‏(ن-٤٦) **مضيَّقٌ عمداً**: كان `catch` عارياً، فكان يبتلع أيَّ رميةٍ من
                // ‏القراءة *أو من التحليل* ويُسقط المصدرَ الرسميَّ صامتاً. والقائمةُ الآن
                // ‏هي ما يوثّقه السطرُ نفسُه: منعُ سياسةٍ لا عطلُ منطق.
            }
        }

        try
        {
            var marker = Path.Combine(AppContext.BaseDirectory, "brqosh.environment");
            if (File.Exists(marker))
            {
                var content = File.ReadAllText(marker).Trim();
                if (!string.IsNullOrWhiteSpace(content))
                    return ParseAuthoritative(content);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // ‏(ن-٤٦) **مضيَّقٌ عمداً** — انظر الالتقاطَ أعلاه: مجلّدُ تثبيتٍ غيرُ مقروء،
            // ‏لا عطلُ منطقٍ في التحليل.
        }

        return null;
    }

    /// <summary>
    /// ‏قراءةُ إعلانِ <b>العمليّة</b> — يكتبه المستخدم، ولا يُخفض به تركيبٌ رسميّ.
    ///
    /// <para>‏والمجهولُ هنا يبقى تطويراً بقرارٍ قائم: «الخطأُ الإملائيُّ لا يمنح امتيازاً،
    /// ومن يكتب قيمةً بيده يقصد الخروجَ من الإنتاج» (<c>UnknownValue_IsNotReadAsProduction</c>).
    /// <b>وذلك صحيحٌ هنا وحدَه</b> — حيث لا مصدرَ رسميّ، فالجهازُ ليس تركيباً مُدارَاً.</para>
    /// </summary>
    private static AppEnvironment Parse(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "production" or "prod" => AppEnvironment.Production,
        "test" => AppEnvironment.Test,
        _ => AppEnvironment.Development,
    };

    /// <summary>
    /// ‏(م-٥ · ن-٤٦) <b>قراءةُ المصدر الرسميّ — ونصٌّ لا يُفهَم يُقرأ إنتاجاً.</b>
    ///
    /// <para><b>ما كان:</b> المصدرُ الرسميُّ كان يمرّ بـ<see cref="Parse"/> نفسِها، ونهايتُها
    /// <c>_ => Development</c>. <b>فكلُّ رمزٍ لا يعرفه الجدولُ إعلانٌ فاعلٌ بالتطوير</b> —
    /// في ملفٍّ سياستُه المكتوبة أنّ «الاتجاه المسموح واحد: <b>رفعُ</b> التشدّد لا خفضه».
    /// وكان الجدولُ **بلا فرعٍ للتطوير أصلاً**: <c>development</c> و<c>dev</c> و<c>staging</c>
    /// و<c>Productoin</c> تخرج كلُّها من المخرج نفسِه — <b>فلا يُفرَّق إعلانٌ مقصودٌ من غلطة</b>.</para>
    ///
    /// <para><b>والطريقُ الحيُّ مقيسٌ ومكتوب:</b> <c>docs/postgres-production.md:13</c>
    /// و<c>installer/README.md:20</c> يأمران المُشغّلَ أن يكتب بيده
    /// <c>SetEnvironmentVariable("BRQOSH_ENV", "production", "Machine")</c> — <b>وحرفٌ مقلوبٌ
    /// هناك كان يخفض التركيبَ كلَّه بلا صوت</b>.</para>
    ///
    /// <para><b>ولمَ «إنتاج» لا «كأنّه غائب»:</b> متغيّرُ نطاقِ الجهاز <b>يُورَّث حرفيّاً إلى
    /// كتلة بيئة العمليّة</b>، فالنصُّ المعطوبُ نفسُه يصل إلى فرع العمليّة المتساهل. فلو
    /// أُعيد <c>null</c> هنا لصار <c>Decide(null, "Productoin", null)</c> ⟵ <b>تطويراً</b> —
    /// أي العطلُ نفسُه من بابٍ آخر. <b>فالفشلُ يكون مغلقاً لا محايداً.</b></para>
    ///
    /// <para><b>وهذا بندٌ ثالثٌ في السياسة يُعلَن ولا يُستنتَج:</b> مصدرٌ رسميٌّ لا يُفهَم نصُّه
    /// يُقرأ إنتاجاً — <b>لا لأنّه أعلنه، بل لأنّ الأشدَّ هو القراءةُ الوحيدةُ المسموحة</b>،
    /// وهو يتقدّم على إعلان العمليّة.</para>
    /// </summary>
    internal static AppEnvironment ParseAuthoritative(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "production" or "prod" => AppEnvironment.Production,
        "test" => AppEnvironment.Test,
        "development" or "dev" => AppEnvironment.Development,
        _ => AppEnvironment.Production,
    };
}
