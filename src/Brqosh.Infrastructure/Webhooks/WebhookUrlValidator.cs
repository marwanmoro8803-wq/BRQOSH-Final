using System.Net;
using System.Net.Sockets;

namespace Brqosh.Infrastructure.Webhooks;

/// <summary>منع SSRF عند تسجيل Webhook — AUDIT-SEC-003.</summary>
public static class WebhookUrlValidator
{
    public static void ValidateOrThrow(string targetUrl, bool allowInsecureLocal = false)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
            throw new ArgumentException("targetUrl must be absolute URI");

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !(allowInsecureLocal && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("targetUrl must use https");

        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("targetUrl must not contain user credentials");

        if (string.IsNullOrWhiteSpace(uri.Host))
            throw new ArgumentException("targetUrl host required");

        if (IsBlockedHost(uri.Host, allowInsecureLocal))
            throw new ArgumentException("targetUrl host is not allowed");

        if (IPAddress.TryParse(uri.Host, out var ip) && IsPrivateOrLoopback(ip))
            throw new ArgumentException("targetUrl must not target private or loopback addresses");
    }

    private static bool IsBlockedHost(string host, bool allowInsecureLocal)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return !allowInsecureLocal;

        return host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
               || host.Equals("metadata.google.internal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivateOrLoopback(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal;

        var bytes = ip.GetAddressBytes();
        return bytes[0] switch
        {
            10 => true,
            127 => true,
            169 when bytes[1] == 254 => true,
            172 when bytes[1] is >= 16 and <= 31 => true,
            192 when bytes[1] == 168 => true,
            _ => false,
        };
    }
}
