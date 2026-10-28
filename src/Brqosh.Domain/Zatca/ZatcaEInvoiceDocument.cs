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

    /// <summary>
    /// <b>المستندُ المُخلَّص كما أعادته الهيئة — لا نسختُنا الموقَّعة محلياً.</b>
    /// في التخليص (B2B) تُعيد الهيئةُ فاتورةً موقَّعةً بختمها، <b>وهي الوثيقةُ النظاميّةُ التي
    /// تُسلَّم للمشتري</b>. وبلا حفظِها كان المشتري يستلم نسختَنا المحليّة.
    /// </summary>
    public string? ClearedXmlFilePath { get; private set; }

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

    /// <param name="generatedAtUtc">
    /// <b>لحظةُ التوليد — تُمرَّر ولا تُقرأ هنا.</b>
    ///
    /// <para><b>وكان الكيانُ يقرأ <c>DateTime.UtcNow</c> بنفسه</b>، والمولّدُ قد قرأها
    /// قبلَه ليشتقّ منها <c>IssueTime</c> ووسمَ QR. <b>فقراءتان لا واحدة</b> — ومتى وقعتا
    /// على طرفَي ثانيةٍ <b>أعلن المستندُ لحظةً وسجّل أخرى</b>.</para>
    ///
    /// <para><b>وهو خللُ امتثالٍ لا خللُ اختبار:</b> فاتورةُ ضريبةٍ إلكترونيّة <b>وقتُها
    /// المعلن جزءٌ من مستندها المُوقَّع</b>، ورمزُ QR يحمله. <b>ومستندٌ يقول لحظتين
    /// يُنازَع فيه عند الفحص</b> — والفرقُ ثانيةٌ اليومَ وقد يصير أكثرَ على جهازٍ محمَّل.</para>
    ///
    /// <para><b>وتعليقُ المولّد كان يَعِد بهذا صراحةً</b> — «تُؤخذ مرّةً واحدة ويُشتقّ منها
    /// الطرفان، فيستحيل اختلافُهما بناءً» — <b>والوعدُ لم يكن محقَّقاً</b>: الكيانُ لم يكن
    /// يقبلها فيقرؤها لنفسه. <b>أخرجه اضطرابُ اختبارٍ يسقط ثلثَ الوقت</b>، لا قراءةُ شيفرة.</para>
    /// </param>
    public static ZatcaEInvoiceDocument Create(
        long companyId, ZatcaDocumentType documentType, long sourceDocumentId,
        string invoiceNumber, Guid invoiceUuid, int icv, string previousHash, string invoiceHash,
        ZatcaTransmissionMode mode, string qrTlv, string xmlPath, string? signedXmlPath,
        DateTime? generatedAtUtc = null)
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
            GeneratedAtUtc = generatedAtUtc ?? DateTime.UtcNow,
            LastStatusAtUtc = generatedAtUtc ?? DateTime.UtcNow,
        };
    }

    /// <summary>
    /// (م-٢ · ن-٠٥) <b>«مخلَّصة» تلزمها نسخةٌ مخلَّصةٌ على القرص — والنطاقُ يفرض ذلك لا الانضباط.</b>
    ///
    /// <para><b>العطل:</b> كان <c>SubmitCoreAsync</c> يستدعي هذه أوّلاً بحالة <c>Cleared</c>،
    /// ثمّ يكتب الملفَّ على القرص. فرميةُ كتابةٍ — قرصٌ ممتلئ أو صلاحيةٌ مفقودة — تترك الكيانَ
    /// مطفَّراً في المتعقّب؛ ويلتقط <c>ZatcaAutoSubmissionHelper</c> الرميةَ فيحفظ <b>الكيانَ
    /// نفسَه</b> (خريطةُ هويّة EF). فتُخزَّن الفاتورةُ «مخلَّصة» <b>بلا مستندٍ مخلَّص</b>،
    /// وتُدهَس استجابةُ الهيئة بملاحظتنا، ويُغلق بابُ إعادة المحاولة.</para>
    ///
    /// <para><b>وأُصلح الترتيبُ في المستدعي</b> — يُكتب الملفُّ ثمّ تُطفَّر الحالة. <b>لكنّ
    /// ترتيباً يمكن إعادتُه ليس ضماناً</b>: سطران يُبدَّلان في مراجعةٍ عابرةٍ يُعيدان العطل
    /// بلا أن يشتكيَ شيء. فالشرطُ هنا يجعل الحالةَ الكاذبةَ <b>غيرَ قابلةٍ للتعبير</b>.</para>
    ///
    /// <para>و<c>clearedXmlPath</c> إلزاميٌّ متى كانت الحالةُ <see cref="ZatcaComplianceStatus.Cleared"/>
    /// — أي في التخليص وحدَه. أمّا الإبلاغ (<c>Reported</c>) فلا تُعيد الهيئةُ فيه مستنداً.</para>
    /// </summary>
    public void MarkSubmitted(ZatcaComplianceStatus newStatus, string? responseJson, string? clearedXmlPath = null)
    {
        if (newStatus == ZatcaComplianceStatus.Cleared && string.IsNullOrWhiteSpace(clearedXmlPath))
            throw new InvalidOperationException(
                "لا تُعلَن فاتورةٌ «مخلَّصة» بلا مستندٍ مخلَّصٍ مكتوبٍ على القرص — "
                + "اكتب المستندَ أوّلاً ثمّ مرّر مسارَه.");

        ComplianceStatus = newStatus;
        ZatcaResponseJson = responseJson;
        SubmittedAtUtc = DateTime.UtcNow;
        LastStatusAtUtc = DateTime.UtcNow;

        if (clearedXmlPath is not null) AttachClearedXml(clearedXmlPath);
    }

    /// <summary>
    /// تسجيلُ مسارِ المستند المُخلَّص العائد من الهيئة.
    /// <b>يُستدعى بعد <c>MarkSubmitted</c> وحدَه</b> — فلا مستندَ مخلَّصٌ لفاتورةٍ لم تُقبَل.
    ///
    /// <para>(م-٢ · ن-٠٥) <b>وصار خاصّاً بعد أن كان عامّاً.</b> إذ كان تعليقُه يقول
    /// «يُستدعى بعد <c>MarkSubmitted</c> وحدَه» — <b>وذلك عُرفٌ لا بنية</b>، وخطوتان
    /// يفصلهما سطرٌ هما النافذةُ التي أوقعت ن-٠٥ أصلاً. <b>ولم يكن له مستدعٍ إنتاجيٌّ
    /// واحد</b> خارج هذا النوع — مقيسٌ بمسحٍ على المستودع مع شاهدِ ضبطٍ موجب — فإغلاقُه
    /// لا يمنع شيئاً قائماً ويمنع ولادةَ خطوتين جديدتين.</para>
    /// </summary>
    private void AttachClearedXml(string clearedXmlPath)
    {
        if (string.IsNullOrWhiteSpace(clearedXmlPath))
            throw new ArgumentException("مسارُ المستند المُخلَّص فارغ.", nameof(clearedXmlPath));
        if (ComplianceStatus != ZatcaComplianceStatus.Cleared)
            throw new InvalidOperationException(
                $"لا يُرفَق مستندٌ مخلَّصٌ لفاتورةٍ حالتُها {ComplianceStatus}.");
        ClearedXmlFilePath = clearedXmlPath;
        LastStatusAtUtc = DateTime.UtcNow;
    }

    public void MarkRejected(string responseJson)
    {
        ComplianceStatus = ZatcaComplianceStatus.Rejected;
        ZatcaResponseJson = responseJson;
        LastStatusAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// تسجيل سببِ فشلِ إرسالٍ تلقائيّ — <b>بلا تغيير الحالة</b>.
    ///
    /// <para>الحالة تبقى كما هي (<c>ReadyToReport</c> أو <c>ReadyToClear</c>) عمداً: الفشل قد
    /// يكون انقطاعاً عابراً، والمستند يجب أن يظلّ مؤهَّلاً لإرسالٍ لاحق. المتغيّر الوحيد أن
    /// السبب صار <b>مكتوباً</b> بدل أن يُبتلع في <c>catch</c> صامت — إذ كان الفرق بين «الشبكة
    /// مقطوعة الآن» و«هذا المستخدم لا يملك صلاحية الإرسال أصلاً» يختفي تماماً، فتبدو البيعة
    /// ناجحة وفاتورتها لن تصل الهيئة أبداً.</para>
    ///
    /// <para>ولا يُعدّ هذا رفضاً من الهيئة — <see cref="MarkRejected"/> وحده لذلك، وهو يعني
    /// أن المستند وصلها ورُدّ.</para>
    /// </summary>
    public void RecordSubmissionFailure(string reason)
    {
        ZatcaResponseJson = $"{{\"autoSubmitFailed\":true,\"reason\":{System.Text.Json.JsonSerializer.Serialize(reason)}}}";
        LastStatusAtUtc = DateTime.UtcNow;
    }
    public void AssignBranch(long branchId)
    {
        if (branchId <= 0) throw new AccountingException("الفرع غير صالح.");
        BranchId = branchId;
    }

}
