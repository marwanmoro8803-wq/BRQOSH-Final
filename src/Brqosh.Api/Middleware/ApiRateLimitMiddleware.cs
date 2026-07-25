using System.Collections.Concurrent;

namespace Brqosh.Api.Middleware;

/// <summary>G-47 / Shura P1 — حد معدّل طلبات أساسي لكل مفتاح API أو عنوان IP.</summary>
public sealed class ApiRateLimitMiddleware(RequestDelegate next)
{
    private const int MaxRequestsPerMinute = 120;
    private static readonly ConcurrentDictionary<string, (long Bucket, int Count)> Windows = new();

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!ctx.Request.Path.StartsWithSegments("/api"))
        {
            await next(ctx);
            return;
        }

        var key = ctx.Request.Headers.TryGetValue("X-Api-Key", out var apiKey) && !string.IsNullOrWhiteSpace(apiKey)
            ? $"key:{apiKey}"
            : $"ip:{ctx.Connection.RemoteIpAddress}";

        var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        var entry = Windows.AddOrUpdate(
            key,
            _ => (bucket, 1),
            (_, old) => old.Bucket == bucket ? (bucket, old.Count + 1) : (bucket, 1));

        if (entry.Count > MaxRequestsPerMinute)
        {
            ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await ctx.Response.WriteAsJsonAsync(new { error = "rate_limit_exceeded" });
            return;
        }

        await next(ctx);
    }
}
