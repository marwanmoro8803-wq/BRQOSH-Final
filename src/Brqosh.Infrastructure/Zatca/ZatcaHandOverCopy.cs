using System.Xml;
using System.Xml.Linq;
using Brqosh.Domain.Zatca;

namespace Brqosh.Infrastructure.Zatca;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٥٣ · <c>tax-zatca#9</c>) <b>ما يُسلَّم للمشتري: ما اعتمدته الهيئة —
/// لا نسختُنا.</b>
///
/// <para>في التخليص (B2B) تُعيد الهيئةُ الفاتورةَ مختومةً، <b>وهي الوثيقةُ النظاميّة</b>
/// (<see cref="ZatcaEInvoiceDocument.ClearedXmlFilePath"/>). وكانت تُحفظ ولا يقرؤها شيء:
/// <b>المطبوعُ يحمل رمزَ QR الذي ولّدناه نحن</b>، و«عرض XML» يفتح نسختَنا الموقَّعة —
/// <b>وفاتورةٌ لم تُعتمد أو رُفضت تُطبع كالمعتمدة</b>، برمزها وعنوانها.</para>
///
/// <para><b>والقارئُ هنا واحد</b> — كلُّ طباعةٍ وكلُّ عرضٍ يمرّ منه، وحارسٌ في الاختبارات
/// يعدّ القراءاتِ المباشرةَ لرمز المستند خارجه. فطباعةٌ سادسةٌ تُكتب غداً لا تعود إلى رمزنا
/// صامتةً.</para>
/// </summary>
public static class ZatcaHandOverCopy
{
    private static readonly XNamespace Cac =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    /// <summary>ما يُطبع من هويّة المستند لدى الهيئة: رمزٌ، أو تنبيهٌ يقول لمَ لا رمز.</summary>
    public readonly record struct PrintedMark(string? QrTlvBase64, string? ClearanceNotice);

    /// <summary>
    /// <b>الرمزُ الذي يُطبع</b>:
    /// <list type="bullet">
    /// <item>لا مستندَ إلكترونيّاً ⟵ ما يُعطيه <paramref name="withoutZatca"/> (الطورُ الأوّل).</item>
    /// <item>مبسّطةٌ تُبلَّغ ⟵ رمزُنا: لا تُعيد الهيئةُ فيها مستنداً، والإبلاغُ بعد التسليم.</item>
    /// <item>محاكاةٌ محلّيّة ⟵ رمزُنا: لم يصل الهيئةَ شيء، والسطرُ المطبوعُ يقول «محاكاة» (CEEF-229).</item>
    /// <item>مُعتمَدة ⟵ <b>رمزُ الهيئة من نسختها</b>؛ وإن لم تُقرأ فلا رمزَ وتنبيهٌ يُسمّي المسار.</item>
    /// <item>ما عدا ذلك في التخليص (تنتظر · مرفوضة) ⟵ <b>لا رمز، وتنبيهٌ أنّها ليست فاتورةً ضريبيّةً نافذة</b>.</item>
    /// </list>
    ///
    /// <para><b>ولا تُمنع الطباعة</b>: النسخةُ الداخليّةُ ومراجعةُ ما يُعاد إرسالُه حاجتان مشروعتان.
    /// <b>والممنوعُ ورقةٌ لا يُميّزها المشتري من الفاتورة النافذة</b> — والتنبيهُ والرمزُ الغائبُ يُميّزانها.</para>
    /// </summary>
    public static PrintedMark MarkFor(ZatcaEInvoiceDocument? document, Func<string?> withoutZatca)
    {
        if (document is null)
            return new PrintedMark(withoutZatca(), null);

        if (document.TransmissionMode != ZatcaTransmissionMode.Clearance
            || document.ComplianceStatus == ZatcaComplianceStatus.SimulatedLocally)
            return new PrintedMark(document.QrTlvBase64, null);

        if (document.ComplianceStatus != ZatcaComplianceStatus.Cleared)
            return new PrintedMark(null, document.ComplianceStatus == ZatcaComplianceStatus.Rejected
                ? "رفضت الهيئةُ هذا المستند — ليس فاتورةً ضريبيّةً نافذة، ولا يُسلَّم للمشتري."
                : "لم تعتمد الهيئةُ هذا المستندَ بعد — ليس فاتورةً ضريبيّةً نافذة، ولا يُسلَّم للمشتري قبل اعتماده.");

        var (qr, why) = AuthorityQr(document.ClearedXmlFilePath);
        return qr is not null
            ? new PrintedMark(qr, null)
            : new PrintedMark(null,
                $"اعتمدت الهيئةُ هذا المستند ونسختُه المعتمدةُ {why} — استعِدها قبل تسليمه للمشتري.");
    }

    /// <summary>
    /// <b>الملفُّ الذي يُعرض ويُنسخ للمشتري</b>: المعتمدُ متى اعتُمد المستند — <b>وإن ضاع
    /// فلا شيء</b>، لا نسختُنا مكانَه. وقبل الاعتماد نسختُنا الموقَّعةُ ثمّ غيرُ الموقَّعة.
    /// </summary>
    public static string? XmlPathFor(ZatcaEInvoiceDocument document)
    {
        if (document.ComplianceStatus == ZatcaComplianceStatus.Cleared)
            return File.Exists(document.ClearedXmlFilePath) ? document.ClearedXmlFilePath : null;
        return new[] { document.SignedXmlFilePath, document.XmlFilePath }.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// رمزُ QR من النسخة المعتمدة — <c>cac:AdditionalDocumentReference</c> معرّفُه <c>QR</c>،
    /// كما يكتبه <see cref="ZatcaXmlSigner.ReplaceQrInSignedXml"/>. <b>وحين لا يُقرأ يُقال لماذا</b>
    /// ليُطبع السببُ لا ليُبتلع.
    /// </summary>
    internal static (string? Qr, string Why) AuthorityQr(string? clearedXmlPath)
    {
        if (string.IsNullOrWhiteSpace(clearedXmlPath))
            return (null, "لم يُحفظ مسارُها");
        if (!File.Exists(clearedXmlPath))
            return (null, $"غيرُ موجودةٍ على القرص ({clearedXmlPath})");

        XDocument xml;
        try
        {
            xml = XDocument.Load(clearedXmlPath);
        }
        catch (XmlException e)
        {
            return (null, $"لا تُقرأ ({clearedXmlPath}: {e.Message})");
        }

        var qr = xml.Descendants(Cac + "AdditionalDocumentReference")
            .FirstOrDefault(r => r.Element(Cbc + "ID")?.Value == "QR")?
            .Element(Cac + "Attachment")?
            .Element(Cbc + "EmbeddedDocumentBinaryObject")?.Value;
        return string.IsNullOrWhiteSpace(qr)
            ? (null, $"لا تحمل رمزَ QR ({clearedXmlPath})")
            : (qr.Trim(), "");
    }
}
