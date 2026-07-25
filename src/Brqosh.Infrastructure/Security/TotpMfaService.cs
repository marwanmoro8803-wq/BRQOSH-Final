using System.Security.Cryptography;
using System.Text;

namespace Brqosh.Infrastructure.Security;

/// <summary>TOTP MFA (RFC 6238) — LIFECYCLE-2–4.</summary>
public static class TotpMfaService
{
    private const int Digits = 6;
    private const int StepSeconds = 30;

    public static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Base32Encode(bytes);
    }

    public static bool ValidateCode(string secretBase32, string code, DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(secretBase32) || string.IsNullOrWhiteSpace(code))
            return false;
        if (code.Trim().Length != Digits || !code.Trim().All(char.IsDigit))
            return false;

        var key = Base32Decode(secretBase32.Trim().ToUpperInvariant());
        var counter = GetCounter(utcNow ?? DateTime.UtcNow);
        for (var drift = -1; drift <= 1; drift++)
        {
            var expected = ComputeCode(key, counter + drift);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected),
                    Encoding.ASCII.GetBytes(code.Trim())))
                return true;
        }

        return false;
    }

    private static long GetCounter(DateTime utc) =>
        (long)(utc - DateTime.UnixEpoch).TotalSeconds / StepSeconds;

    private static string ComputeCode(byte[] key, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        var hash = HMACSHA1.HashData(key, counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);
        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString($"D{Digits}");
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = data[0], next = 1, bitsLeft = 8;
        while (bitsLeft > 0 || next < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (next < data.Length)
                {
                    buffer = (buffer << 8) | data[next++];
                    bitsLeft += 8;
                }
                else
                {
                    var pad = 5 - bitsLeft;
                    buffer <<= pad;
                    bitsLeft += pad;
                }
            }

            var index = (buffer >> (bitsLeft - 5)) & 0x1F;
            bitsLeft -= 5;
            result.Append(alphabet[index]);
        }

        return result.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bitsLeft = 0;
        foreach (var c in input.Where(char.IsLetterOrDigit))
        {
            var val = alphabet.IndexOf(char.ToUpperInvariant(c));
            if (val < 0) continue;
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bytes.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }

        return bytes.ToArray();
    }
}
