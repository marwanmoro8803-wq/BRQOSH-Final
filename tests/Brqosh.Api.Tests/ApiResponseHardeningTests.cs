using System.Net;
using System.Text.Json;
using Brqosh.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Brqosh.Api.Tests;

/// <summary>
/// (جاهزيّةُ الإنتاج · C/F) ‏كلُّ ردٍّ يحمل ترويساتِ الأمان ومعرّفَ الترابط، والاستثناءُ غيرُ الملتقَط يُردّ ٥٠٠ بنوعٍ عامٍّ
/// ‏ومعرّفٍ — <b>لا بنصّه</b>. ‏الاختباران الأوّلان على الخادم الحقيقيّ (فالوصلُ في <c>Program.cs</c> مقيس)، والباقي على
/// ‏الوسيط نفسِه بطلبٍ يرمي — إذ لا مسارَ في الواجهة يرمي عمداً.
/// </summary>
public class ApiResponseHardeningTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/customers?companyId=1")]
    public async Task EveryResponse_CarriesTheSecurityHeaders(string path)
    {
        await using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Single(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Single(response, "Referrer-Policy"));
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", Single(response, "Content-Security-Policy"));
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(Single(response, ApiResponseHardeningMiddleware.CorrelationHeader)));
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task AWellFormedCorrelationId_IsEchoed_AndAnInjectedOne_IsReplaced()
    {
        await using var factory = new IsolatedApiFactory();
        var client = factory.CreateClient();

        using var good = new HttpRequestMessage(HttpMethod.Get, "/health");
        good.Headers.Add(ApiResponseHardeningMiddleware.CorrelationHeader, "pos-7.req:42");
        var echoed = await client.SendAsync(good);
        Assert.Equal("pos-7.req:42", Single(echoed, ApiResponseHardeningMiddleware.CorrelationHeader));

        using var bad = new HttpRequestMessage(HttpMethod.Get, "/health");
        bad.Headers.TryAddWithoutValidation(ApiResponseHardeningMiddleware.CorrelationHeader, "x y <script>");
        var replaced = await client.SendAsync(bad);
        Assert.NotEqual("x y <script>", Single(replaced, ApiResponseHardeningMiddleware.CorrelationHeader));
    }

    [Fact]
    public async Task AnUnhandledException_Returns500WithTheCorrelationId_AndNotItsText()
    {
        var middleware = new ApiResponseHardeningMiddleware(
            _ => throw new InvalidOperationException("Host=db-internal;Password=TopSecret; relation \"Invoices\" broke"),
            NullLogger<ApiResponseHardeningMiddleware>.Instance);
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
        context.Request.Path = "/api/v1/invoices";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("TopSecret", body, StringComparison.Ordinal);
        Assert.DoesNotContain("db-internal", body, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(body);
        Assert.Equal("internal_error", json.RootElement.GetProperty("error").GetString());
        Assert.Equal("trace-1", json.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("trace-1", context.Response.Headers[ApiResponseHardeningMiddleware.CorrelationHeader].ToString());
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
    }

    // ‏طلبٌ معيبٌ يبقى ٤٠٠ لا يصير ٥٠٠ — والمسارُ الحقيقيّ بلا companyId يُقاس في ApiIntegrationTests.
    [Fact]
    public async Task ABadRequest_KeepsItsStatus_AndIsNotAnInternalError()
    {
        var middleware = new ApiResponseHardeningMiddleware(
            _ => throw new BadHttpRequestException("Required parameter \"long companyId\" was not provided."),
            NullLogger<ApiResponseHardeningMiddleware>.Instance);
        var context = new DefaultHttpContext { TraceIdentifier = "trace-2" };
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = JsonDocument.Parse(await new StreamReader(context.Response.Body).ReadToEndAsync());
        Assert.Equal("bad_request", json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task AClientThatHungUp_GetsNoResponseWritten()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var middleware = new ApiResponseHardeningMiddleware(
            ctx => throw new OperationCanceledException(ctx.RequestAborted),
            NullLogger<ApiResponseHardeningMiddleware>.Instance);
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(0, context.Response.Body.Length);
    }

    private static string Single(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values)
        : response.Content.Headers.TryGetValues(name, out var content) ? string.Join(",", content)
        : string.Empty;
}
