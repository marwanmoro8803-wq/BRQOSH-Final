using Brqosh.Domain.Accounting;

namespace Brqosh.Domain.Sales;

public enum SalesQuotationStatus
{
    Draft = 0,
    Sent = 1,
    Accepted = 2,
    Rejected = 3,
    Expired = 4,
}

/// <summary>عرض سعر — LIFECYCLE-31 (مبيعات مبكرة).</summary>
public class SalesQuotation : ILineDocumentTotals
{
    private readonly List<SalesInvoiceLine> _lines = [];

    public long Id { get; private set; }
    public long CompanyId { get; private set; }
    public long CustomerId { get; private set; }
    public string? QuotationNumber { get; private set; }
    public DateOnly QuotationDate { get; private set; }
    public DateOnly ValidUntil { get; private set; }
    public SalesQuotationStatus Status { get; private set; } = SalesQuotationStatus.Draft;
    public decimal Subtotal { get; private set; }
    public decimal VatAmount { get; private set; }
    public IReadOnlyList<SalesInvoiceLine> Lines => _lines.AsReadOnly();

    private SalesQuotation() { }

    private SalesQuotation(long companyId, long customerId, DateOnly quotationDate, DateOnly validUntil)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        QuotationDate = quotationDate;
        ValidUntil = validUntil;
    }

    public static SalesQuotation CreateDraft(long companyId, long customerId, DateOnly quotationDate, int validDays = 30)
    {
        if (validDays < 1)
            throw new AccountingException("مدة صلاحية العرض يجب أن تكون يوماً واحداً على الأقل.");
        return new SalesQuotation(companyId, customerId, quotationDate, quotationDate.AddDays(validDays));
    }

    /// <summary>
    /// (م-٥ · ن-٥٢) <b>والفئةُ الضريبيّةُ تُقبَل هنا — وكانت لا تُقبَل.</b>
    ///
    /// <para>عرضُ السعر وطلبُ الشراء كانا وحدَهما من أصحاب <see cref="SalesInvoiceLine"/>
    /// التسعة عاجزَين عن التعبير عن فئةٍ ضريبيّة، <b>وهما بعينهما الجدولان اللذان كان عمودُهما
    /// عدداً صحيحاً</b> (ن-٥١): العمودُ لم يُعلَن نصّاً لأنّ المستندَ لم يحمل المفهومَ أصلاً.
    /// فالعجزُ والعمودُ وجهان لسببٍ واحد.</para>
    ///
    /// <para><b>ولا يُضاف الوسيطُ وحدَه.</b> سجلُّ <c>SalesInvoiceLine</c> يشترط في تعليق
    /// <c>UnitCode</c> ألّا يُتاح في مستندٍ ما لا يُحوَّل عنه — <b>فالمُصرِّفُ يضمن الغيابَ بلا
    /// حارسٍ يُنسى</b>. ولذلك يُمرَّر الثلاثيُّ في <c>SalesOrderService.CreateFromQuotationAsync</c>
    /// في التغيير نفسِه.</para>
    /// </summary>
    public void AddLine(string description, decimal quantity, decimal unitPrice, long? productId = null,
        Tax.VatCategory vatCategory = Tax.VatCategory.Standard,
        string? taxExemptionReasonCode = null, string? taxExemptionReason = null)
    {
        if (Status != SalesQuotationStatus.Draft)
            throw new AccountingException("لا تُعدَّل عروض الأسعار بعد الإرسال.");
        DocumentLineScale.EnsureUnitPriceHalala(unitPrice);
        var line = new SalesInvoiceLine(description.Trim(), quantity, unitPrice, productId,
            vatCategory, taxExemptionReasonCode, taxExemptionReason);
        _lines.Add(line);
        RecalculateTotals();
    }

    public void MarkSent(string number)
    {
        if (Status != SalesQuotationStatus.Draft)
            throw new AccountingException("العرض ليس مسودة.");
        QuotationNumber = number;
        Status = SalesQuotationStatus.Sent;
    }

    public void Accept() => Status = SalesQuotationStatus.Accepted;

    /// <summary>
    /// (م-٥ · ن-٥٢) <b>الأوعيةُ بالفئة — وكانت نسبةً مسمّرةً على الإجمالي.</b>
    ///
    /// <para>كان الحسابُ <c>Subtotal * SaudiVatRate</c> — <b>وهو بعينه العطلُ الذي وُجد
    /// CEEF-207 لإزالته</b> («كانت النسبة 15% مسمّرة في كل المسارات»). ونجا هنا وحدَه لسببٍ
    /// واحد: <b>عرضُ السعر لم يكن يحمل فئةً ضريبيّةً أصلاً</b>، فالنسبةُ الواحدةُ كانت
    /// صحيحةً بحكم استحالة غيرها.</para>
    ///
    /// <para><b>ومتى صار يحملها صار الحسابُ خطأً حيّاً</b>: عرضٌ فيه سطرٌ معفًى كان يقتبس
    /// للعميل ضريبةً على ما لا ضريبةَ عليه، <b>ثمّ يتغيّر الإجماليُّ عند التحويل إلى أمر
    /// بيع</b> — فالعرضُ الذي وقّعه العميلُ ليس الفاتورةَ التي تصله. وهو ما قِيس: ٣٠٠٫٠٠
    /// في العرض مقابل ١٥٠٫٠٠ في الأمر على الأسطر نفسِها. <b>فالحاسبةُ المشتركةُ هي المرجع</b>،
    /// كما في أمر البيع والفاتورة.</para>
    /// </summary>
    private void RecalculateTotals()
    {
        (Subtotal, VatAmount) = Accounting.LineDocumentTotalsCalculator.Compute(_lines);
    }
}
