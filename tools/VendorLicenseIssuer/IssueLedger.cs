using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Brqosh.VendorIssuer;

/// <summary>
/// <b>سجلُّ ما أُصدر — بيانات وصفٍ لا تراخيص.</b>
///
/// <para>يُكتب سطرٌ لكلّ إصدار: لمن، ولأيّ جهاز، ومتى، وببصمةِ أيّ مفتاح. <b>ولا يُكتب الغلافُ
/// الموقَّعُ نفسُه</b> — بل بصمتُه فقط. فملفٌّ يجمع تراخيصَ العملاء كلَّها في مكانٍ واحدٍ على
/// جهاز المورِّد يصير هدفاً: كلُّ سطرٍ فيه اعتمادٌ صالحٌ يعمل على جهاز صاحبه.</para>
///
/// <para>والبصمةُ تكفي لما يُراد من السجلّ فعلاً: إثباتُ أنّ ترخيصاً بعينه صدر من هنا،
/// ومعرفةُ ما انتهت مدّتُه.</para>
/// </summary>
public static class IssueLedger
{
    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BRQOSH", "vendor-signing", "issued-licenses.jsonl");

    /// <summary>يُلحق سطراً. يُعيد رسالةَ الخطأ عند الإخفاق — <b>ولا يُسقط الإصدار</b>.</summary>
    public static string? TryAppend(LicensePayload payload, string envelopeJson, string keyFingerprint)
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(Path)!;
            Directory.CreateDirectory(directory);

            var row = JsonSerializer.Serialize(new
            {
                issuedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                company = payload.CompanyName,
                machineId = payload.MachineId,
                tier = payload.Tier,
                expiresUtc = payload.ExpiresUtc.ToString("o", CultureInfo.InvariantCulture),
                keyFingerprint,
                envelopeSha256 = LicenseCore.Sha256Hex(envelopeJson),
            });

            File.AppendAllText(Path, row + Environment.NewLine, new UTF8Encoding(false));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return ex.Message;
        }
    }
}
