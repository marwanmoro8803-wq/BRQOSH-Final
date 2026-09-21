using Brqosh.Domain.Zatca;

namespace Brqosh.Desktop.Ui;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-١٥) <b>أيُّ مستندٍ يحمله <c>SourceDocumentId</c> — فالأنواعُ
/// أكثرُ من فضاءات المعرّفات.</b>
///
/// <para><c>ZatcaEInvoiceDocument</c> حمل يومَ ح-١٥ <b>ستّةَ أنواع</b> في عمودِ مصدرٍ واحد،
/// ومصادرُها <b>ثلاثةٌ</b>: فاتورةُ مبيعات، وإشعارٌ دائن، ومستخلصُ أعمال — <b>ثمّ أضاف ح-١٦
/// رابعاً</b>: الأصلُ الثابتُ المباع. <b>وكلُّ فضاءٍ يبدأ من واحد</b> — وهو بعينه السببُ
/// الذي من أجله أُفرد <see cref="ZatcaDocumentType.InvoiceCancel"/> بقيمةٍ خاصّةٍ في
/// جدول التخزين.</para>
///
/// <para><b>والقراءةُ لم تُدقَّق يومها.</b> فبقيت شاشةُ المستندات تسأل سؤالاً ثنائيّاً:
/// «أهو فاتورةُ مبيعات؟ فإن لم يكن <b>فهو إشعارٌ دائن</b>» — <b>فثلاثةٌ من الستّة تُقرأ من
/// الجدول الخطأ</b>: مستخلصٌ رقمُه ٥ يُعرض بسطور <b>إشعارٍ دائنٍ رقمُه ٥</b>، بأصنافه ومبالغه
/// وضريبته، تحت عنوان «إشعار دائن». <b>ولا شيء يقول إنّ المعروضَ مستندٌ آخر.</b></para>
///
/// <para><b>والحارسُ عمل كما وُضع له</b>: نوعُ ح-١٦ السابعُ أسقط هذه الدوالَّ رمياً حتّى
/// أُدرج فيها — <b>ولم يُعرض بوصفه «إشعارَ دائنٍ» صامتاً</b> كما كان سيقع لولاه.</para>
///
/// <para><b>ولذلك صار الجوابُ دالّةً واحدةً تُقاس</b> بدل شرطٍ داخل معالجِ ضغطٍ يحتاج قاعدةَ
/// بياناتٍ وخيطَ واجهة — على منوال <see cref="VatReturnRows"/>. <b>ولا فرعَ افتراضيّاً يبتلع
/// نوعاً جديداً</b>: النوعُ الذي لا يُذكر هنا يرمي، فيُوقِف الاختبارُ من يضيف سابعاً
/// ولا يُحدِّث القراءة.</para>
/// </summary>
public static class ZatcaDocumentPresentation
{
    /// <summary>المستندُ الذي يشير إليه <c>SourceDocumentId</c> فعلاً.</summary>
    public enum SourceKind
    {
        /// <summary>فاتورةُ مبيعاتٍ في <c>sales_invoices</c>.</summary>
        SalesInvoice,

        /// <summary>إشعارٌ دائنٌ في <c>credit_notes</c>.</summary>
        CreditNote,

        /// <summary>مستخلصُ أعمالٍ في <c>progress_billings</c>.</summary>
        ProgressBilling,

        /// <summary>(ح-١٦) أصلٌ ثابتٌ بِيع — في <c>fixed_assets</c>.</summary>
        FixedAssetDisposal,
    }

    /// <summary>اسمُ نوع المستند كما يُعرض في الجدول.</summary>
    public static string TypeLabel(ZatcaDocumentType type) => type switch
    {
        ZatcaDocumentType.SalesInvoice => LocaleManager.T("فاتورة", "Invoice"),
        ZatcaDocumentType.CreditNote => LocaleManager.T("إشعار دائن", "Credit Note"),
        ZatcaDocumentType.ProgressBilling => LocaleManager.T("مستخلص أعمال", "Progress Billing"),
        ZatcaDocumentType.DebitNote => LocaleManager.T("إشعار مدين", "Debit Note"),
        ZatcaDocumentType.InvoiceCancel => LocaleManager.T("إشعار دائن (إلغاء فاتورة)", "Credit Note (Invoice Cancellation)"),
        ZatcaDocumentType.BillingCancel => LocaleManager.T("إشعار دائن (إلغاء مستخلص)", "Credit Note (Billing Cancellation)"),
        ZatcaDocumentType.AssetDisposal => LocaleManager.T("فاتورة بيع أصل ثابت", "Fixed Asset Sale Invoice"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "نوعُ مستندٍ إلكترونيٍّ غيرُ معروفٍ لشاشة المستندات."),
    };

    /// <summary>
    /// <b>مصدرُ المستند — لا نوعُه.</b>
    ///
    /// <para>ثلاثةٌ من الستّة مصدرُها <b>غيرُ ما يوحي به اسمُها</b>: <c>DebitNote</c> إشعارٌ
    /// مدينٌ مصدرُه <b>إشعارٌ دائنٌ أُلغي</b>، و<c>InvoiceCancel</c> إشعارٌ دائنٌ مصدرُه
    /// <b>فاتورةٌ أُلغيت</b>، و<c>BillingCancel</c> إشعارٌ دائنٌ مصدرُه <b>مستخلصٌ أُلغي</b>.
    /// <b>فالاسمُ يصف المستندَ المُصدَر، والمصدرُ هو ما أُلغي.</b></para>
    /// </summary>
    public static SourceKind SourceOf(ZatcaDocumentType type) => type switch
    {
        ZatcaDocumentType.SalesInvoice => SourceKind.SalesInvoice,
        ZatcaDocumentType.CreditNote => SourceKind.CreditNote,
        ZatcaDocumentType.ProgressBilling => SourceKind.ProgressBilling,
        ZatcaDocumentType.DebitNote => SourceKind.CreditNote,
        ZatcaDocumentType.InvoiceCancel => SourceKind.SalesInvoice,
        ZatcaDocumentType.BillingCancel => SourceKind.ProgressBilling,
        ZatcaDocumentType.AssetDisposal => SourceKind.FixedAssetDisposal,
        _ => throw new ArgumentOutOfRangeException(
            nameof(type), type, "نوعُ مستندٍ إلكترونيٍّ غيرُ معروفٍ لشاشة المستندات."),
    };

    /// <summary>عنوانُ نافذة السطور — يصف <b>المستندَ المصدر</b> الذي تُعرض سطورُه.</summary>
    public static string SourceTitle(ZatcaDocumentType type) => SourceOf(type) switch
    {
        SourceKind.SalesInvoice => LocaleManager.T("فاتورة مبيعات", "Sales Invoice"),
        SourceKind.CreditNote => LocaleManager.T("إشعار دائن", "Credit Note"),
        SourceKind.ProgressBilling => LocaleManager.T("مستخلص أعمال", "Progress Billing"),
        SourceKind.FixedAssetDisposal => LocaleManager.T("أصل ثابت مباع", "Fixed Asset Sold"),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// <summary>رسالةُ الغياب — <b>تسمّي المستندَ المصدر</b> لا «الفاتورة» دائماً.</summary>
    public static string SourceMissingMessage(ZatcaDocumentType type) =>
        LocaleManager.T($"المستند المصدر ({SourceTitle(type)}) غير موجود.",
                        $"Source document ({SourceTitle(type)}) not found.");
}
