using Brqosh.Domain.Security;

namespace Brqosh.Infrastructure.Security;

/// <summary>إعدادات أمن API من المتغيرات البيئية — CEEF-138 AUDIT-SEC-001..004.</summary>
public static class ApiSecuritySettings
{
    public const string ApiKeyEnv = "BRQOSH_API_KEY";
    public const string ProvisioningKeyEnv = "BRQOSH_PROVISIONING_KEY";
    public const string SaaSModeEnv = "BRQOSH_SAAS_MODE";

    public static string? ReadApiKey() => Environment.GetEnvironmentVariable(ApiKeyEnv);

    public static string? ReadProvisioningKey() => Environment.GetEnvironmentVariable(ProvisioningKeyEnv);

    public static bool IsSaaSMode()
    {
        var raw = Environment.GetEnvironmentVariable(SaaSModeEnv);
        return string.Equals(raw, "1", StringComparison.Ordinal)
               || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// (م-٤ · ن-٤٥) <b>عزلُ المستأجرين — قرارٌ ثلاثيُّ القيم، وكان ثنائيّاً يفشل مفتوحاً.</b>
    ///
    /// <para><b>العطل:</b> <c>TenantContext.EnforceTenantIsolation</c> خاصّيّةٌ بلا مُهيّئ
    /// فافتراضُها <c>false</c>، وتُضبط من <c>BRQOSH_SAAS_MODE</c> وحدَه. <b>والمتغيّرُ لا
    /// يُضبط في أيّ وثيقةٍ ولا سكربتٍ ولا مُنصِّبٍ ولا CI</b> — مقيسٌ ببحثٍ على المستودع
    /// كلِّه أعاد موضعين: تعليقاً وإعلانَ الثابت. <b>وشاهدُ ضبطٍ موجب:</b> شقيقُه
    /// <c>BRQOSH_API_KEY</c> يظهر في خمسة ملفّاتِ نشرٍ وتشغيل. فالعزلُ مُطفأٌ في كلّ
    /// تنصيبٍ قائم، لا «يُنسى أحياناً».</para>
    ///
    /// <para><b>وأثرُه أنّ الحارسَ يفشل مفتوحاً:</b> <c>TenantGuard</c> يُعيد <c>true</c> لأيّ
    /// شركةٍ متى غاب مُعرِّفُ المستأجر. <b>وسردُ الشركات بلا ترويسةٍ يُعيد صفوفَ كلّ
    /// المستأجرين — ومعها <c>TenantId</c> نفسُه</b>، وهو المفتاحُ الذي يفحصه الحارسُ بعدها.
    /// فالتسريبُ يُكمل نفسَه.</para>
    ///
    /// <para><b>ولمَ لا يُقلب الافتراضُ إلى <c>true</c> ببساطة:</b> مضيفٌ أحاديُّ المنشأة
    /// يستضيف واجهتَه بنفسه <b>لا يُرسل الترويسةَ اليوم</b>، فقلبٌ أعمى يكسره. ولا يصلح
    /// الاشتقاقُ من البيانات كذلك: <c>Companies.TenantId</c> فريدٌ لكلّ شركة، <b>فمنشأةٌ
    /// محلّيّةٌ بشركتين تبدو مستأجرَين</b>.</para>
    ///
    /// <para><b>فالقرارُ ثلاثيّ:</b> <c>1</c>/<c>true</c> يفرض · <c>0</c>/<c>false</c> يُعطّل
    /// <b>صراحةً</b> لمضيفٍ أحاديٍّ مقصود · <b>والغيابُ يفرض في الإنتاج ويُعطّل خارجه</b>.
    /// وهو عينُ نمطِ <see cref="ResolveApiRole"/>: <b>ما لا يُصرَّح به يفشل مغلقاً حيث يضرّ.</b></para>
    /// </summary>
    /// <param name="configured">القيمةُ المقروءةُ من الإعداد أو البيئة — أو <c>null</c> إن غابت.</param>
    public static bool ResolveTenantIsolation(bool isProduction, string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return isProduction;

        var raw = configured.Trim();

        if (string.Equals(raw, "1", StringComparison.Ordinal)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(raw, "0", StringComparison.Ordinal)
            || string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
            return false;

        // ‏قيمةٌ لا تُفهم — خطأٌ مطبعيٌّ من مُشغِّل. **ولا تُقرأ تعطيلاً**: ذلك يجعل
        // ‏`BRQOSH_SAAS_MODE=yes` يفتح القاعدةَ كلَّها صامتاً. فتُعامَل كالغياب: يُفرض
        // ‏العزلُ في الإنتاج.
        return isProduction;
    }

    public const string ApiRoleEnv = "BRQOSH_API_ROLE";

    /// <summary>
    /// (CEEF-229 · م2-١) دور مستدعي API — <b>يضبطه الخادم، لا المستدعي</b>.
    ///
    /// <para><b>ما كان:</b> الوسيط يبدأ بـ<c>var role = UserRole.Administrator;</c> ثم يسمح
    /// لترويسة <c>X-Api-Role</c> بتغييره. أي أن الدور يُعلنه المستدعي، <b>وإغفالُ الترويسة
    /// يمنحه أعلى امتياز</b>. فمواضع فرض الصلاحيات السبعون كلُّها بلا أثرٍ أمام أيّ مستدعي API.
    /// المصادقة بمفتاح مشترك موجودة، لكنها تُثبت <b>مَن</b> يتصل لا <b>ماذا يحقّ له</b>.</para>
    ///
    /// <para><b>وما صار:</b> الدور يُقرأ من <c>BRQOSH_API_ROLE</c>. والترويسة تبقى مقبولة —
    /// لكنها <b>تُضيّق ولا تُوسّع</b>: لمستدعٍ أن يتنازل طوعاً عن امتيازٍ يملكه، وليس له أن
    /// يمنح نفسه امتيازاً لم يُمنح.</para>
    ///
    /// <para><b>والافتراض يفشل إلى الأمان في الإنتاج وحده:</b> <c>Viewer</c> عند غياب الضبط،
    /// فتُرفض الكتابة برسالة صريحة تسمّي الصلاحية الناقصة بدل أن تمرّ صامتة. وخارج الإنتاج
    /// يبقى <c>Administrator</c> فلا يتعطّل تطويرٌ ولا تكامل تجريبيّ. ولم أجعله شرطاً لبدء
    /// التشغيل — كما هو المفتاح — كي لا يمنع الترقية تركيباً قائماً؛ الرفض المسمَّى أوضح من
    /// خدمةٍ لا تُقلع.</para>
    /// </summary>
    /// <summary>
    /// (م-٢ · ن-٢٦) <b>تحليلٌ يرفض ما ليس دوراً معرَّفاً.</b>
    ///
    /// <para><c>Enum.TryParse&lt;UserRole&gt;</c> <b>يقبل السلاسل الرقميّة</b>: «99» يُنتج
    /// <c>(UserRole)99</c> و<c>TryParse</c> يُعيد <c>true</c>. ولا يُصلح ذلك <c>Enum.IsDefined</c>
    /// وحدَه إن جاء بعد الاستعمال — فيُدمج هنا في موضعٍ واحد.</para>
    ///
    /// <para>وموضعا التحليل كلاهما مُحصَّن: هذا (متغيّرُ البيئة — خطأٌ مطبعيٌّ من مُشغِّل)،
    /// و<see cref="NarrowByRequest"/> (ترويسةُ الطلب — مُدخلٌ من الخارج).</para>
    /// </summary>
    public static UserRole ResolveApiRole(bool isProduction)
    {
        var raw = Environment.GetEnvironmentVariable(ApiRoleEnv);
        if (TryParseDefinedRole(raw, out var configured))
            return configured;

        return isProduction ? UserRole.Viewer : UserRole.Administrator;
    }

    /// <summary>
    /// تحليلُ دورٍ <b>معرَّفٍ في التعداد</b> — لا رقماً يقبله <c>TryParse</c> صامتاً.
    /// </summary>
    private static bool TryParseDefinedRole(string? raw, out UserRole role)
    {
        role = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (!Enum.TryParse<UserRole>(raw.Trim(), ignoreCase: true, out var parsed)) return false;
        if (!Enum.IsDefined(parsed)) return false;   // ‏«99» يجتاز TryParse ويسقط هنا.
        role = parsed;
        return true;
    }

    /// <summary>
    /// الدور الفعّال للطلب: المضبوط خادمياً، ما لم تطلب الترويسة دوراً <b>أقلّ</b> امتيازاً.
    /// وترتيب <see cref="UserRole"/> تنازليّ (Administrator=0 … Viewer=3)، فالأكبر أقلّ امتيازاً.
    /// </summary>
    /// <remarks>
    /// (م-٢ · ن-٢٦) <b>ترويسةٌ برقمٍ خارج التعداد كانت تُوسّع الدور لا تُضيّقه.</b>
    /// <c>X-Api-Role: 99</c> ⟸ <c>(UserRole)99</c>، وهو أكبرُ من المضبوط فيُعاد «تضييقاً» —
    /// <b>ثمّ يمرّ من حرّاس المنع الثلاثة لأنّه ليس على قائمة أحد</b>. فالتحليلُ الآن
    /// يشترط دوراً معرَّفاً، والحرّاسُ صاروا قوائمَ سماح: طبقتان لا واحدة.
    /// </remarks>
    public static UserRole NarrowByRequest(UserRole configured, string? requestedRole)
    {
        if (!TryParseDefinedRole(requestedRole, out var requested))
            return configured;

        return requested > configured ? requested : configured;
    }

    public static void EnsureProductionApiKey(string? apiKey, bool isProduction)
    {
        if (isProduction && string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException($"{ApiKeyEnv} is required in Production.");
    }

    public static void EnsureProductionProvisioningKey(string? provisioningKey, bool isProduction)
    {
        if (isProduction && string.IsNullOrWhiteSpace(provisioningKey))
            throw new InvalidOperationException($"{ProvisioningKeyEnv} is required in Production.");
    }
}
