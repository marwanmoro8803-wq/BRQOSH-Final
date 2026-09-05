using System.Text;
using System.Text.Json;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Zatca;

namespace Brqosh.Application.Tests.Zatca;

/// <summary>
/// (CEEF-237 · ز-٢) <b>حكمُ الهيئة يُقرأ من جسم الردّ — لا يُفترض من رمز HTTP.</b>
///
/// <para><b>ما كان:</b> أيُّ استجابةٍ ناجحة (2xx) كانت تُعلّم المستند <c>Cleared</c> أو
/// <c>Reported</c>. <b>والهيئةُ تردّ 200 ومعها <c>clearanceStatus: NOT_CLEARED</c></b> حين
/// تقبل الطلبَ وترفض المحتوى.</para>
///
/// <para><b>وأثرُه:</b> فاتورةٌ مرفوضةٌ تُخزَّن «مخلَّصة»، ويُسلَّم المشتري مستنداً لا سندَ
/// له، <b>ولا يظهر الخللُ إلا في مطابقةِ الهيئة بعد شهور</b> — وقتَها تكون الفواتيرُ بالمئات.</para>
///
/// <para><b>وصمتُ الهيئة لا يُقرأ قبولاً:</b> ردٌّ بلا حقلِ حالةٍ أصلاً يُعامَل إخفاقاً
/// مُعلَناً، لا نجاحاً مفترضاً.</para>
/// </summary>
public class ZatcaAuthorityVerdictTests
{
    private static string Body(object payload) => JsonSerializer.Serialize(payload);

    [Theory]
    [InlineData("CLEARED", true)]
    [InlineData("cleared", true)]
    [InlineData("NOT_CLEARED", false)]
    [InlineData("", false)]
    public void Clearance_IsDecidedByTheAuthoritysOwnStatus(string status, bool expected)
    {
        var verdict = ZatcaSubmissionService.ReadAuthorityVerdict(
            Body(new { clearanceStatus = status }), ZatcaTransmissionMode.Clearance);

        Assert.Equal(expected, verdict.Accepted);
    }

    [Theory]
    [InlineData("REPORTED", true)]
    [InlineData("NOT_REPORTED", false)]
    public void Reporting_IsDecidedByItsOwnStatusField(string status, bool expected)
    {
        var verdict = ZatcaSubmissionService.ReadAuthorityVerdict(
            Body(new { reportingStatus = status }), ZatcaTransmissionMode.Reporting);

        Assert.Equal(expected, verdict.Accepted);
    }

    [Fact]
    public void AClearedStatusOnAReportingCall_IsNotAcceptance()
    {
        // ‏الحقلُ المقروء يتبع نمطَ الإرسال؛ خلطُهما يجعل ردّاً صالحاً لأحدهما يمرّ في الآخر.
        var verdict = ZatcaSubmissionService.ReadAuthorityVerdict(
            Body(new { clearanceStatus = "CLEARED" }), ZatcaTransmissionMode.Reporting);

        Assert.False(verdict.Accepted);
        Assert.Contains("reportingStatus", verdict.Status);
    }

    [Fact]
    public void AMissingStatusField_IsAnAnnouncedFailure_NotSilentSuccess()
    {
        var verdict = ZatcaSubmissionService.ReadAuthorityVerdict(
            Body(new { validationResults = new { status = "PASS" } }), ZatcaTransmissionMode.Clearance);

        Assert.False(verdict.Accepted);
        Assert.Contains("غائب", verdict.Status);
    }

    [Fact]
    public void ABodyThatIsNotJson_IsNotReadAsAcceptance()
    {
        var verdict = ZatcaSubmissionService.ReadAuthorityVerdict(
            "<html>502 Bad Gateway</html>", ZatcaTransmissionMode.Clearance);

        Assert.False(verdict.Accepted);
    }

    [Fact]
    public void TheRejectionReason_CarriesTheAuthoritysOwnMessage()
    {
        var body = Body(new
        {
            clearanceStatus = "NOT_CLEARED",
            errorMessages = new[] { new { code = "BR-KSA-11", message = "رقمُ التسجيل الضريبيّ غير صالح" } },
        });

        var verdict = ZatcaSubmissionService.ReadAuthorityVerdict(body, ZatcaTransmissionMode.Clearance);

        Assert.False(verdict.Accepted);
        Assert.Equal("رقمُ التسجيل الضريبيّ غير صالح", verdict.Reason);
    }

    [Fact]
    public void TheClearedInvoice_IsExtractedFromTheResponse()
    {
        var xml = Encoding.UTF8.GetBytes("<Invoice>مختومة من الهيئة</Invoice>");
        var body = Body(new { clearanceStatus = "CLEARED", clearedInvoice = Convert.ToBase64String(xml) });

        Assert.Equal(xml, ZatcaSubmissionService.ReadClearedInvoice(body));
    }

    [Theory]
    [InlineData("{\"clearanceStatus\":\"CLEARED\"}")]                       // لا حقلَ أصلاً
    [InlineData("{\"clearanceStatus\":\"CLEARED\",\"clearedInvoice\":\"\"}")] // فارغ
    [InlineData("{\"clearanceStatus\":\"CLEARED\",\"clearedInvoice\":\"!!\"}")] // ليس base64
    public void AnAbsentOrCorruptClearedInvoice_ReturnsNothingRatherThanThrowing(string body)
    {
        Assert.Null(ZatcaSubmissionService.ReadClearedInvoice(body));
    }

    [Fact]
    public void AClearedDocumentCannotBeAttachedToAnUnclearedInvoice()
    {
        var doc = ZatcaEInvoiceDocument.Create(
            companyId: 1, documentType: ZatcaDocumentType.SalesInvoice, sourceDocumentId: 1,
            invoiceNumber: "INV-1", invoiceUuid: Guid.NewGuid(), icv: 1,
            previousHash: "cHJldmlvdXM=", invoiceHash: "hash",
            mode: ZatcaTransmissionMode.Clearance, qrTlv: "qr",
            xmlPath: "a.xml", signedXmlPath: "a.signed.xml");

        // ‏لم تُقبل بعد — فلا مستندَ مخلَّصٌ يُرفَق بها.
        Assert.Throws<InvalidOperationException>(() => doc.AttachClearedXml("a.cleared.xml"));

        doc.MarkSubmitted(ZatcaComplianceStatus.Cleared, "{}");
        doc.AttachClearedXml("a.cleared.xml");
        Assert.Equal("a.cleared.xml", doc.ClearedXmlFilePath);
    }
}
