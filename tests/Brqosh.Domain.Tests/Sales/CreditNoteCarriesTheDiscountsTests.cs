using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;

namespace Brqosh.Domain.Tests.Sales;

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٦) <b>الإشعارُ الدائنُ كان يُسقط خصومَ الفاتورة.</b>
///
/// <para>الفاتورةُ تحمل خصمَ سطرٍ (<c>DiscountPercent</c>) وخصمَ مستندٍ
/// (<c>DocumentDiscountAmount</c>)، <b>والإشعارُ كان يُبنى منها بلا أيٍّ منهما</b> —
/// السطرُ بالسعر الخامّ، و<c>RecalculateTotals</c> يُستدعى بلا خصمِ مستند.</para>
///
/// <para><b>فأثران:</b> مرتجعٌ جزئيٌّ <b>يُعيد للعميل أكثرَ ممّا دفع</b> ويُنقص ضريبةَ
/// المخرجات أكثرَ ممّا استُحقّ — <b>وهو نقصٌ في إقرارٍ يُسلَّم للهيئة</b>. ومرتجعٌ كاملٌ
/// <b>يُرفض</b> لأنّ إجماليَّه الخامَّ يتجاوز متبقّي الفاتورة المخصومة.</para>
///
/// <para><b>والحسابُ كان موجوداً كلَّه:</b> <c>SalesInvoiceLine</c> تحمل النسبةَ، وعمودُها
/// في <c>credit_note_lines</c> مُعرَّفٌ منذ ط1-٢ <b>ولا يكتبه أحد</b>،
/// و<c>LineDocumentTotalsCalculator</c> يوزّع خصمَ المستند على الأوعية. <b>وإنّما كان
/// الإشعارُ لا يُمرّر شيئاً.</b></para>
/// </summary>
public class CreditNoteCarriesTheDiscountsTests
{
    private const long CompanyId = 1;
    private const long CustomerId = 9;
    private static readonly DateOnly Day = new(2026, 5, 10);

    /// <summary>فاتورةٌ: مئةُ قطعةٍ بعشرة، خصمُ سطرٍ ١٠٪، ثمّ خصمُ مستندٍ ١٠٠.</summary>
    private static SalesInvoice DiscountedInvoice()
    {
        var invoice = SalesInvoice.CreateDraft(CompanyId, CustomerId, Day);
        invoice.AddLine("صنف", 100m, 10m, discountPercent: 10m);
        invoice.ApplyDocumentDiscount(100m);
        return invoice;
    }

    // ── العطلُ نفسُه ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>القياسُ الحاكم — المرتجعُ الكامل يساوي الفاتورةَ بالضبط.</b>
    ///
    /// <para>صافي الفاتورة ٩٠٠ ناقصَ خصمِ المستند ١٠٠ ⟵ <b>٨٠٠</b>، وضريبتُها ١٢٠.
    /// <b>وكان الإشعارُ يُحسب ١٬٠٠٠</b> — أي أكثرَ من الفاتورة كلِّها.</para>
    /// </summary>
    [Fact]
    public void AFullReturn_MatchesTheDiscountedInvoice_Exactly()
    {
        var invoice = DiscountedInvoice();
        var note = FullReturnOf(invoice);

        Assert.Equal(800m, note.Subtotal);
        Assert.Equal(120m, note.VatAmount);
        Assert.Equal(invoice.Subtotal, note.Subtotal);
        Assert.Equal(invoice.VatAmount, note.VatAmount);
    }

    /// <summary>
    /// <b>والمرتجعُ الجزئيُّ بنسبته</b> — أربعون قطعةً من مئة ⟵ صافٍ ٣٦٠ ناقصَ حصّتِه
    /// من الخصم ٤٠ ⟵ <b>٣٢٠</b>. وكان يُحسب ٤٠٠: زيادةٌ ثمانون ريالاً تُدفع للعميل
    /// وتُنقَص من ضريبة المخرجات.
    /// </summary>
    [Fact]
    public void APartialReturn_TakesItsShareOfTheDocumentDiscount()
    {
        var invoice = DiscountedInvoice();

        var note = CreditNote.CreateDraft(CompanyId, CustomerId, originalInvoiceId: 1, Day, "مرتجع");
        note.AddLine("صنف", 40m, 10m, discountPercent: 10m);
        note.ApplyDocumentDiscount(CreditNote.DocumentDiscountShareFor(invoice, note.Subtotal));

        Assert.Equal(320m, note.Subtotal);
        Assert.Equal(48m, note.VatAmount);
    }

    /// <summary>
    /// <b>وخصمُ السطر وحدَه يبلغ الإشعار</b> — فاتورةٌ بلا خصمِ مستندٍ وبخصمِ سطرٍ ٢٥٪.
    /// </summary>
    [Fact]
    public void ALineDiscount_ReachesTheCreditNote()
    {
        var note = CreditNote.CreateDraft(CompanyId, CustomerId, originalInvoiceId: 1, Day, "مرتجع");
        note.AddLine("صنف", 10m, 100m, discountPercent: 25m);

        Assert.Equal(750m, note.Subtotal);
    }

    // ── شواهدُ الضبط ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>شاهدُ ضبطٍ أوّل:</b> فاتورةٌ بلا خصومٍ تبقى كما كانت حرفاً — لا حصّةَ خصمٍ
    /// تُخترع، ولا نسبةَ تُفرض.
    /// </summary>
    [Fact]
    public void WithoutAnyDiscount_TheNoteIsUnchanged()
    {
        var invoice = SalesInvoice.CreateDraft(CompanyId, CustomerId, Day);
        invoice.AddLine("صنف", 100m, 10m);

        var note = FullReturnOf(invoice);

        Assert.Equal(0m, CreditNote.DocumentDiscountShareFor(invoice, note.Subtotal));
        Assert.Equal(1_000m, note.Subtotal);
        Assert.Equal(150m, note.VatAmount);
        Assert.Equal(invoice.Subtotal, note.Subtotal);
    }

    /// <summary>
    /// <b>شاهدُ ضبطٍ ثانٍ:</b> الحصّةُ لا تتجاوز صافيَ المُرتجَع أبداً — فالتقريبُ لأعلى
    /// في مرتجعٍ شبهِ كاملٍ كان يُنتج خصماً يرفضه النطاقُ بحقّ.
    /// </summary>
    [Theory]
    [InlineData(0.01)]
    [InlineData(333.33)]
    [InlineData(899.99)]
    [InlineData(900.00)]
    public void TheShare_NeverExceedsTheReturnedNet(decimal returnedNet)
    {
        var invoice = DiscountedInvoice();

        var share = CreditNote.DocumentDiscountShareFor(invoice, returnedNet);

        Assert.True(share <= returnedNet,
            $"‏الحصّةُ {share} تتجاوز صافيَ المُرتجَع {returnedNet} — ويرفضها النطاق.");
        Assert.True(share >= 0m);
    }

    /// <summary>وخصمُ سطرٍ خارج المدى يُرفض في موضع بنائه لا بعد الترحيل.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void AnOutOfRangeLineDiscount_IsRefused(decimal percent)
    {
        var note = CreditNote.CreateDraft(CompanyId, CustomerId, originalInvoiceId: 1, Day, "مرتجع");

        Assert.Throws<AccountingException>(
            () => note.AddLine("صنف", 1m, 100m, discountPercent: percent));
    }

    /// <summary>يبني إشعارَ مرتجعٍ كاملٍ كما يبنيه الحوارُ من الفاتورة.</summary>
    private static CreditNote FullReturnOf(SalesInvoice invoice)
    {
        var note = CreditNote.CreateDraft(CompanyId, CustomerId, originalInvoiceId: 1, Day, "مرتجع");

        foreach (var line in invoice.Lines)
            note.AddLine(line.Description, line.Quantity, line.UnitPrice, line.ProductId,
                line.VatCategory, line.TaxExemptionReasonCode, line.TaxExemptionReason,
                discountPercent: line.DiscountPercent);

        var share = CreditNote.DocumentDiscountShareFor(invoice, note.Subtotal);
        if (share > 0m)
            note.ApplyDocumentDiscount(share);

        return note;
    }
}
