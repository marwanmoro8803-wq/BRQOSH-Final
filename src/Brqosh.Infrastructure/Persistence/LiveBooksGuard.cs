namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// (م-٥ · ن-٢٢) <b>إذنٌ صريحٌ باسمِ القاعدة قبل أن تكتب أداةٌ في دفاترَ حيّة.</b>
///
/// <para><b>ما كان قبله:</b> <c>tools/FullScenarioAudit</c> — <b>وهي الخطوةُ ٦ في كتيّب
/// التنصيب</b> ويستدعيها <c>docs/ops/O-01_ZATCA_PRODUCTION_RUNBOOK.md:14</c> —
/// تفتح قاعدةَ المُشغِّل الحقيقيّة من <c>database.json</c> <b>بلا سؤالٍ ولا علامة</b>، ثمّ
/// <b>تُنشئ حساباً بدور «مدير» اسمُه <c>uiaudit</c> بكلمةٍ منشورةٍ في المستودع</b>.</para>
///
/// <para><b>والإنشاءُ أخطرُ من إعادة التعيين</b>: إعادةُ التعيين <b>صاخبة</b> — يُقفَل
/// المُشغِّلُ خارجَ حسابه فيتّصل؛ <b>والحسابُ المُنشأ صامت</b>: لا أحدَ يُقفَل، ولا شيءَ
/// يبدو معطوباً، <b>ويبقى مديرٌ زائدٌ بكلمةٍ عامّةٍ في الدفاتر</b>.</para>
///
/// <para><b>ولماذا اسمُ القاعدة لا «نعم»:</b> علامةٌ منطقيّةٌ (<c>=1</c>) تُلصَق في سكربتٍ
/// مرّةً فتصير دائمة، <b>وتنتقل بين الأجهزة سالمة</b>. أمّا اسمُ القاعدة فلا ينتقل: مَن
/// أذِن لـ<c>brqosh_qa</c> على جهازه لا يأذن لـ<c>brqosh</c> على جهاز المُشغِّل، <b>فالإذنُ
/// يفقد صلاحيّتَه بمجرّد تغيّر الآلة</b> — وهو بالضبط ما يُراد.</para>
///
/// <para><b>وأين لا يعيش هذا الحارس — وهو قرارٌ مقصود:</b> لا يُستدعى من مسار إقلاع المنتج
/// ولا من <see cref="BrqoshDataRoot"/>. <b>وضعُه هناك يجعل سطراً واحداً يفصل تركيبَ عميلٍ عن
/// رفضِ الإقلاع</b> — وهذه الحملةُ دفعت ثمنَ ذلك مرّتين (ت-١ · م-١٣). <b>والضررُ الذي يمنعه
/// قابلٌ للإصلاح</b> (صفوفٌ موسومةٌ تُحذف وكلماتٌ تُعاد)، <b>والضررُ الذي قد يُحدثه ليس كذلك</b>
/// (تركيبٌ لا يُقلع عند عميل). فيعيش حيث الخطر: في الأدوات، ولا يمسّ المنتج.</para>
/// </summary>
public static class LiveBooksGuard
{
    /// <summary>متغيّرُ البيئة الذي يحمل <b>اسمَ القاعدة</b> المأذون بالكتابة فيها.</summary>
    public const string ConsentEnvironmentVariable = "BRQOSH_WRITE_TARGET";

    /// <summary>
    /// يتحقّق أنّ المُشغِّل أذِن <b>لهذه القاعدة بعينها</b>، وإلّا يرمي.
    /// </summary>
    /// <param name="resolvedTarget">اسمُ القاعدة كما استُخرج فعلاً من إعدادات الاتصال.</param>
    /// <param name="whatItWillDo">وصفُ ما ستفعله الأداةُ — يظهر في رسالة الرفض.</param>
    public static void RequireConsentFor(string resolvedTarget, string whatItWillDo)
    {
        if (string.IsNullOrWhiteSpace(resolvedTarget))
            throw new ArgumentException("‏لا يُطلب إذنٌ لهدفٍ بلا اسم.", nameof(resolvedTarget));

        var consent = Environment.GetEnvironmentVariable(ConsentEnvironmentVariable);

        if (string.Equals(consent?.Trim(), resolvedTarget, StringComparison.OrdinalIgnoreCase))
            return;

        // ‏والرسالةُ تُعلّم المخرجَين معاً: العزلَ (الأسلم) والإذنَ الصريح (حين يُقصد).
        throw new InvalidOperationException(
            $"‏هذه الأداةُ ستكتب في «{resolvedTarget}»: {whatItWillDo}\n"
            + (string.IsNullOrWhiteSpace(consent)
                ? "‏ولا إذنَ معطى.\n"
                : $"‏والإذنُ المعطى «{consent}» لقاعدةٍ أخرى — فالأرجحُ أنّك على آلةٍ غير التي أذِنتَ لها.\n")
            + "\n‏الأسلمُ أن تعزل بدل أن تأذن:\n"
            + $"    $env:{BrqoshDataRoot.OverrideEnvironmentVariable} = \"<مجلّدٌ مؤقّت>\"\n"
            + "‏فتعمل الأداةُ على دفاترَ منفصلةٍ ولا تلمس دفاترَ أحد.\n"
            + "\n‏وإن قصدتَ هذه القاعدةَ بعينها فأذَن لها **بالاسم**:\n"
            + $"    $env:{ConsentEnvironmentVariable} = \"{resolvedTarget}\"");
    }

    /// <summary>
    /// اسمُ القاعدة من سلسلة اتصال — ولا يُعيد سرّاً. يُستعمل في رسائل الإذن والرفض.
    /// </summary>
    public static string DescribeTarget(string? connectionString, bool isSqlite)
    {
        if (isSqlite)
            return "sqlite:" + Path.GetFileName(BrqoshDbContextFactory.DefaultSqlitePath());

        if (string.IsNullOrWhiteSpace(connectionString))
            return "(بلا سلسلة اتّصال)";

        // ‏يُقرأ `Database=` و`Host=` وحدَهما — **ولا تُنسخ السلسلةُ كاملةً** فتتسرّب كلمةُ
        // ‏المرور إلى سجلٍّ أو رسالةِ خطأ. وهي كلمةٌ حماها CEEF-236 بـDPAPI عمداً.
        string? host = null, database = null;

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0) continue;

            var key = part[..separator].Trim();
            var value = part[(separator + 1)..].Trim();

            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Server", StringComparison.OrdinalIgnoreCase)) host = value;
            else if (key.Equals("Database", StringComparison.OrdinalIgnoreCase)) database = value;
        }

        return database is { Length: > 0 }
            ? $"{host ?? "?"}/{database}"
            : "(بلا اسم قاعدة)";
    }
}
