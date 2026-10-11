using System.Net;
using Brqosh.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace Brqosh.Api.Tests;

/// <summary>
/// جولةُ التقوية الأولى (2026-10-11): حدُّ المعدّل لا يُفتح بمفتاحٍ خاطئ،
/// واسمُ المستخدم من الترويسة لا ينتحل مستخدمي سطح المكتب في سجلّ التدقيق.
/// </summary>
public class ApiHardeningRound1Tests
{
    private const string ConfiguredKey = "configured-key-0123456789abcdef";

    private static DefaultHttpContext Request(string? presentedKey, string ip = "203.0.113.77")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/v1/customers";
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (presentedKey is not null)
            ctx.Request.Headers["X-Api-Key"] = presentedKey;
        return ctx;
    }

    [Fact]
    public void Guessed_keys_share_the_caller_ip_bucket()
    {
        var first = ApiRateLimitMiddleware.ResolveBucketKey(Request("guess-1"), ConfiguredKey);
        var second = ApiRateLimitMiddleware.ResolveBucketKey(Request("guess-2"), ConfiguredKey);
        var none = ApiRateLimitMiddleware.ResolveBucketKey(Request(null), ConfiguredKey);

        Assert.Equal("ip:203.0.113.77", first);
        Assert.Equal(first, second);
        Assert.Equal(first, none);
    }

    [Fact]
    public void Valid_key_gets_its_own_bucket_without_storing_the_secret()
    {
        var bucket = ApiRateLimitMiddleware.ResolveBucketKey(Request(ConfiguredKey), ConfiguredKey);

        Assert.StartsWith("key:", bucket);
        Assert.DoesNotContain(ConfiguredKey, bucket);
    }

    [Fact]
    public void Without_configured_key_every_caller_is_bucketed_by_ip()
    {
        var bucket = ApiRateLimitMiddleware.ResolveBucketKey(Request("anything"), configuredKey: null);

        Assert.Equal("ip:203.0.113.77", bucket);
    }

    [Theory]
    [InlineData(null, "api-integration")]
    [InlineData("   ", "api-integration")]
    [InlineData("admin", "api:admin")]
    [InlineData(" erp-bot ", "api:erp-bot")]
    [InlineData("ad\u202Emin", "api:admin")]
    [InlineData("a\r\nb", "api:ab")]
    public void Header_username_is_marked_and_sanitised(string? header, string expected)
    {
        Assert.Equal(expected, ApiSecurityContextMiddleware.ResolveApiUsername(header));
    }

    /// <summary>مسبارُ الجاهزيّة في الإنتاج يُسمّي نوعَ الخطأ ولا يُفشي المضيفَ ولا مستخدمَ القاعدة.</summary>
    [Fact]
    public void Production_readiness_names_the_error_type_without_leaking_the_infrastructure()
    {
        var error = new InvalidOperationException("wrapper",
            new TimeoutException("password authentication failed for user \"brqosh_admin\" at db-prod-01.internal"));

        var text = ReadinessProbe.DatabaseErrorText(error, isProduction: true);

        Assert.Equal(nameof(TimeoutException), text);
        Assert.DoesNotContain("brqosh_admin", text);
        Assert.DoesNotContain("db-prod-01", text);
    }

    /// <summary>وخارجَ الإنتاج يُردّ نصُّ السبب الجذريّ كاملاً — فأوّلُ دقيقةٍ في العطل أثمنُها.</summary>
    [Fact]
    public void Non_production_readiness_returns_the_root_message()
    {
        var error = new InvalidOperationException("wrapper", new TimeoutException("host db-dev not reachable"));

        Assert.Equal("host db-dev not reachable", ReadinessProbe.DatabaseErrorText(error, isProduction: false));
    }

    [Fact]
    public void Header_username_is_length_capped()
    {
        var resolved = ApiSecurityContextMiddleware.ResolveApiUsername(new string('x', 500));

        Assert.Equal(
            ApiSecurityContextMiddleware.ApiUsernamePrefix.Length + ApiSecurityContextMiddleware.MaxApiUsernameLength,
            resolved.Length);
    }
}
