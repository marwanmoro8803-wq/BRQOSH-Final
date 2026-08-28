using Brqosh.Application.Setup;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;

namespace Brqosh.Api.Middleware;

/// <summary>AUDIT-SEC-005/016 — ربط SecurityContext بمسارات API بعد المصادقة.</summary>
public sealed class ApiSecurityContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            var username = ctx.Request.Headers.TryGetValue("X-Api-User", out var userHeader)
                && !string.IsNullOrWhiteSpace(userHeader)
                ? userHeader.ToString().Trim()
                : "api-integration";

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
