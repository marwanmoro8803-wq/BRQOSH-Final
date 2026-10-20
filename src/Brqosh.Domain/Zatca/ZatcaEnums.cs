namespace Brqosh.Domain.Zatca;

public enum ZatcaEnvironment
{
    /// <summary>بيئة المطور / المحاكاة — للاختبار دون إرسال حقيقي.</summary>
    Simulation = 0,

    /// <summary>الإنتاج — إرسال فعلي لهيئة الزكاة.</summary>
    Production = 1,
}

public enum ZatcaTransmissionMode
{
    /// <summary>فاتورة مبسطة (B2C) — إبلاغ.</summary>
    Reporting = 0,

    /// <summary>فاتورة ضريبية (B2B) — اعتماد فوري.</summary>
    Clearance = 1,
}

public enum ZatcaDocumentType
{
    SalesInvoice = 1,
    CreditNote = 2,
    ProgressBilling = 3,
    /// <summary>إشعار مدين — لإلغاء إشعار دائن (UBL 383).</summary>
    DebitNote = 4,

    /// <summary>
    /// (CEEF-229 · Z-3) إشعار دائن ناتج عن <b>إلغاء فاتورة مبيعات</b> — مصدره فاتورة لا إشعار.
    ///
    /// كان يُسجَّل بالنوع <c>CreditNote</c>، فيُحشر معرّف الفاتورة في العمود نفسه الذي يحمل
    /// معرّف الإشعار الحقيقيّ. وثلاثة فضاءات معرّفات مستقلّة — تبدأ كلّها من واحد — داخل فهرسٍ
    /// فريد واحد <c>(CompanyId, DocumentType, SourceDocumentId)</c>. فالتصادم شبه مؤكّد:
    /// بعد إلغاء الفاتورة رقم 12 يستحيل ترحيل الإشعار الدائن رقم 12، ويُنسب مرجع الفاتورة
    /// الأصلية إلى مستندٍ آخر. الاسم قصير عمداً — العمود <c>varchar(16)</c>.
    /// </summary>
    InvoiceCancel = 5,

    /// <summary>إشعار دائن ناتج عن إلغاء مستخلص أعمال — فضاء معرّفات ثالث، انظر <see cref="InvoiceCancel"/>.</summary>
    BillingCancel = 6,

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٦) فاتورةُ ضريبةٍ عن <b>بيع أصلٍ ثابت</b> — مصدرُها
    /// <c>fixed_assets</c>، وهو <b>فضاءُ معرّفاتٍ رابع</b>.
    ///
    /// <para><b>وبيعُ أصلٍ من أصول النشاط توريدٌ خاضعٌ نظاماً</b>، وكان الاستبعادُ يُقيّد
    /// ضريبةَ مخرجاتٍ <b>بلا فاتورةٍ ولا مستندٍ إلكترونيّ</b>: توريدٌ خاضعٌ لا تعرفه
    /// الهيئةُ أبداً — وهي مخالفةُ الطور الثاني بعينها.</para>
    ///
    /// <para><b>ولم يُحشر في <c>SalesInvoice</c></b>: معرّفُ الأصل ومعرّفُ الفاتورة
    /// يبدآن من واحدٍ داخل الفهرس الفريد <c>(CompanyId, DocumentType, SourceDocumentId)</c>،
    /// <b>فالتصادمُ شبه مؤكّد</b> — وهو الدرسُ نفسُه الذي وُلد منه <see cref="InvoiceCancel"/>.
    /// والاسمُ قصيرٌ عمداً — العمودُ <c>varchar(16)</c>.</para>
    /// </summary>
    AssetDisposal = 7,

    /// <summary>
    /// (جاهزيّةُ الإطلاق · ح-١٦١) إشعارٌ دائنٌ عن <b>عكس استبعاد أصلٍ بيع بفاتورة</b> — مصدرُه الأصلُ كفاتورته
    /// (<see cref="AssetDisposal"/>)، ومرجعُه تلك الفاتورة. والاسمُ قصيرٌ عمداً — العمودُ <c>varchar(16)</c>.
    /// </summary>
    DisposalCancel = 8,

    /// <summary>
    /// (جاهزيّةُ الإطلاق · <c>sector-modules#6</c>) <b>فاتورةُ دفعةٍ مقدّمة (UBL 386)</b> — عن عربون حجزٍ يومَ قبضه، وهو نقطةُ
    /// استحقاق الضريبة. مصدرُها قيدُ العربون (<c>journal_entries</c>) — <b>فضاءُ معرّفاتٍ خامس</b> —، وتُسوّى في فاتورة المغادرة
    /// بسطر تسويةٍ يُشير إليها. والاسمُ قصيرٌ عمداً — العمود <c>varchar(16)</c>.
    /// </summary>
    Prepayment = 9,

    /// <summary>
    /// (<c>sector-modules#6</c>) إشعارٌ دائنٌ (381) بردّ عربونٍ عند إلغاء الحجز — مرجعُه فاتورةُ دفعته (<see cref="Prepayment"/>)
    /// ومصدرُه قيدُ العربون نفسُه، فلكلّ فاتورةِ دفعةٍ ردٌّ واحدٌ على الأكثر.
    /// </summary>
    DepositRefund = 10,
}

/// <summary>حالة مستند الفاتورة الإلكترونية في دورة ZATCA.</summary>
public enum ZatcaComplianceStatus
{
    Generated = 0,
    ReadyToReport = 1,
    ReadyToClear = 2,
    Reported = 3,
    Cleared = 4,
    Accepted = 5,
    Rejected = 6,

    /// <summary>
    /// (CEEF-229 · م0-١٠) مُولَّد ومُحاكى محلياً — <b>لم يصل الهيئة</b>.
    ///
    /// كانت المحاكاة تُعلَّم <c>Cleared</c>/<c>Reported</c>، وهما حالتا القبول الفعليّ لدى الهيئة.
    /// فمنشأةٌ فعّلت التكامل من القالب (وهو يفرض المحاكاة) ترى كل فواتيرها «معتمدة» ولم تصل
    /// الهيئةَ فاتورةٌ واحدة — وتكتشفه عند الفحص الضريبيّ بعد شهور. الضرر تضليليّ لا تقنيّ،
    /// ولذلك الحالة منفصلة ولا تُعرَض قَبولاً بحال.
    /// </summary>
    SimulatedLocally = 7,
}

public static class ZatcaComplianceStatusExtensions
{
    /// <summary>تسمية عربية للعرض والطباعة — الاسم البرمجيّ لا يُعرض للمستخدم.</summary>
    public static string NameAr(this ZatcaComplianceStatus status) => status switch
    {
        ZatcaComplianceStatus.Generated => "مُولَّد",
        ZatcaComplianceStatus.ReadyToReport => "جاهز للإبلاغ",
        ZatcaComplianceStatus.ReadyToClear => "جاهز للاعتماد",
        ZatcaComplianceStatus.Reported => "مُبلَّغ للهيئة",
        ZatcaComplianceStatus.Cleared => "معتمَد من الهيئة",
        ZatcaComplianceStatus.Accepted => "مقبول",
        ZatcaComplianceStatus.Rejected => "مرفوض",
        ZatcaComplianceStatus.SimulatedLocally => "محاكاة محلية — لم يُرسل للهيئة",
        _ => status.ToString(),
    };

    /// <summary>هل تعني هذه الحالة أن الهيئة استلمت المستند فعلاً؟</summary>
    public static bool IsAcknowledgedByAuthority(this ZatcaComplianceStatus status) =>
        status is ZatcaComplianceStatus.Reported
               or ZatcaComplianceStatus.Cleared
               or ZatcaComplianceStatus.Accepted;
}
