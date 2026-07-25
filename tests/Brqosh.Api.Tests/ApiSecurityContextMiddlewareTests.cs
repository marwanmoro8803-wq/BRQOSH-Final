using Brqosh.Api.Middleware;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Security;
using Microsoft.AspNetCore.Http;

namespace Brqosh.Api.Tests;

public class ApiSecurityContextMiddlewareTests
{
    [Fact]
    public async Task Api_route_sets_and_clears_security_context()
    {
        var username = "";
        RequestDelegate next = _ =>
        {
            username = SecurityContext.ResolveUsername();
            Assert.Equal(UserRole.Administrator, SecurityContext.CurrentRole);
            return Task.CompletedTask;
        };

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/v1/customers";
        ctx.Request.Headers["X-Api-User"] = "integration-bot";

        await new ApiSecurityContextMiddleware(next).InvokeAsync(ctx);

        Assert.Equal("integration-bot", username);
        Assert.Null(SecurityContext.CurrentUsername);
        Assert.Null(SecurityContext.CurrentRole);
    }

    [Fact]
    public async Task Api_role_header_parsed()
    {
        UserRole? roleDuring = null;
        RequestDelegate next = _ =>
        {
            roleDuring = SecurityContext.CurrentRole;
            return Task.CompletedTask;
        };

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/v1/billing-plans";
        ctx.Request.Headers["X-Api-Role"] = "Viewer";

        await new ApiSecurityContextMiddleware(next).InvokeAsync(ctx);

        Assert.Equal(UserRole.Viewer, roleDuring);
    }
}
