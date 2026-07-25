using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
namespace Brqosh.Domain.Zatca;

/// <summary>
/// مستند فاتورة إلكترونية — UBL + UUID + سلسلة الهاش (PIH) + QR TLV.
/// يُنشأ تلقائياً عند ترحيل فاتورة المبيعات/إشعار الدائن إذا كانت ZATCA مفعّلة.
/// </summary>
public class ZatcaEInvoiceDocument : IBranchScoped
{
    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long? BranchId { get; private set; }
    public ZatcaDocumentType DocumentType { get; private set; }
    public long SourceDocumentId { get; private set; }

    public string InvoiceNumber { get; private set; }
    public Guid InvoiceUuid { get; private set; }

    /// <summary>عداد الفاتورة الإلكترونية (ICV) — تسلسلي داخل الشركة.</summary>
    public int Icv { get; private set; }

    /// <summary>هاش الفاتورة السابقة (PIH) — Base64 SHA-256.</summary>
    public string PreviousInvoiceHash { get; private set; }

    /// <summary>هاش هذه الفاتورة — Base64 SHA-256 من XML قبل التوقيع.</summary>
    public string InvoiceHash { get; private set; }

    public ZatcaTransmissionMode TransmissionMode { get; private set; }
    public ZatcaComplianceStatus ComplianceStatus { get; private set; }

    /// <summary>QR بصيغة TLV مشفّر Base64 — للطباعة على الفاتورة.</summary>
    public string QrTlvBase64 { get; private set; }

    public string XmlFilePath { get; private set; }
    public string? SignedXmlFilePath { get; private set; }
    public string? ZatcaResponseJson { get; private set; }

    public DateTime GeneratedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime LastStatusAtUtc { get; private set; }

    // EF Core
    private ZatcaEInvoiceDocument()
    {
        InvoiceNumber = null!;
        PreviousInvoiceHash = null!;
        InvoiceHash = null!;
        QrTlvBase64 = null!;
        XmlFilePath = null!;
    }

    public static ZatcaEInvoiceDocument Create(
        long companyId, ZatcaDocumentType documentType, long sourceDocumentId,
        string invoiceNumber, Guid invoiceUuid, int icv, string previousHash, string invoiceHash,
        ZatcaTransmissionMode mode, string qrTlv, string xmlPath, string? signedXmlPath)
    {
        var status = mode == ZatcaTransmissionMode.Reporting
            ? ZatcaComplianceStatus.ReadyToReport
            : ZatcaComplianceStatus.ReadyToClear;

        return new ZatcaEInvoiceDocument
        {
            CompanyId = companyId,
            DocumentType = documentType,
            SourceDocumentId = sourceDocumentId,
            InvoiceNumber = invoiceNumber,
            InvoiceUuid = invoiceUuid,
            Icv = icv,
            PreviousInvoiceHash = previousHash,
            InvoiceHash = invoiceHash,
            TransmissionMode = mode,
            ComplianceStatus = status,
            QrTlvBase64 = qrTlv,
            XmlFilePath = xmlPath,
            SignedXmlFilePath = signedXmlPath,
            GeneratedAtUtc = DateTime.UtcNow,
            LastStatusAtUtc = DateTime.UtcNow,
        };
    }

    public void MarkSubmitted(ZatcaComplianceStatus newStatus, string? responseJson)
    {
        ComplianceStatus = newStatus;
        ZatcaResponseJson = responseJson;
        SubmittedAtUtc = DateTime.UtcNow;
        LastStatusAtUtc = DateTime.UtcNow;
    }

    public void MarkRejected(string responseJson)
    {
        ComplianceStatus = ZatcaComplianceStatus.Rejected;
        ZatcaResponseJson = responseJson;
        LastStatusAtUtc = DateTime.UtcNow;
    }
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
