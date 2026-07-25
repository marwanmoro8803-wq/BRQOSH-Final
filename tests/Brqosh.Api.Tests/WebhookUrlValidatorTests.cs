using Brqosh.Infrastructure.Webhooks;

namespace Brqosh.Api.Tests;

public class WebhookUrlValidatorTests
{
    [Fact]
    public void Https_public_url_allowed()
    {
        var ex = Record.Exception(() =>
            WebhookUrlValidator.ValidateOrThrow("https://example.com/hooks/brqosh"));
        Assert.Null(ex);
    }

    [Fact]
    public void Loopback_blocked()
    {
        Assert.Throws<ArgumentException>(() =>
            WebhookUrlValidator.ValidateOrThrow("https://127.0.0.1/hook"));
    }

    [Fact]
    public void Private_ip_blocked()
    {
        Assert.Throws<ArgumentException>(() =>
            WebhookUrlValidator.ValidateOrThrow("https://192.168.1.10/hook"));
    }

    [Fact]
    public void Http_allowed_only_when_insecure_local_flag()
    {
        WebhookUrlValidator.ValidateOrThrow("http://example.com/hook", allowInsecureLocal: true);
        Assert.Throws<ArgumentException>(() =>
            WebhookUrlValidator.ValidateOrThrow("http://example.com/hook", allowInsecureLocal: false));
    }
}
