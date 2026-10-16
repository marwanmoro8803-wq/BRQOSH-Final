using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Operations;
using Brqosh.Infrastructure.Persistence;

// (ح-١٤٠ · installer-deployment-ops#12) ‏ناشرُ قناة التحديث — **البيانُ لا يُكتب بيد**.
// ‏المنطقُ كلُّه في `UpdatePublisher` (Infrastructure) حيث يُختبر؛ وهذا غلافُ سطر أوامر.

if (args.Length == 0)
    return PrintUsage();

try
{
    switch (args[0])
    {
        case "gen-keys":
        {
            var dataRoot = BrqoshDataRoot.Path();
            var dir = args.Length > 1 ? args[1] : Path.Combine(dataRoot, "vendor-signing");
            var pair = UpdatePublisher.GenerateKeys(dir, dataRoot);
            Console.WriteLine($"Private: {pair.PrivateKeyPath}");
            Console.WriteLine($"Public:  {pair.PublicKeyPath}");
            Console.WriteLine($"بصمة المفتاح العام (SHA-256 · SPKI): {pair.Fingerprint}");
            Console.WriteLine("⚠ لا يصير مرساةَ ثقةٍ إلا بلصقه في EmbeddedUpdateKeys.PublicKeyPem ثمّ إعادة البناء والشحن.");
            Console.WriteLine("⚠ احفظ المفتاحَ الخاصّ بعيداً عن جهاز العمل اليوميّ — يُخرج يومَ الإصدار وحدَه.");
            return 0;
        }

        case "publish" when args.Length >= 5:
        {
            string? notes = null;
            var date = DateOnly.FromDateTime(DateTime.Today);
            for (var i = 5; i < args.Length - 1; i += 2)
            {
                switch (args[i])
                {
                    case "--notes": notes = args[i + 1]; break;
                    case "--date": date = DateOnly.ParseExact(args[i + 1], "yyyy-MM-dd"); break;
                    default: return PrintUsage();
                }
            }

            var manifest = await UpdatePublisher.PublishAsync(
                args[1], args[2], date, notes, File.ReadAllText(args[3]), args[4]);
            Console.WriteLine($"نُشر: {manifest}");
            Console.WriteLine($"مرساةُ هذا البناء: {UpdateCheckService.TrustAnchorFingerprint()}");
            return 0;
        }

        case "anchor":
            Console.WriteLine(UpdateCheckService.TrustAnchorFingerprint());
            return 0;

        default:
            return PrintUsage();
    }
}
catch (AccountingException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

static int PrintUsage()
{
    Console.WriteLine("""
        BRQOSH Update Publisher
        -----------------------
        gen-keys [outputDir]       — زوجُ مفاتيح التحديث (غيرُ مفتاح الترخيص)
                                     الافتراضيّ: %APPDATA%\BRQOSH\vendor-signing
                                     ⚠ لا يستبدل ملفّاً قائماً، ويرفض جذرَ بيانات المنتج.
        publish <package.msi> <version> <update-private.pem> <channelDir> [--notes "…"] [--date yyyy-MM-dd]
                                     يحسب البصمةَ والحجم، يوقّع، يكتب manifest.json ويتحقّق منه؛
                                     ويرفض مفتاحاً لا يطابق مرساةَ التحديث في هذا البناء.
        anchor                     — بصمةُ مرساة التحديث التي يثق بها هذا البناء.
        """);
    return 1;
}
