using Brqosh.Domain.Zatca;

namespace Brqosh.Domain.Tests;

/// <summary>
/// (م-٢ · ن-٠٥) <b>«مخلَّصة» بلا مستندٍ مخلَّصٍ كانت حالةً قابلةً للتخزين — وصارت مستحيلة.</b>
///
/// <para><b>سلسلةُ العطل كما قِيست:</b> <c>SubmitCoreAsync</c> كان يُطفّر الحالةَ إلى
/// <c>Cleared</c> ثمّ يكتب المستندَ على القرص. فرميةُ كتابةٍ تترك الكيانَ مطفَّراً في
/// متعقّب EF؛ ويلتقط <c>ZatcaAutoSubmissionHelper</c> الرميةَ فيستدعي
/// <c>RecordSubmissionFailure</c> ثمّ <c>SaveChangesAsync</c> — <b>على المثيل نفسِه</b>،
/// لأنّ خريطةَ الهويّة تُعيده. فيُحفَظ ثلاثةُ أخطاءٍ معاً: فاتورةٌ «مخلَّصة» بلا مستند،
/// وردُّ الهيئة مدهوسٌ بملاحظتنا، وبابُ الإعادة مُغلقٌ لأنّ <c>Cleared</c> لا يلتقطها
/// إرسالٌ لاحق.</para>
///
/// <para><b>ولماذا لا يكفي إصلاحُ الترتيب:</b> أُصلح — يُكتب الملفُّ ثمّ تُطفَّر الحالة.
/// <b>لكنّ سطرين يُبدَّلان في مراجعةٍ عابرةٍ يُعيدان العطلَ بلا أن يشتكيَ شيء.</b> فالشرطُ
/// في النطاق يجعل الحالةَ الكاذبةَ <b>غيرَ قابلةٍ للتعبير</b>، لا مجرّدَ غيرِ مقصودة.</para>
/// </summary>
public class ClearedRequiresAClearedDocumentTests
{
    private static ZatcaEInvoiceDocument NewClearanceDoc() => Doc(ZatcaTransmissionMode.Clearance);

    private static ZatcaEInvoiceDocument Doc(ZatcaTransmissionMode mode) =>
        ZatcaEInvoiceDocument.Create(
            companyId: 1, documentType: ZatcaDocumentType.SalesInvoice, sourceDocumentId: 1,
            invoiceNumber: "INV-2026-000001", invoiceUuid: Guid.NewGuid(), icv: 1,
            previousHash: "prev", invoiceHash: "hash", mode: mode,
            qrTlv: "tlv", xmlPath: @"C:\x\inv.xml", signedXmlPath: null);

    /// <summary><b>لبُّ الكناريّ:</b> «مخلَّصة» بلا مسارٍ تُرفض.</summary>
    [Fact]
    public void MarkSubmitted_RefusesClearedWithoutAClearedDocumentPath()
    {
        var doc = NewClearanceDoc();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            doc.MarkSubmitted(ZatcaComplianceStatus.Cleared, "{}"));

        Assert.Contains("مخلَّص", ex.Message);
        Assert.NotEqual(ZatcaComplianceStatus.Cleared, doc.ComplianceStatus);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkSubmitted_RefusesAnEmptyPathToo(string path)
    {
        var doc = NewClearanceDoc();

        Assert.Throws<InvalidOperationException>(() =>
            doc.MarkSubmitted(ZatcaComplianceStatus.Cleared, "{}", path));
    }

    /// <summary><b>شاهدُ ضبطٍ موجب:</b> المسارُ الصحيحُ يُقبل ويُرفَق في نداءٍ واحد.</summary>
    [Fact]
    public void MarkSubmitted_AcceptsClearedWithAPath_AndAttachesIt()
    {
        var doc = NewClearanceDoc();

        doc.MarkSubmitted(ZatcaComplianceStatus.Cleared, "{}", @"C:\x\inv.cleared.xml");

        Assert.Equal(ZatcaComplianceStatus.Cleared, doc.ComplianceStatus);
        Assert.Equal(@"C:\x\inv.cleared.xml", doc.ClearedXmlFilePath);
    }

    /// <summary>
    /// <b>والإبلاغُ لا يلزمه مستند</b> — الهيئةُ لا تُعيد فيه شيئاً، فلا يُشدَّد بلا سبب.
    /// </summary>
    [Fact]
    public void MarkSubmitted_ReportedNeedsNoClearedDocument()
    {
        var doc = Doc(ZatcaTransmissionMode.Reporting);

        doc.MarkSubmitted(ZatcaComplianceStatus.Reported, "{}");

        Assert.Equal(ZatcaComplianceStatus.Reported, doc.ComplianceStatus);
        Assert.Null(doc.ClearedXmlFilePath);
    }

    /// <summary>
    /// <b>ويبقى المستندُ قابلاً لإعادة المحاولة بعد الرفض</b> — وهو ما كان يُغلقه العطل.
    /// </summary>
    [Fact]
    public void AFailedClearance_LeavesTheDocumentRetryable()
    {
        var doc = NewClearanceDoc();
        var before = doc.ComplianceStatus;

        Assert.Throws<InvalidOperationException>(() =>
            doc.MarkSubmitted(ZatcaComplianceStatus.Cleared, "{}"));

        Assert.Equal(before, doc.ComplianceStatus);
        Assert.Null(doc.ClearedXmlFilePath);
    }
}
