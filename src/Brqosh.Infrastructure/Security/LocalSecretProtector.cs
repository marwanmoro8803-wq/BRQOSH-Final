using System.Security.Cryptography;
using System.Text;

namespace Brqosh.Infrastructure.Security;

/// <summary>تشفير أسرار محلية (DPAPI على Windows) — ZATCA-002.</summary>
public static class LocalSecretProtector
{
    private const string Prefix = "BRQOSH1:";

    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain))
            return plain ?? "";
        if (plain.StartsWith(Prefix, StringComparison.Ordinal))
            return plain;

        if (!OperatingSystem.IsWindows())
            return plain;

        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(bytes);
    }

    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
            return stored ?? "";

        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            return stored;

        if (!OperatingSystem.IsWindows())
            return "";

        var payload = stored[Prefix.Length..];
        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(payload), null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
