using System.Security.Cryptography;
using System.Text;
using Brqosh.Infrastructure.Security;
using Xunit;

namespace Brqosh.Application.Tests.Security;

/// <summary>‏(Phase 7 · مراجعةٌ أمنيّة) ‏PBKDF2 بعدد OWASP، والقديمُ يُقرأ ثمّ يُعاد، والتالفُ يُرفض بلا استثناء.</summary>
public class ThePasswordHashMeetsCurrentGuidanceTests
{
    [Fact]
    public void NewHashes_Use600kIterations_AndNeedNoRehash()
    {
        var hash = UserAuthService.HashPassword("S3cure!pass");
        Assert.StartsWith("pbkdf2:600000:", hash, StringComparison.Ordinal);
        Assert.True(UserAuthService.VerifyPassword("S3cure!pass", hash));
        Assert.False(UserAuthService.VerifyPassword("wrong", hash));
        Assert.False(UserAuthService.NeedsRehash(hash));
    }

    [Fact]
    public void OldHashes_StillVerify_ButAreFlaggedForRehash()
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var old = $"pbkdf2:100000:{Convert.ToHexString(salt)}:" + Convert.ToHexString(
            Rfc2898DeriveBytes.Pbkdf2("legacy-pass", salt, 100_000, HashAlgorithmName.SHA256, 32));
        Assert.True(UserAuthService.VerifyPassword("legacy-pass", old));
        Assert.True(UserAuthService.NeedsRehash(old));

        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("legacy-pass"))).ToLowerInvariant();
        Assert.True(UserAuthService.VerifyPassword("legacy-pass", sha));
        Assert.False(UserAuthService.VerifyPassword("other", sha));
        Assert.True(UserAuthService.NeedsRehash(sha));
    }

    [Theory]
    [InlineData("pbkdf2:600000:ZZ:00")]
    [InlineData("pbkdf2:0:00:00")]
    [InlineData("pbkdf2:600000:00:")]
    [InlineData("")]
    public void CorruptHashes_AreRejected_NotThrown(string stored) =>
        Assert.False(UserAuthService.VerifyPassword("x", stored));
}
