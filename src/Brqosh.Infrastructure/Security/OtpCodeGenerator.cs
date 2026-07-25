using System.Security.Cryptography;
using System.Text;

namespace Brqosh.Infrastructure.Security;

internal static class OtpCodeGenerator
{
    private const int Digits = 6;

    public static string Generate() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString($"D{Digits}");

    public static string Hash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()));
        return Convert.ToHexString(bytes);
    }

    public static bool Verify(string code, string storedHash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(code)),
            Encoding.UTF8.GetBytes(storedHash));
}
