namespace Brqosh.Infrastructure.Reporting;

/// <summary>Print layout kind — drives title, party labels, and ZATCA compliance strip.</summary>
public enum PrintDocumentKind
{
    SimplifiedSalesInvoice = 1,
    StandardSalesInvoice = 2,
    PurchaseInvoice = 3,
    CreditNote = 4,
    PurchaseDebitNote = 5,
    ProgressBilling = 6,

    /// <summary>
    /// إشعارٌ مدينٌ <b>على جانب المبيعات</b> — يُصدَر لإلغاء
    /// إشعارٍ دائن (UBL 383).
    ///
    /// <para><b>ولا يُخلط بـ<see cref="PurchaseDebitNote"/></b>: ذاك إشعارُ مشترياتٍ
    /// طرفُه <b>مورّد</b>، وهذا طرفُه <b>عميل</b> — <b>وطباعتُه بعنوان المشتريات تنسب
    /// المستندَ إلى الطرف الخطأ</b> في وثيقةٍ ضريبيّة.</para>
    /// </summary>
    SalesDebitNote = 7,
}
