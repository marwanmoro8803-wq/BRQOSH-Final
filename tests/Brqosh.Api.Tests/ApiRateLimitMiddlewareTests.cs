using Brqosh.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace Brqosh.Api.Tests;

public class ApiRateLimitMiddlewareTests
{
    [Fact]
    public async Task Under_limit_allows_request()
    {
        var called = false;
        RequestDelegate next = _ =>
        {
            called = true;
            return Task.CompletedTask;
        };

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/v1/health-check-proxy";
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.10");

        await new ApiRateLimitMiddleware(next).InvokeAsync(ctx);

        Assert.True(called);
        Assert.NotEqual(StatusCodes.Status429TooManyRequests, ctx.Response.StatusCode);
    }
}
