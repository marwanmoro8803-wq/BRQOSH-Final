using Brqosh.Domain.Accounting;
using Brqosh.Domain.Inventory;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Inventory;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Purchasing;

/// <summary>سطرٌ من الفاتورة يصلح للإرجاع — وكمّيّتُه المتبقّية بعد ما أُرجع سابقاً.</summary>
public sealed record ReturnableInvoiceLine(
    int LineIndex,
    string Description,
    long? ProductId,
    decimal InvoiceQuantity,
    decimal AlreadyReturned,
    decimal UnitPrice,
    VatCategory VatCategory)
{
    public decimal Returnable => InvoiceQuantity - AlreadyReturned;
}

/// <summary>
/// ما يطلب المستخدمُ إرجاعَه: سطرٌ بعينه وكمّيّةٌ منه — <b>والدفعةُ التي يُرجعها</b> للصنف المتتبَّع (ح-١٠٥).
/// </summary>
public sealed record RequestedReturn(int LineIndex, decimal Quantity, decimal? UnitPrice = null, string? BatchNumber = null);

/// <summary>
/// (جاهزيّةُ الإطلاق · ح-٣٧) <b>الإشعارُ المدين يُرجع السطرَ الذي اختاره المستخدم —
/// لا أوّلَ سطرٍ في الفاتورة مهما كتب.</b>
///
/// <para><b>ما كان — ثلاثةُ عيوبٍ في سطرٍ واحد</b> (<c>DebitNoteDialog</c>):</para>
///
/// <list type="number">
/// <item><b>المنتَجُ كان دائماً <c>Lines.FirstOrDefault()</c></b>: الشاشةُ تعرض حقلَ
/// وصفٍ حرّاً، <b>والمعرَّفُ يُؤخذ من أوّل سطرٍ في الفاتورة</b>. فمن يُرجع الصنفَ الثالث
/// <b>يُنقص مخزونَ الأوّل</b> — <b>والكمّيّةُ والقيمةُ صحيحتان فلا يشكو شيء</b>، ويظهر
/// العطلُ في جردٍ بعد شهور.</item>
///
/// <item><b>والثلاثيُّ الضريبيُّ لا يعبر</b>: <c>AddLine</c> يقبل الفئةَ ورمزَ الإعفاء
/// وسببَه منذ نشأته <b>ولا تُمرَّر واحدةٌ منها</b> — فمرتجعُ توريدٍ معفًى <b>يُعاد
/// تصنيفُه أساسيّاً</b> فتُستردّ ضريبةُ مدخلاتٍ لم تُدفع. وهو نقضُ «م-٥ · ن-٥٢»
/// المكتوب في <c>PurchaseOrderService</c>.</item>
///
/// <item><b>ولا فحصَ كمّيّةٍ للصنف</b>: تُشترى عشرٌ وتُرجَع مئة. <b>وحدُّ المستند قائم</b>
/// (<c>DebitNotePostingService</c> يرفض تجاوزَ متبقّي الفاتورة بالقيمة)، <b>لكنّه لا يرى
/// الأصناف</b> — فإرجاعُ مئةٍ من صنفٍ رخيصٍ ضمن فاتورةٍ كبيرةٍ يمرّ.</item>
/// </list>
///
/// <para><b>ولِمَ خدمةٌ لا إصلاحٌ في الشاشة:</b> منطقُ الاختيار والتحقّق في كودِ نافذةٍ
/// <b>لا يبلغه اختبار</b> — وهو نمطُ «الطرفان مقيسان والوسطُ مُستنتَج» المسجَّل في
/// CEEF-229. <b>فأُخرج كي يُقاس</b>، والشاشةُ تستدعيه.</para>
///
/// <para><b>وحدُّه معلَن</b>: المُرجَعُ سابقاً يُحصى <b>بالمنتَج</b> — فسطرٌ بلا منتَج
/// (خدمةٌ أو وصفٌ حرّ) لا يُحصى له متبقٍّ، <b>ويبقى حدُّ المستند بالقيمة هو حارسَه</b>.
/// وهو عينُ ما تفعله شاشةُ الإشعار الدائن على الجهة المقابلة.</para>
/// </summary>
public class DebitNoteDraftBuilder(BrqoshDbContext db)
{
    /// <summary>سطورُ الفاتورة وما بقي من كلٍّ منها بعد الإشعارات المُرحَّلة.</summary>
    public async Task<IReadOnlyList<ReturnableInvoiceLine>> ReturnableLinesAsync(
        long companyId, long invoiceId, CancellationToken ct = default)
    {
        var invoice = await LoadInvoiceAsync(companyId, invoiceId, ct);
        var returned = await ReturnedByProductAsync(companyId, invoiceId, ct);

        var lines = new List<ReturnableInvoiceLine>();
        for (var i = 0; i < invoice.Lines.Count; i++)
        {
            var line = invoice.Lines[i];
            var already = line.ProductId is long pid ? returned.GetValueOrDefault(pid) : 0m;
            lines.Add(new ReturnableInvoiceLine(
                i, line.Description, line.ProductId,
                line.Quantity, already, line.UnitPrice, line.VatCategory));
        }

        return lines;
    }

    /// <summary>
    /// <b>يبني الإشعارَ من سطورٍ مختارةٍ بأعيانها</b> — بمنتَجِ كلِّ سطرٍ وثلاثيّه
    /// الضريبيّ، <b>وبفحصِ كمّيّته</b>.
    /// </summary>
    public async Task<DebitNote> BuildAsync(
        long companyId, long invoiceId, DateOnly noteDate, string reason,
        IReadOnlyCollection<RequestedReturn> requested, CancellationToken ct = default)
    {
        if (requested.Count == 0)
            throw new AccountingException("لا سطرَ واحدٌ للإرجاع.");

        var invoice = await LoadInvoiceAsync(companyId, invoiceId, ct);
        var returnable = await ReturnableLinesAsync(companyId, invoiceId, ct);

        var note = DebitNote.CreateDraft(companyId, invoice.SupplierId, invoiceId, noteDate, reason);

        // (جاهزيّةُ الإطلاق · ح-٣٩) ‏**والعملةُ تُورَّث قبل السطور** — فسعرُ السطر
        // ‏المنسوخُ أدناه بعملة الفاتورة، **وبلا وراثتها يصير الدولارُ ريالاً**.
        // ‏وكان هذا الباني (ح-٣٧) يحمل المنتَجَ والثلاثيَّ ويُغفل العملة.
        if (invoice.IsForeignCurrency)
            note.SetCurrency(invoice.CurrencyCode, invoice.ExchangeRate);

        // ‏(جاهزيّةُ الإطلاق · ح-١٠٥) ‏**ولا يُخمِّن الإشعارُ المدينُ دفعةً من الشاشة.** سطرُ فاتورة الشراء لا يحمل دفعة، والدفعاتُ على
        // ‏الاستلام — فإن بقيت للصنف المتتبَّع دفعتان أو أكثر ولم تُسمَّ واحدة، رُفض الطلبُ بأسمائها وكمّيّاتها؛ وإلا خرج مرتجعُ دفعةٍ
        // ‏مسحوبةٍ أو تالفةٍ من غيرها بلا خطأ، واختلف رصيدُ الدفعات عن الرفّ.
        var inv = new InventoryService(db);
        var products = await inv.LoadProductsForLinesAsync(companyId, invoice.Lines.Select(l => l.ProductId));
        var held = products.Values.Any(p => p.IsTracked && p.IsStockable())
            ? await new DebitNotePostingService(db).ReceivedBatchesStillHeldAsync(
                invoice, (await inv.GetDefaultWarehouseAsync(companyId)).Id, products, ct)
            : [];

        foreach (var want in requested)
        {
            if (want.LineIndex < 0 || want.LineIndex >= returnable.Count)
                throw new AccountingException($"سطرٌ غيرُ موجودٍ في الفاتورة: {want.LineIndex + 1}.");

            var line = returnable[want.LineIndex];
            if (want.Quantity <= 0m)
                throw new AccountingException($"كمّيّةُ الإرجاع يجب أن تكون موجبةً — السطر «{line.Description}».");

            // ‏**والفحصُ بالصنف لا بقيمة المستند** — فحدُّ القيمة أعمى عن الأصناف.
            if (line.ProductId is not null && want.Quantity > line.Returnable)
                throw new AccountingException(
                    $"‏الكمّيّةُ {want.Quantity:N2} تتجاوز المتبقّي {line.Returnable:N2} "
                    + $"من «{line.Description}» (الفاتورة {line.InvoiceQuantity:N2}، "
                    + $"أُرجع سابقاً {line.AlreadyReturned:N2}).");

            var source = invoice.Lines[want.LineIndex];
            if (string.IsNullOrWhiteSpace(want.BatchNumber) && source.ProductId is long pid
                && products.TryGetValue(pid, out var product) && product.IsTracked && product.IsStockable())
            {
                var batches = held.Where(b => b.ProductId == pid && b.BatchNumber is not null && b.Left > 0m).ToList();
                if (batches.Count > 1)
                    throw new AccountingException(
                        $"حدِّد الدفعةَ المُرجَعة من «{source.Description}»: "
                        + string.Join("، ", batches.Select(b => $"{b.BatchNumber} ({b.Left:N2})")) + ".");
            }

            note.AddLine(
                source.Description, want.Quantity, want.UnitPrice ?? source.UnitPrice,
                source.ProductId,
                // ‏(م-٥ · ن-٥٢) ‏**الثلاثيُّ الضريبيُّ يعبر التحويل** — كما في أمر الشراء.
                source.VatCategory, source.TaxExemptionReasonCode, source.TaxExemptionReason,
                batchNumber: want.BatchNumber);
        }

        return note;
    }

    private async Task<PurchaseInvoice> LoadInvoiceAsync(long companyId, long invoiceId, CancellationToken ct)
    {
        var invoice = await db.PurchaseInvoices
            .SingleOrDefaultAsync(i => i.Id == invoiceId && i.CompanyId == companyId, ct)
            ?? throw new AccountingException("فاتورة المشتريات الأصلية غير موجودة.");

        if (invoice.Status != PurchaseInvoiceStatus.Posted)
            throw new AccountingException("لا يصدر إشعار مدين إلا على فاتورة مرحّلة.");

        return invoice;
    }

    /// <summary>ما أُرجع سابقاً من كلّ منتَج على هذه الفاتورة — بالإشعارات المُرحَّلة وحدَها.</summary>
    private async Task<Dictionary<long, decimal>> ReturnedByProductAsync(
        long companyId, long invoiceId, CancellationToken ct)
    {
        var notes = await db.DebitNotes
            .Where(n => n.CompanyId == companyId
                        && n.OriginalPurchaseInvoiceId == invoiceId
                        && n.Status == DebitNoteStatus.Posted)
            .ToListAsync(ct);

        return notes
            .SelectMany(n => n.Lines)
            .Where(l => l.ProductId is not null)
            .GroupBy(l => l.ProductId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
    }
}
