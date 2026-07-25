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

            var role = UserRole.Administrator;
            if (ctx.Request.Headers.TryGetValue("X-Api-Role", out var roleHeader)
                && Enum.TryParse<UserRole>(roleHeader.ToString(), ignoreCase: true, out var parsed))
                role = parsed;

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
