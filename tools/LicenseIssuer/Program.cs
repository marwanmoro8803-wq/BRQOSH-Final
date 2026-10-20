using System.Security.Cryptography;
using System.Text;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Persistence;

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
                                   الافتراضيّ: %APPDATA%\BRQOSH\vendor-signing
                                   ⚠ المفتاح العام لا يعمل إلا بلصقه في EmbeddedLicenseKeys
                                     وإعادة بناء المنتج — ويُبطل كلَّ ترخيصٍ صادرٍ قبله.
                                   ⚠ لا يستبدل ملفَّ مفتاحٍ قائماً، ويرفض الكتابةَ في جذر
                                     بيانات المنتج (هناك يصير المفتاحُ مرساةَ ثقةٍ بلا قصد).
        sign <company> <machineId> <privateKey.pem> [days] [modules]
                                   اسمُ المنشأة يجب أن يطابق ما في معالج التهيئة حرفاً بحرف.
                                   modules: وحداتٌ مفصولةٌ بفواصل (Sales,Inventory,...) تحصر الترخيص؛
                                     وبدونها لا يحصر شيئاً. والمحاسبةُ العامّة تُمنح دائماً،
                                     والفوترةُ الإلكترونيّة مع المبيعات.
        """);
    return 1;
}

static int GenKeys(string[] args)
{
    // (CEEF-236 · مراجعةٌ لاحقة) ‏**فعلٌ كان يُتلف مفتاحَ التوقيع ويزرع مرساةَ ثقةٍ بلا قصد.**
    //
    // ‏كان الافتراضيُّ **جذرَ بيانات المنتج نفسَه**، فوقع عطلان:
    //
    // ‏① `license-public.pem` في الجذر هو **مرساةُ ثقةٍ بديلةٌ تُقرأ خارج الإنتاج**
    // ‏   (‏`EmbeddedLicenseKeys.ReadDataRootFile`‎). فمن ولّد زوجاً للتجربة استبدل مرساةَ
    // ‏   جهازه بلا أن يدري: صار بناءُ التطويرِ يثق بمفتاحه هو، **فتُقبل عنده تراخيصُ لا يقبلها
    // ‏   أحدٌ غيره** — ويُشحن ظنّاً أنّ المسارَ مقيس.
    //
    // ‏② والأشدّ: `File.WriteAllText` على `license-private.pem` **يستبدل الملفَّ صامتاً**.
    // ‏   ومفتاحُ المورِّد الخاصُّ لا نسخةَ له في المنتج: إتلافُه يعني أنّ كلَّ ترخيصٍ جديدٍ
    // ‏   يحتاج ثابتاً جديداً في المنتج وبناءً جديداً، **وذلك يُبطل كلَّ ترخيصٍ صادرٍ قبله**.
    // ‏   أمرٌ من كلمتين بلا وسائطَ كان يكفي.
    //
    // ‏فصار: الافتراضيُّ مجلّدَ التوقيع لا الجذر، **والجذرُ نفسُه مرفوض**، **ولا يُستبدل ملفٌّ
    // ‏قائمٌ أبداً** (ولا `--force`: خيارٌ يُتلف مفتاحاً لا يُوضع في متناول يدٍ مستعجلة)،
    // ‏والعامُّ يحمل بصمتَه في اسمه فلا يُخلط بملفّ المرساة ولو نُسخ.
    var dataRoot = BrqoshDataRoot.Path();
    var dir = args.Length > 1 ? args[1] : Path.Combine(dataRoot, "vendor-signing");

    var resolved = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir));
    if (string.Equals(resolved, Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot)),
            StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"مرفوض: {resolved} هو جذرُ بيانات المنتج.");
        Console.Error.WriteLine("‏`license-public.pem` هناك يُقرأ كمرساةِ ثقةٍ بديلةٍ خارج الإنتاج، فيثق هذا الجهازُ بمفتاحك أنت.");
        Console.Error.WriteLine($"استعمل مجلّداً آخر — الافتراضيّ: {Path.Combine(dataRoot, "vendor-signing")}");
        return 2;
    }

    Directory.CreateDirectory(resolved);

    using var rsa = RSA.Create(2048);
    var privatePem = rsa.ExportPkcs8PrivateKeyPem();
    var publicPem = rsa.ExportSubjectPublicKeyInfoPem();
    var keyFingerprint = Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));

    var privatePath = Path.Combine(resolved, "license-private.pem");
    var publicPath = Path.Combine(resolved, $"license-public-{keyFingerprint[..16]}.pem");

    foreach (var existing in new[] { privatePath, publicPath })
    {
        if (!File.Exists(existing))
            continue;

        Console.Error.WriteLine($"مرفوض: {existing} موجودٌ سلفاً — ولن يُستبدل.");
        Console.Error.WriteLine("مفتاحُ توقيعٍ يُتلَف لا يُستعاد، وكلُّ ترخيصٍ صادرٍ به يموت معه.");
        Console.Error.WriteLine("انقل القديمَ إلى مكانٍ آمنٍ بيدك ثمّ أعد المحاولة، أو مرّر مجلّداً آخر.");
        return 3;
    }

    File.WriteAllText(privatePath, privatePem);
    File.WriteAllText(publicPath, publicPem);

    // ‏قراءةٌ بعد الكتابة: كتابةٌ ناقصةٌ (قرصٌ ممتلئ، مضادُّ فيروسات) تُكتشف الآن لا بعد
    // ‏شهورٍ حين يُطلب توقيعٌ بمفتاحٍ لا يُستورد.
    try
    {
        using var reread = RSA.Create();
        reread.ImportFromPem(File.ReadAllText(privatePath));
        var probe = reread.SignData("brqosh"u8.ToArray(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        using var publicOnly = RSA.Create();
        publicOnly.ImportFromPem(File.ReadAllText(publicPath));
        if (!publicOnly.VerifyData("brqosh"u8.ToArray(), probe, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            throw new CryptographicException("الملفّان المكتوبان ليسا زوجاً.");
    }
    catch (Exception ex) when (ex is CryptographicException or ArgumentException or IOException)
    {
        Console.Error.WriteLine($"الزوجُ كُتب ولا يُستعمل: {ex.Message}");
        Console.Error.WriteLine($"احذف {privatePath} و{publicPath} وأعد المحاولة.");
        return 4;
    }

    Console.WriteLine($"Private: {privatePath}");
    Console.WriteLine($"Public:  {publicPath}");
    Console.WriteLine("احفظ المفتاح الخاص خارج التطبيق — لا تُضمّنه في المستودع.");

    // (CEEF-236 · م-١٦) ‏**وكتابةُ الملفّ لم تعد تُنشئ مرساةَ ثقة.**
    //
    // ‏قبل CEEF-236 كان `%APPDATA%\BRQOSH\license-public.pem` هو آليّةَ التوزيع كلَّها. وبعده
    // ‏**لا يُقرأ هذا الملفُّ في الإنتاج البتّة** — المرساةُ الوحيدة ثابتٌ مُجمَّع. فمن ولّد زوجاً
    // ‏جديداً ووقّع به ظنّاً أنّ الملفَّ يكفي، رُفض كلُّ ترخيصٍ أصدره على كلّ جهاز، **والرسالةُ
    // ‏تلوم ملفَّ الترخيص لا البناءَ المخالف**. فيُقال ذلك هنا، حيث يقع الخطأ.
    var fingerprint = keyFingerprint;
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

    // ‏(ح-١٦٤) ‏الوحداتُ تُكتب مدخلاتِ `module:` تحت التوقيع؛ واسمٌ لا يُعرف يُرفض هنا — لا يُسقَط صامتاً فيُصدَر ترخيصٌ أضيقُ ممّا بيع.
    string[] features = ["core", "zatca", "construction"];
    if (args.Length > 5 && !string.IsNullOrWhiteSpace(args[5]))
    {
        var modules = new List<Brqosh.Domain.Modules.ModuleKey>();
        foreach (var name in args[5].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<Brqosh.Domain.Modules.ModuleKey>(name, ignoreCase: true, out var key) || !Enum.IsDefined(key))
            {
                Console.Error.WriteLine($"وحدةٌ غيرُ معروفة: «{name}». المعروفة: {string.Join(", ", Enum.GetNames<Brqosh.Domain.Modules.ModuleKey>())}");
                return 2;
            }
            modules.Add(key);
        }
        features = [.. features, .. LicensedModules.ToFeatures(modules)];
    }

    var privatePem = File.ReadAllText(privateKeyPath);
    var payload = new LicensePayload(
        company, machineId, "standard",
        DateTime.UtcNow.AddDays(days),
        features);

    var json = RsaLicenseValidator.CreateSignedLicenseJson(payload, privatePem);
    Console.WriteLine(json);
    return 0;
}
