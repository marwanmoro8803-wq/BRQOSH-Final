using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Brqosh.Application.Tests.Platform;

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٨ · ‏`installer-deployment-ops#3`) <b>خطّافُ التوقيع يقبل الشهادةَ كما تُصدَر اليوم — من مخزن ويندوز ببصمتها.</b>
///
/// <para>‏كان <c>build-msi.ps1</c> لا يقبل إلّا ملفَّ PFX، والجهاتُ المُصدِرةُ لم تعد تُصدر شهادةَ توقيع كودٍ بمفتاحٍ يُصدَّر إلى ملفّ (المفتاحُ
/// ‏على رمزٍ أو HSM منذ 2023) — فـ«الخطّافُ جاهزٌ ينتظر الشهادة» لم يكن صحيحاً. والاختبارُ يُشغّل <c>installer/signing.ps1 -Describe</c>
/// ‏نفسَه — ما يُشغّله البناء — <b>ويُثبت أنّ كلمةَ المرور لا تُطبع</b>.</para>
/// </summary>
public class TheSigningHookAcceptsTokenCertificatesTests : IDisposable
{
    private const string Thumbprint = "0123456789ABCDEF0123456789ABCDEF01234567";
    private readonly string _pfx = Path.Combine(Path.GetTempPath(), $"brqosh-sign-{Guid.NewGuid():N}.pfx");

    public TheSigningHookAcceptsTokenCertificatesTests() => File.WriteAllText(_pfx, "not a real pfx");

    public void Dispose()
    {
        if (File.Exists(_pfx)) File.Delete(_pfx);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void A_certificate_store_thumbprint_signs_with_sha1_selection()
    {
        var r = Describe(new() { ["BRQOSH_SIGNING_THUMBPRINT"] = "01 23 45 67 89 ab cd ef 01 23 45 67 89 ab cd ef 01 23 45 67" });

        Assert.Equal("CertificateStore", r.Mode);
        Assert.Equal(["/sha1", Thumbprint, "/fd", "SHA256", "/tr", "http://timestamp.digicert.com", "/td", "SHA256"], r.Args);
    }

    [Fact]
    public void A_pfx_still_signs_and_its_password_is_never_printed()
    {
        var r = Describe(new() { ["BRQOSH_SIGNING_PFX"] = _pfx, ["BRQOSH_SIGNING_PASSWORD"] = "s3cret-pass" });

        Assert.Equal("Pfx", r.Mode);
        Assert.Equal(["/f", _pfx, "/p", "***"], r.Args.Take(4));
        Assert.DoesNotContain("s3cret-pass", r.Raw, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("BOTH")]
    [InlineData("not-hex")]
    [InlineData("MISSING-PFX")]
    public void A_contradictory_or_invalid_setting_is_an_error_not_a_silent_choice(string kind)
    {
        var env = kind switch
        {
            "BOTH" => new Dictionary<string, string> { ["BRQOSH_SIGNING_THUMBPRINT"] = Thumbprint, ["BRQOSH_SIGNING_PFX"] = _pfx },
            "not-hex" => new Dictionary<string, string> { ["BRQOSH_SIGNING_THUMBPRINT"] = "zzzz" },
            _ => new Dictionary<string, string> { ["BRQOSH_SIGNING_PFX"] = _pfx + ".missing" },
        };

        var r = Describe(env);

        Assert.Equal("Error", r.Mode);
        Assert.Empty(r.Args);
    }

    /// <summary>شاهدُ ضبط: بلا إعدادٍ لا توقيع — والبناءُ يُعلن «غيرُ موقَّعة» ولا يسقط.</summary>
    [Fact]
    public void No_setting_means_no_signing() => Assert.Equal("None", Describe(new()).Mode);

    [Fact]
    public void The_timestamp_server_can_be_chosen()
    {
        var r = Describe(new() { ["BRQOSH_SIGNING_THUMBPRINT"] = Thumbprint, ["BRQOSH_SIGNING_TIMESTAMP"] = "http://ts.example" });

        Assert.Contains("http://ts.example", r.Args);
    }

    private sealed record Decision(string Mode, string[] Args, string Raw);

    private static Decision Describe(Dictionary<string, string> env)
    {
        var psi = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-File", Path.Combine(RepositoryRoot(), "installer", "signing.ps1"), "-Describe" })
            psi.ArgumentList.Add(arg);
        foreach (var name in new[] { "BRQOSH_SIGNING_THUMBPRINT", "BRQOSH_SIGNING_PFX", "BRQOSH_SIGNING_PASSWORD", "BRQOSH_SIGNING_TIMESTAMP" })
            psi.Environment.Remove(name);
        foreach (var (name, value) in env)
            psi.Environment[name] = value;

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(60_000), "signing.ps1 لم تنتهِ في 60 ثانية");
        Assert.True(process.ExitCode == 0, $"signing.ps1 خرجت بـ{process.ExitCode}:\n{stderr}");

        using var json = JsonDocument.Parse(stdout.Trim());
        var root = json.RootElement;
        var args = root.GetProperty("Args").ValueKind == JsonValueKind.Array
            ? root.GetProperty("Args").EnumerateArray().Select(a => a.GetString() ?? "").ToArray()
            : [];
        return new Decision(root.GetProperty("Mode").GetString() ?? "", args, stdout);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Brqosh.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("جذرُ المستودع غير موجود.");
    }
}
