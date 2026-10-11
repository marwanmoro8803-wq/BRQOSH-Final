using Brqosh.Application.Setup;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Api.Middleware;

/// <summary>AUDIT-SEC-005/016 — ربط SecurityContext بمسارات API بعد المصادقة.</summary>
public sealed class ApiSecurityContextMiddleware(RequestDelegate next)
{
    internal const string DefaultApiUsername = "api-integration";
    internal const string ApiUsernamePrefix = "api:";
    internal const int MaxApiUsernameLength = 64;

    /// <summary>
    /// <b>اسمُ المستخدم من الترويسة يُوسَم بأصله.</b> كان <c>X-Api-User</c> يُكتب في سجلّ
    /// التدقيق كما أرسله المُتّصل — فمفتاحُ تكاملٍ واحدٌ يستطيع أن يُسجِّل قيداً باسم
    /// <c>admin</c> أو باسم أيّ محاسبٍ على سطح المكتب، <b>وسجلُّ التدقيق دليلٌ لا زخرفة</b>.
    /// فيُسبق بـ<c>api:</c>، ويُنزع منه ما لا يُطبع (ومنه محارفُ الاتّجاه التي تقلب العرض)، ويُقصّ طولُه.
    /// </summary>
    internal static string ResolveApiUsername(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return DefaultApiUsername;

        var cleaned = new string(header.Trim()
            .Where(c => !char.IsControl(c)
                        && System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                            != System.Globalization.UnicodeCategory.Format)
            .Take(MaxApiUsernameLength)
            .ToArray()).Trim();

        return cleaned.Length == 0 ? DefaultApiUsername : ApiUsernamePrefix + cleaned;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            var username = ResolveApiUsername(
                ctx.Request.Headers.TryGetValue("X-Api-User", out var userHeader) ? userHeader.ToString() : null);

            // (CEEF-229 · م2-١) الدور يُضبط خادمياً، والترويسة تُضيّقه ولا تُوسّعه.
            // كان يبدأ من `Administrator` ثم تُغيّره ترويسةٌ يرسلها المستدعي — فإغفالها يمنح
            // أعلى امتياز، وتُصبح مواضع فرض الصلاحيات السبعون بلا أثرٍ على سطح API كلّه.
            var configured = ApiSecuritySettings.ResolveApiRole(AppEnvironmentConfig.IsProduction);
            var role = ctx.Request.Headers.TryGetValue("X-Api-Role", out var roleHeader)
                ? ApiSecuritySettings.NarrowByRequest(configured, roleHeader.ToString())
                : configured;

            SecurityContext.CurrentUsername = username;
            SecurityContext.CurrentRole = role;
        }

        try
        {
            await next(ctx);
        }
        finally
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                SecurityContext.CurrentUsername = null;
                SecurityContext.CurrentRole = null;
            }
        }
    }
}
