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
        sign <company> <machineId> <privateKey.pem> [days]
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
