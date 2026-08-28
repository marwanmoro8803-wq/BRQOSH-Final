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
        string ecdsaSignatureBase64,
        string? publicKeyDerBase64 = null)
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

        // (CEEF-229 · Z-9) الوسم 8 — المفتاح العام لختم التشفير.
        //
        // كانت المصفوفة تنتهي عند 7: توقيعٌ ECDSA بلا مفتاحٍ يتحقّق به. فماسحُ «فاتورة» يفكّ
        // سبعة وسوم ولا يستطيع إثبات صحّة الختم، وفحصُ الامتثال على الفاتورة المبسّطة يفشل.
        // والمفتاح يُستخرج من الشهادة محلياً — لا ينتظر شيئاً من الهيئة.
        //
        // (الوسم 9 — توقيع الهيئة على المفتاح العام — يأتي مع CSID الإنتاجيّ في م4.)
        if (!string.IsNullOrWhiteSpace(publicKeyDerBase64))
            items = [.. items, ((byte)8, Convert.FromBase64String(publicKeyDerBase64))];

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

            // ترميز طول TLV (BER):
            // (CEEF-206) الفرع الطويل كان يكتب 0x81 ثم (byte)Length — فأي طول ≥ 256
            // يُقصّ إلى بايت واحد (300 تصير 44) وينتج رمزاً تالفاً بلا أي استثناء.
            // اسم بائع عربي طويل يتجاوز 255 بايتاً في UTF-8 يبلغ هذا الحد فعلاً.
            if (data.Length <= 127)
            {
                payload.WriteByte((byte)data.Length);
            }
            else if (data.Length <= 255)
            {
                payload.WriteByte(0x81);
                payload.WriteByte((byte)data.Length);
            }
            else
            {
                payload.WriteByte(0x82);
                payload.WriteByte((byte)(data.Length >> 8));
                payload.WriteByte((byte)(data.Length & 0xFF));
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
