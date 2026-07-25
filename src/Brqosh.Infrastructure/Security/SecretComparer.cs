using System.Security.Cryptography;
using System.Text;

namespace Brqosh.Infrastructure.Security;

public static class SecretComparer
{
    public static bool Equals(string? expected, string? presented)
    {
        if (string.IsNullOrEmpty(expected) || presented is null)
            return false;

        var exp = Encoding.UTF8.GetBytes(expected);
        var act = Encoding.UTF8.GetBytes(presented);
        if (exp.Length != act.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(exp, act);
    }
}
