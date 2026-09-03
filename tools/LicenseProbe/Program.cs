// مسبار الترخيص على جهاز عميلٍ نظيف (CEEF-236 · ت-١/ت-٢)
//
// يحاكي ما يجده العميلُ بعد المُنصِّب مباشرةً: جذرُ بياناتٍ فارغ، لا متغيّرَ بيئةٍ لمفتاحٍ عامّ،
// ووضعُ الإنتاج. ثمّ يقيس أربعةَ أشياء — وكان اثنان منها معكوسَين قبل الإصلاح:
//   [1] هل يملك التركيبُ مفتاحَ ثقة؟                          (كان: لا  → كلُّ ترخيصٍ يُرفض)
//   [2] بلا ترخيصٍ مخزَّن: هل تُطلب نافذةُ التفعيل؟             (نعم — وهذا صحيح)
//   [3] مهاجمٌ يستبدل ملفَّ الثقة في %APPDATA% ويوقّع لنفسه   (كان: يُقبل → يُرفض)
//   [4] المسارُ نفسُه عبر BRQOSH_LICENSE_PUBLIC_KEY_PEM        (كان: يُقبل → يُرفض)
//   [5] اختياريّ: --private <license-private.pem> يوقّع ترخيصاً حقيقياً ويتوقّع قبولَه.
//
// آمن: يعمل على جذرٍ مؤقّتٍ يُحذف بعده، ولا يمسّ %APPDATA%\BRQOSH الحقيقيّ.
// التشغيل:  dotnet run --project tools/LicenseProbe [-- --private <path>]

using System.Security.Cryptography;
using Brqosh.Application.Setup;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Persistence;

var privateKeyPath = Array.IndexOf(args, "--private") is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : null;

var root = Path.Combine(Path.GetTempPath(), "brqosh-clean-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(root);
Environment.SetEnvironmentVariable(BrqoshDataRoot.OverrideEnvironmentVariable, root);
Environment.SetEnvironmentVariable("BRQOSH_LICENSE_PUBLIC_KEY_PEM", null);
Environment.SetEnvironmentVariable("BRQOSH_UITEST", null);
AppEnvironmentConfig.ForceForTests(AppEnvironment.Production);

var failures = 0;
void Check(string label, bool actual, bool expected, string? note = null)
{
    var ok = actual == expected;
    if (!ok) failures++;
    Console.WriteLine($"[{(ok ? "OK" : "FAIL")}] {label} = {actual}{(ok ? "" : $"  (المتوقّع {expected})")}{(note is null ? "" : "  — " + note)}");
}

Console.WriteLine($"DataRoot     : {root}");
Console.WriteLine($"IsProduction : {AppEnvironmentConfig.IsProduction}");
Console.WriteLine($"Fingerprint  : {RsaLicenseValidator.TrustAnchorFingerprint() ?? "(none)"}");
Console.WriteLine();

const string company = "منشأة العميل";
var machineId = LicenseService.MachineId();

// [1] مرساةُ الثقة على جهازٍ نظيف.
Check("[1] HasTrustAnchor على جذرٍ فارغٍ بلا متغيّر", RsaLicenseValidator.HasTrustAnchor(), expected: true,
    "كان False: لا مفتاحَ يُشحَن فيُرفض كلُّ ترخيص");

// [2] بلا ترخيصٍ مخزَّن تُطلب نافذةُ التفعيل — سلوكٌ صحيح.
Check("[2] ValidateForCompany بلا ترخيصٍ مخزَّن", LicenseStore.ValidateForCompany(company, DateTime.UtcNow), expected: false,
    "تفتح نافذةُ التفعيل — صحيح");

// [3] مهاجمٌ يستبدل ملفَّ الثقة ويوقّع لنفسه.
using var attacker = RSA.Create(2048);
var forged = RsaLicenseValidator.CreateSignedLicenseJson(
    new LicensePayload(company, machineId, "Enterprise", DateTime.UtcNow.AddYears(99), ["all"]),
    attacker.ExportPkcs8PrivateKeyPem());
File.WriteAllText(Path.Combine(root, "license-public.pem"), attacker.ExportSubjectPublicKeyInfoPem());
var v3 = RsaLicenseValidator.Validate(company, machineId, forged, out _);
Check("[3] ترخيصٌ وقّعه العميل بمفتاحه بعد استبدال ملفّ الثقة", v3 == LicenseVerdict.Valid, expected: false,
    $"الحكم: {v3} — كان يُقبل بـTier=Enterprise حتى 2125");

// [4] المسارُ نفسُه عبر متغيّرِ بيئةٍ بنطاق المستخدم.
File.Delete(Path.Combine(root, "license-public.pem"));
Environment.SetEnvironmentVariable("BRQOSH_LICENSE_PUBLIC_KEY_PEM", attacker.ExportSubjectPublicKeyInfoPem());
var v4 = RsaLicenseValidator.Validate(company, machineId, forged, out _);
Check("[4] المسارُ نفسُه عبر BRQOSH_LICENSE_PUBLIC_KEY_PEM", v4 == LicenseVerdict.Valid, expected: false, $"الحكم: {v4}");
Environment.SetEnvironmentVariable("BRQOSH_LICENSE_PUBLIC_KEY_PEM", null);

// [5] ترخيصُ المورِّد الحقيقيّ — إن أُعطي المفتاحُ الخاصّ.
if (privateKeyPath is not null)
{
    var genuine = RsaLicenseValidator.CreateSignedLicenseJson(
        new LicensePayload(company, machineId, "standard", DateTime.UtcNow.AddDays(365), ["core"]),
        File.ReadAllText(privateKeyPath));
    var v5 = RsaLicenseValidator.Validate(company, machineId, genuine, out var payload);
    Check("[5] ترخيصٌ موقَّعٌ بمفتاح المورِّد الخاصّ", v5 == LicenseVerdict.Valid, expected: true,
        v5 == LicenseVerdict.Valid ? $"Tier={payload!.Tier} · ينتهي {payload.ExpiresUtc:yyyy-MM-dd}" : $"الحكم: {v5}");
}
else
{
    Console.WriteLine("[..] [5] لم يُمرَّر --private <license-private.pem> — تُخطّى مطابقةُ المفتاح الخاصّ الحقيقيّ.");
}

Directory.Delete(root, recursive: true);
Console.WriteLine();
Console.WriteLine(failures == 0
    ? "=== PASS: مرساةُ الثقة مُضمَّنة، ولا تُستبدل في الإنتاج ==="
    : $"=== FAIL: {failures} فحصاً خالف المتوقّع ===");
return failures == 0 ? 0 : 1;
