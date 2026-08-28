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

    /// <summary>للاختبارات فقط — يتجاوز متغير البيئة دون تلويث العملية إن أُزيل في finally.</summary>
    public static void ForceForTests(AppEnvironment? environment)
    {
        _override = environment;
        _cached = null;
    }

    public static void ResetForTests()
    {
        _override = null;
        _cached = null;
    }

    /// <summary>
    /// (CEEF-228) يفشل إلى الأمان: غياب <c>BRQOSH_ENV</c> يعني <b>إنتاجاً</b> لا تطويراً.
    ///
    /// كان الافتراض <c>Development</c>، والمثبِّت (<c>installer/brqosh.wxs</c>) لا يضبط المتغير،
    /// واختصارا قائمة ابدأ وسطح المكتب يشيران إلى <c>Brqosh.Desktop.exe</c> مباشرةً — فكل
    /// تركيب حقيقيّ كان يعمل في وضع التطوير. وقيمةٌ افتراضية واحدة كانت تُطفئ حرّاساً
    /// مكتوبةً وصحيحة: كشفُ رمز OTP على الشاشة (<c>InAppOtpNotificationChannel</c>)، وتجاوزُ
    /// RBAC بـ<c>BRQOSH_TEST</c> (<c>SecurityContext</c>)، وقبولُ SQLite بدل PostgreSQL.
    ///
    /// التطوير والاختبار يجب أن يُعلنا عن نفسيهما صراحةً: إمّا <c>BRQOSH_ENV</c>، أو
    /// <c>BRQOSH_TEST=1</c> الذي يضبطه harness الاختبارات (انظر <c>test.runsettings</c>
    /// و<c>TestEnvironmentInitializer</c> في مشاريع الاختبار).
    /// </summary>
    private static AppEnvironment Resolve() => Decide(
        ResolveAuthoritative(),
        Environment.GetEnvironmentVariable("BRQOSH_ENV"),
        Environment.GetEnvironmentVariable("BRQOSH_TEST"));

    /// <summary>
    /// (CEEF-229 · م0-١٢) قرار البيئة — دالة نقيّة، كل مدخلاتها ظاهرة.
    ///
    /// <para>أُخرج القرار من قراءة النظام عمداً: سياسة «لا خفض» يجب أن تُختبَر بكل تركيباتها،
    /// واختبارٌ يكتب في سجلّ النظام أو مجلد التثبيت يحتاج صلاحية مسؤول ويُلوّث جهاز المطوّر —
    /// فيُكتب حارسٌ ضعيف أو لا يُكتب حارس. الفصل يجعل السياسة مقيسة والقراءة رقيقة.</para>
    ///
    /// <para><b>القاعدة:</b> متى أعلن المصدر الرسميّ إنتاجاً فلا شيء يخفضه. وفيما عدا ذلك
    /// يُحترم إعلان العملية، والافتراض إنتاج.</para>
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
    /// (CEEF-229 · م0-١٢) البيئة من مصدرٍ <b>لا يملكه المستخدم</b>.
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
                    return Parse(machine);
            }
            catch
            {
                // قراءة نطاق الجهاز قد تُمنع بسياسة — يُسقط هذا المصدر ويُجرَّب الملفّ.
            }
        }

        try
        {
            var marker = Path.Combine(AppContext.BaseDirectory, "brqosh.environment");
            if (File.Exists(marker))
            {
                var content = File.ReadAllText(marker).Trim();
                if (!string.IsNullOrWhiteSpace(content))
                    return Parse(content);
            }
        }
        catch
        {
            // مجلد تثبيت غير مقروء — لا مصدر رسميّ، ويبقى الافتراض الآمن أدناه.
        }

        return null;
    }

    private static AppEnvironment Parse(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "production" or "prod" => AppEnvironment.Production,
        "test" => AppEnvironment.Test,
        _ => AppEnvironment.Development,
    };
}
