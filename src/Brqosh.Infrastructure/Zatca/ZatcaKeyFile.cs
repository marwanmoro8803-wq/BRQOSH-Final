using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Brqosh.Infrastructure.Zatca;

/// <summary>
/// (جاهزيّةُ الإطلاق · <c>security-licensing-tenancy#3</c> · قرارُ المالك 2026-10-18) <b>مفتاحُ ختم الفواتير مشفَّرٌ للجهاز.</b>
///
/// <para><b>ما كان:</b> المفتاحُ الخاصّ يُكتب PEM نصّاً صريحاً في مجلّد الشهادات — فمن نسخ الملفَّ (نسخةٌ احتياطيّةٌ للمجلّد، أو قرصٌ
/// مشترك، أو برمجيّةٌ تقرأ ملفّاتِ المستخدم) ختم فواتيرَ باسم المنشأة لدى الهيئة. واختار المالكُ تشفيرَه بـDPAPI <b>للجهاز</b>
/// (<see cref="DataProtectionScope.LocalMachine"/>): يقرؤه أيُّ مستخدمٍ على هذا الجهاز فيعمل الختمُ لكلّ محاسب، <b>ولا يُقرأ
/// منسوخاً على جهازٍ آخر</b>. والثمنُ المقبول: استعادةُ النسخة على جهازٍ جديدٍ تحتاج إعادةَ تسجيل الجهاز لدى الهيئة.</para>
///
/// <para><b>والملفُّ القديمُ الصريحُ يُقرأ ثمّ يُشفَّر في مكانه</b> عند أوّل قراءة — فلا تبقى تركيبةٌ قائمةٌ على النصّ الصريح.</para>
/// </summary>
public static class ZatcaKeyFile
{
    /// <summary>رأسُ الملفّ المشفَّر — يميّزه عن PEM الصريح القديم.</summary>
    public const string Header = "BRQOSH-DPAPI-MACHINE-1";

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BRQOSH ZATCA signing key");

    /// <summary>
    /// يكتب المفتاحَ مشفَّراً للجهاز — ولا يمسّ القرصَ نصٌّ صريحٌ قطّ. <b>والكتابةُ ذرّيّة</b> (ملفٌّ جانبيٌّ ثمّ نقلٌ فوقه):
    /// تشفيرُ الملفّ القديم في مكانه يكتب فوق المفتاح الوحيد، وانقطاعٌ في منتصف كتابةٍ مباشرةٍ يتركه مبتوراً فيلزم إعادةُ التسجيل.
    /// </summary>
    public static void Write(string path, string privateKeyPem)
    {
        var sealedBytes = Protect(Encoding.UTF8.GetBytes(privateKeyPem));
        var staging = path + ".sealing";
        File.WriteAllText(staging, Header + "\n" + Convert.ToBase64String(sealedBytes) + "\n");
        File.Move(staging, path, overwrite: true);
    }

    /// <summary>
    /// يقرأ PEM المفتاح إلى الذاكرة — ويشفّر الملفَّ القديمَ الصريحَ في مكانه إن وجده.
    /// </summary>
    public static string ReadPem(string path)
    {
        var text = File.ReadAllText(path);
        if (!text.StartsWith(Header, StringComparison.Ordinal))
        {
            Write(path, text);
            return text;
        }

        var payload = text[Header.Length..].Trim();
        try
        {
            return Encoding.UTF8.GetString(Unprotect(Convert.FromBase64String(payload)));
        }
        catch (CryptographicException ex)
        {
            throw new ZatcaSigningException(
                "مفتاح الختم مشفّر لجهاز آخر — أعد تسجيل هذا الجهاز لدى الهيئة (شهادة جديدة).", ex);
        }
    }

    /// <summary>هل الملفُّ مشفَّرٌ بهذه الصيغة؟ — للحرّاس والاختبار.</summary>
    public static bool IsSealed(string path) =>
        File.Exists(path) && File.ReadAllText(path).StartsWith(Header, StringComparison.Ordinal);

    private static byte[] Protect(byte[] plain)
    {
        EnsureWindows();
        return ProtectWindows(plain);
    }

    private static byte[] Unprotect(byte[] sealedBytes)
    {
        EnsureWindows();
        return UnprotectWindows(sealedBytes);
    }

    [SupportedOSPlatform("windows")]
    private static byte[] ProtectWindows(byte[] plain) =>
        ProtectedData.Protect(plain, Entropy, DataProtectionScope.LocalMachine);

    [SupportedOSPlatform("windows")]
    private static byte[] UnprotectWindows(byte[] sealedBytes) =>
        ProtectedData.Unprotect(sealedBytes, Entropy, DataProtectionScope.LocalMachine);

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("تشفير مفتاح الختم يتطلب ويندوز (DPAPI).");
    }
}
