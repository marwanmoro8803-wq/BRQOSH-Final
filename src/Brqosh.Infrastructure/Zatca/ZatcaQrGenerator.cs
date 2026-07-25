using System.Security.Cryptography;
using System.Text;

namespace Brqosh.Infrastructure.Zatca;

/// <summary>
/// مولّد QR بصيغة TLV — متوافق مع ZATCA Phase 2.
/// Phase 1: حقول 1-5 (اسم البائع، الرقم الضريبي، الطابع الزمني، الإجمالي، الضريبة).
/// Phase 2: حقول 6-7 إضافية (هاش الفاتورة، توقيع ECDSA).
/// المرجع: ZATCA e-Invoicing Technical Specifications v2.3
/// </summary>
public static class ZatcaQrGenerator
{
    /// <summary>
    /// توليد TLV Base64 للمرحلة الثانية — يشمل هاش الفاتورة وتوقيع ECDSA.
    /// </summary>
    public static string GenerateTlvBase64Phase2(
        string sellerName,
        string vatNumber,
        DateTime timestampUtc,
        decimal invoiceTotal,
        decimal vatTotal,
        string invoiceHashBase64,
        string ecdsaSignatureBase64)
    {
        var ts = timestampUtc.ToString(
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            System.Globalization.CultureInfo.InvariantCulture);

        var items = new (byte Tag, byte[] Value)[]
        {
            (1, Encoding.UTF8.GetBytes(sellerName)),
            (2, Encoding.UTF8.GetBytes(vatNumber)),
            (3, Encoding.UTF8.GetBytes(ts)),
            (4, Encoding.UTF8.GetBytes(invoiceTotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))),
            (5, Encoding.UTF8.GetBytes(vatTotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))),
            (6, Convert.FromBase64String(invoiceHashBase64)),   // Phase 2: هاش الفاتورة (bytes خام)
            (7, Convert.FromBase64String(ecdsaSignatureBase64)), // Phase 2: توقيع ECDSA (bytes خام)
        };

        return EncodeTlv(items);
    }

    /// <summary>اسم قديم — يعادل Phase 1 (قبل التوقيع أو محاكاة).</summary>
    public static string GenerateTlvBase64(
        string sellerName,
        string vatNumber,
        DateTime timestampUtc,
        decimal invoiceTotal,
        decimal vatTotal) =>
        GenerateTlvBase64Phase1(sellerName, vatNumber, timestampUtc, invoiceTotal, vatTotal);

    /// <summary>
    /// توليد TLV Base64 للمرحلة الأولى فقط — للاستخدام قبل التوقيع أو في بيئة المحاكاة.
    /// </summary>
    public static string GenerateTlvBase64Phase1(
        string sellerName,
        string vatNumber,
        DateTime timestampUtc,
        decimal invoiceTotal,
        decimal vatTotal)
    {
        var ts = timestampUtc.ToString(
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            System.Globalization.CultureInfo.InvariantCulture);

        var items = new (byte Tag, byte[] Value)[]
        {
            (1, Encoding.UTF8.GetBytes(sellerName)),
            (2, Encoding.UTF8.GetBytes(vatNumber)),
            (3, Encoding.UTF8.GetBytes(ts)),
            (4, Encoding.UTF8.GetBytes(invoiceTotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))),
            (5, Encoding.UTF8.GetBytes(vatTotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))),
        };

        return EncodeTlv(items);
    }

    private static string EncodeTlv(IEnumerable<(byte Tag, byte[] Value)> items)
    {
        using var payload = new MemoryStream();
        foreach (var (tag, data) in items)
        {
            if (data.Length > 65535)
                throw new ArgumentException($"قيمة TLV للوسم {tag} تتجاوز الحد الأقصى.");

            payload.WriteByte(tag);

            // TLV length encoding: إذا > 127 نستخدم 2 bytes
            if (data.Length <= 127)
            {
                payload.WriteByte((byte)data.Length);
            }
            else
            {
                payload.WriteByte(0x81);
                payload.WriteByte((byte)data.Length);
            }

            payload.Write(data);
        }
        return Convert.ToBase64String(payload.ToArray());
    }
}

/// <summary>هاش الفواتير — SHA-256 Base64؛ PIH الأول = هاش سلسلة "0".</summary>
public static class ZatcaHashHelper
{
    public static string Sha256Base64(byte[] data) =>
        Convert.ToBase64String(SHA256.HashData(data));

    public static string Sha256Base64(string text) =>
        Sha256Base64(Encoding.UTF8.GetBytes(text));

    /// <summary>
    /// هاش الصفر للفاتورة الأولى — وفق ZATCA: SHA-256("0") مُشفَّر Base64.
    /// </summary>
    public static string ZeroHash() =>
        Sha256Base64("0"u8.ToArray());
}
