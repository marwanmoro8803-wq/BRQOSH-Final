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
    public static UserRole ResolveApiRole(bool isProduction)
    {
        var raw = Environment.GetEnvironmentVariable(ApiRoleEnv);
        if (!string.IsNullOrWhiteSpace(raw) && Enum.TryParse<UserRole>(raw.Trim(), ignoreCase: true, out var configured))
            return configured;

        return isProduction ? UserRole.Viewer : UserRole.Administrator;
    }

    /// <summary>
    /// الدور الفعّال للطلب: المضبوط خادمياً، ما لم تطلب الترويسة دوراً <b>أقلّ</b> امتيازاً.
    /// وترتيب <see cref="UserRole"/> تنازليّ (Administrator=0 … Viewer=3)، فالأكبر أقلّ امتيازاً.
    /// </summary>
    public static UserRole NarrowByRequest(UserRole configured, string? requestedRole)
    {
        if (string.IsNullOrWhiteSpace(requestedRole)
            || !Enum.TryParse<UserRole>(requestedRole.Trim(), ignoreCase: true, out var requested))
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
