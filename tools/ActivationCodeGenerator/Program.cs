// ‏(Phase 7) ‏مولّدُ رموز التفعيل — أداةُ المورّد وحده، لا تُشحن مع البرنامج ولا تُضاف إلى الحلّ ولا إلى المُثبِّت.
// ‏تحمل المفتاحَ الخاصّ (ECDSA P-256) على جهاز المورّد؛ والبرنامجُ لا يعرف إلّا المفتاحَ العامّ (EmbeddedActivationKeys).
using System.Security.Cryptography;
using Brqosh.Infrastructure.Licensing;

return args.Length == 0 ? Usage() : args[0] switch
{
    "gen-keys" => GenKeys(args),
    "issue" => Issue(args),
    _ => Usage(),
};

static int Usage()
{
    Console.WriteLine("""
        BRQOSH Activation Code Generator (vendor only)
        gen-keys <outputDir>
            ينشئ activation-private.pem (احفظه خارج أيّ مستودع) ويطبع المفتاحَ العامّ للصقه في
            src/Brqosh.Infrastructure/Licensing/EmbeddedActivationKeys.PublicKeyPem ثمّ يُعاد البناء.
        issue <private.pem> <requestCode> <standard|professional> <companies> <expiry>
            expiry: yyyy-MM-dd  |  perpetual  |  trial:<days>
            يطبع رمزَ التفعيل (BRQA1....). لكلّ رمزٍ nonce جديد — فلا يُعاد استعمالُه.
        """);
    return 1;
}

static int GenKeys(string[] args)
{
    if (args.Length < 2) return Usage();
    var dir = Path.GetFullPath(args[1]);
    var path = Path.Combine(dir, "activation-private.pem");
    if (File.Exists(path))
    {
        Console.Error.WriteLine($"لا يُستبدل مفتاحٌ قائم: {path}");
        return 2;
    }
    if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, "Brqosh.sln")))
    {
        Console.Error.WriteLine("لا يُكتب المفتاحُ الخاصّ داخل مستودع.");
        return 2;
    }
    Directory.CreateDirectory(dir);
    using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    File.WriteAllText(path, key.ExportPkcs8PrivateKeyPem());
    Console.WriteLine($"المفتاح الخاص: {path}");
    Console.WriteLine("المفتاح العام (الصقه في EmbeddedActivationKeys.PublicKeyPem):");
    Console.WriteLine(key.ExportSubjectPublicKeyInfoPem());
    return 0;
}

static int Issue(string[] args)
{
    if (args.Length < 6) return Usage();
    if (!RequestCode.TryDecode(args[2], out var device))
    {
        Console.Error.WriteLine("رمزُ الطلب غير صحيح — تحقّق من نسخه.");
        return 2;
    }
    var edition = args[3].Trim().ToLowerInvariant();
    if (edition is not ("standard" or "professional"))
    {
        Console.Error.WriteLine("الفئة: standard أو professional.");
        return 2;
    }
    if (!int.TryParse(args[4], out var companies) || companies < 1)
    {
        Console.Error.WriteLine("حدُّ المنشآت عددٌ ≥ 1.");
        return 2;
    }
    var now = DateTime.UtcNow;
    DateTime? expires;
    var trial = false;
    var e = args[5].Trim().ToLowerInvariant();
    if (e == "perpetual") expires = null;
    else if (e.StartsWith("trial:") && int.TryParse(e[6..], out var days) && days > 0)
    {
        expires = now.AddDays(days);
        trial = true;
    }
    else if (DateOnly.TryParse(e, out var date)) expires = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    else
    {
        Console.Error.WriteLine("الانتهاء: yyyy-MM-dd أو perpetual أو trial:<days>.");
        return 2;
    }

    var payload = new ActivationPayload(1, device, edition, companies, expires, trial,
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(), now);
    Console.WriteLine(ActivationCodec.Sign(payload, File.ReadAllText(args[1])));
    return 0;
}
