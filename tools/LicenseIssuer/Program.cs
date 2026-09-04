using System.Security.Cryptography;
using System.Text;
using Brqosh.Infrastructure.Licensing;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

return args[0] switch
{
    "gen-keys" => GenKeys(args),
    "sign" => SignLicense(args),
    _ => PrintUsage(),
};

static int PrintUsage()
{
    Console.WriteLine("""
        BRQOSH License Issuer
        ---------------------
        gen-keys [outputDir]     — إنشاء زوج مفاتيح RSA (خاص + عام)
                                   ⚠ المفتاح العام لا يعمل إلا بلصقه في EmbeddedLicenseKeys
                                     وإعادة بناء المنتج — ويُبطل كلَّ ترخيصٍ صادرٍ قبله.
        sign <company> <machineId> <privateKey.pem> [days]
                                   اسمُ المنشأة يجب أن يطابق ما في معالج التهيئة حرفاً بحرف.
        """);
    return 1;
}

static int GenKeys(string[] args)
{
    var dir = args.Length > 1 ? args[1] : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BRQOSH");
    Directory.CreateDirectory(dir);

    using var rsa = RSA.Create(2048);
    var privatePem = rsa.ExportPkcs8PrivateKeyPem();
    var publicPem = rsa.ExportSubjectPublicKeyInfoPem();

    var privatePath = Path.Combine(dir, "license-private.pem");
    var publicPath = Path.Combine(dir, "license-public.pem");
    File.WriteAllText(privatePath, privatePem);
    File.WriteAllText(publicPath, publicPem);

    Console.WriteLine($"Private: {privatePath}");
    Console.WriteLine($"Public:  {publicPath}");
    Console.WriteLine("احفظ المفتاح الخاص خارج التطبيق — لا تُضمّنه في المستودع.");

    // (CEEF-236 · م-١٦) ‏**وكتابةُ الملفّ لم تعد تُنشئ مرساةَ ثقة.**
    //
    // ‏قبل CEEF-236 كان `%APPDATA%\BRQOSH\license-public.pem` هو آليّةَ التوزيع كلَّها. وبعده
    // ‏**لا يُقرأ هذا الملفُّ في الإنتاج البتّة** — المرساةُ الوحيدة ثابتٌ مُجمَّع. فمن ولّد زوجاً
    // ‏جديداً ووقّع به ظنّاً أنّ الملفَّ يكفي، رُفض كلُّ ترخيصٍ أصدره على كلّ جهاز، **والرسالةُ
    // ‏تلوم ملفَّ الترخيص لا البناءَ المخالف**. فيُقال ذلك هنا، حيث يقع الخطأ.
    var fingerprint = Convert.ToHexString(
        SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));
    Console.WriteLine();
    Console.WriteLine($"بصمة المفتاح العام (SHA-256 · SPKI): {fingerprint}");
    Console.WriteLine();
    Console.WriteLine("⚠ هذا المفتاح لا يصير مرساةَ ثقة إلا بلصقه في الثابت:");
    Console.WriteLine("    src/Brqosh.Infrastructure/Licensing/EmbeddedLicenseKeys.PublicKeyPem");
    Console.WriteLine("  ثمّ إعادة بناء المنتج وشحنه. ووضعُه في %APPDATA% لا أثر له في الإنتاج.");
    Console.WriteLine("⚠ وتبديلُ الثابت **يُبطل كلَّ ترخيصٍ صادرٍ بالمفتاح السابق** — فهو قرارُ إصدار.");
    Console.WriteLine("  وحدّث معه بصمةَ الاختبار LicenseTrustAnchorTests و docs/OPEN_ITEMS.md §3.");
    Console.WriteLine();
    var fence = new string('"', 3);
    Console.WriteLine("الثابت جاهزاً للّصق:");
    Console.WriteLine("    public const string PublicKeyPem = " + fence);
    foreach (var line in publicPem.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'))
        Console.WriteLine("        " + line);
    Console.WriteLine("        " + fence + ";");
    return 0;
}

static int SignLicense(string[] args)
{
    if (args.Length < 4)
        return PrintUsage();

    var company = args[1];
    var machineId = args[2];
    var privateKeyPath = args[3];
    var days = args.Length > 4 && int.TryParse(args[4], out var d) ? d : 365;

    var privatePem = File.ReadAllText(privateKeyPath);
    var payload = new LicensePayload(
        company, machineId, "standard",
        DateTime.UtcNow.AddDays(days),
        ["core", "zatca", "construction"]);

    var json = RsaLicenseValidator.CreateSignedLicenseJson(payload, privatePem);
    Console.WriteLine(json);
    return 0;
}
