using Microsoft.EntityFrameworkCore.Infrastructure;
using Brqosh.Infrastructure.Accounting;
using Brqosh.Domain.Accounting;
using Brqosh.Domain.Construction;
using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Treasury;

public record OpenDocumentBalance(
    PaymentAllocationDocumentType DocumentType,
    long DocumentId,
    string DocumentNumber,
    DateOnly DocumentDate,
    decimal OriginalAmount,
    decimal AllocatedAmount,
    decimal RemainingAmount);

/// <summary>استعلام المتبقي على فواتير/مستخلصات وحفظ تخصيصات السندات.</summary>
public class PaymentAllocationService(BrqoshDbContext db)
{
    public async Task<IReadOnlyList<OpenDocumentBalance>> OpenSalesDocumentsAsync(
        long companyId, long customerId, CancellationToken ct = default)
    {
        var invoices = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId
                        && i.CustomerId == customerId
                        && i.Status == SalesInvoiceStatus.Posted
                        && i.PosShiftId == null)
            .ToListAsync(ct);

        var billings = await db.ProgressBillings
            .Where(b => b.CompanyId == companyId
                        && b.CustomerId == customerId
                        && b.Status == ProgressBillingStatus.Posted)
            .ToListAsync(ct);

        var result = new List<OpenDocumentBalance>();
        foreach (var inv in invoices)
        {
            var remaining = await RemainingAsync(companyId, PaymentAllocationDocumentType.SalesInvoice,
                inv.Id, inv.GrandTotal(), ct);
            if (remaining > 0.009m)
            {
                result.Add(new OpenDocumentBalance(
                    PaymentAllocationDocumentType.SalesInvoice, inv.Id,
                    inv.InvoiceNumber ?? inv.Id.ToString(), inv.InvoiceDate,
                    inv.GrandTotal(), inv.GrandTotal() - remaining, remaining));
            }
        }

        foreach (var b in billings)
        {
            var remaining = await RemainingAsync(companyId, PaymentAllocationDocumentType.ProgressBilling,
                b.Id, b.GrandTotal, ct);
            if (remaining > 0.009m)
            {
                result.Add(new OpenDocumentBalance(
                    PaymentAllocationDocumentType.ProgressBilling, b.Id,
                    b.BillingNumber ?? b.Id.ToString(), b.BillingDate,
                    b.GrandTotal, b.GrandTotal - remaining, remaining));
            }
        }

        return result.OrderBy(r => r.DocumentDate).ThenBy(r => r.DocumentNumber).ToList();
    }

    public async Task<IReadOnlyList<OpenDocumentBalance>> OpenPurchaseDocumentsAsync(
        long companyId, long supplierId, CancellationToken ct = default)
    {
        var invoices = await db.PurchaseInvoices
            .Where(i => i.CompanyId == companyId
                        && i.SupplierId == supplierId
                        && i.Status == PurchaseInvoiceStatus.Posted)
            .ToListAsync(ct);

        var result = new List<OpenDocumentBalance>();
        foreach (var inv in invoices)
        {
            var remaining = await RemainingAsync(companyId, PaymentAllocationDocumentType.PurchaseInvoice,
                inv.Id, inv.GrandTotal(), ct);
            if (remaining > 0.009m)
            {
                result.Add(new OpenDocumentBalance(
                    PaymentAllocationDocumentType.PurchaseInvoice, inv.Id,
                    inv.InvoiceNumber ?? inv.Id.ToString(), inv.InvoiceDate,
                    inv.GrandTotal(), inv.GrandTotal() - remaining, remaining));
            }
        }

        return result.OrderBy(r => r.DocumentDate).ThenBy(r => r.DocumentNumber).ToList();
    }

    public async Task ApplyReceiptAllocationsAsync(
        long companyId, long receiptVoucherId, DateOnly date,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> lines,
        CancellationToken ct = default)
    {
        await ValidateAndAddAsync(companyId, date, lines, receiptVoucherId, null, isReceipt: true, ct);
    }

    public async Task ApplyPaymentAllocationsAsync(
        long companyId, long paymentVoucherId, DateOnly date,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> lines,
        CancellationToken ct = default)
    {
        await ValidateAndAddAsync(companyId, date, lines, null, paymentVoucherId, isReceipt: false, ct);
    }

    private async Task ValidateAndAddAsync(
        long companyId, DateOnly date,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> lines,
        long? receiptId, long? paymentId, bool isReceipt, CancellationToken ct)
    {
        if (lines.Count == 0)
            return;

        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            await AcquireDocumentLocksAsync(db.Database, companyId, lines, token);
            await ValidateAndAddCoreAsync(companyId, date, lines, receiptId, paymentId, isReceipt, token);

            // (CEEF-229) ‏**الحفظُ داخل القفل، وإلا لم يحمِ القفلُ شيئاً.**
            //
            // ‏كانت الخدمةُ تُضيف إلى المتتبِّع ويحفظ المستدعي بعده. فأوّلُ صياغةٍ للقفل
            // ‏مرّت بلا أثر: يُؤخذ القفل، يُقرأ المتبقي، تُضاف السطور، **تُغلق المعاملةُ
            // ‏فيُحرَّر القفل**، ثمّ يحفظ المستدعي — فيقرأ المتزاحمُ الرقمَ نفسَه ويكتب.
            // ‏وقيس: `خُصّص 2,300.00 على فاتورةٍ بـ1,150` **بعد إضافة القفل**.
            //
            // ‏والحفظُ هنا لا يُفسد تركيبَ المستدعي: إن كان في معاملةٍ قائمة انضمّ إليها
            // ‏هذا الحفظُ وارتدّ معها، وإلا فُتحت معاملةٌ لهذا وحده.
            await db.SaveChangesAsync(token);
        }, ct);
    }

    private async Task ValidateAndAddCoreAsync(
        long companyId, DateOnly date,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> lines,
        long? receiptId, long? paymentId, bool isReceipt, CancellationToken ct)
    {

        // (CEEF-229 · م1-١٦ / A-13) **ما خُصّص في هذه الدفعة يُطرح كذلك.**
        //
        // `RemainingAsync` يجمع بـ`SumAsync` — أي **استعلامٌ على القاعدة**. والسطورُ المضافةُ
        // قبل قليلٍ في الدفعة نفسها `Added` ولم تُحفَظ، فلا يراها الاستعلام. فسطران على
        // المستند نفسه داخل سندٍ واحد **يقرآن المتبقيَ نفسَه ويمرّان معاً**.
        //
        // وحارسُ «المجموع يساوي مبلغ السند» لا يمنعه: سندٌ بـ2,000 وسطران بـ1,000 مجموعُهما
        // 2,000 — **والفاتورةُ 1,150**. وقيس: `No exception was thrown`.
        //
        // فيُحفظ في الذاكرة ما أُخذ من كلّ مستندٍ في هذه الدفعة، ويُطرح من المتبقي المقروء.
        var takenInThisBatch = new Dictionary<(PaymentAllocationDocumentType, long), decimal>();

        foreach (var line in lines)
        {
            if (line.Amount <= 0)
                throw new AccountingException("مبلغ التخصيص يجب أن يكون أكبر من صفر.");

            var original = await OriginalAmountAsync(companyId, line.Type, line.DocumentId, ct);
            var remaining = await RemainingAsync(companyId, line.Type, line.DocumentId, original, ct);

            var key = (line.Type, line.DocumentId);
            if (takenInThisBatch.TryGetValue(key, out var alreadyTaken))
                remaining -= alreadyTaken;
            // (CEEF-206) بلا تفاوت: كل المبالغ مقرّبة لخانتين أصلاً، فالتفاوت 0.01 كان
            // يسمح بتجاوز المتبقي بهللة لكل سطر — وتخصيصات متتابعة تُراكم الفارق حتى
            // يظهر مستند مسدَّداً بأكثر من قيمته في تقارير الذمم.
            if (line.Amount > remaining)
                throw new AccountingException(
                    $"التخصيص على {line.Number} ({line.Amount:N2}) يتجاوز المتبقي {remaining:N2}.");

            takenInThisBatch[key] = alreadyTaken + line.Amount;

            if (isReceipt)
            {
                db.PaymentAllocationLines.Add(PaymentAllocationLine.ForReceipt(
                    companyId, receiptId!.Value, line.Type, line.DocumentId, line.Number, line.Amount, date));
            }
            else
            {
                db.PaymentAllocationLines.Add(PaymentAllocationLine.ForPayment(
                    companyId, paymentId!.Value, line.Type, line.DocumentId, line.Number, line.Amount, date));
            }
        }
    }

    /// <summary>
    /// (CEEF-229) ‏<b>قفلُ التخصيص — لكلّ (منشأة · مستند).</b>
    ///
    /// <para>الخدمةُ تقرأ المتبقيَ بـ<c>SumAsync</c> ثمّ تكتب. وبلا قفلٍ يقرأ متزاحمان
    /// المتبقيَ نفسَه ويكتبان معاً، <b>فتُسدَّد الفاتورةُ بأكثر من قيمتها</b> ويصير رصيدُ
    /// العميل سالباً بلا مقابلٍ في الأستاذ.</para>
    ///
    /// <para><b>وكانت الحمايةُ قائمةً بالعَرَض:</b> <c>ReceiptVoucherPostingService</c> يأخذ
    /// قفلَ ترقيم المستندات داخل معاملته فيُسلسِل كلَّ سنداتِ المنشأة في تلك السنة. وذلك
    /// مقيسٌ في <c>م1-١٦</c>، <b>ولم يُضَف قفلٌ حينها لأنّ عضّتَه لم تكن تُقاس</b>: قفلٌ
    /// يستره قفلٌ آخر لا يُثبَت نفعُه.</para>
    ///
    /// <para><b>وقياسُها بُني الآن</b>: تُستدعى الخدمةُ مباشرةً بلا مسار السند — وهو مسارٌ
    /// حقيقيّ يسلكه سطحُ API وشاشتان — فينكشف السباقُ عارياً. والمقيس بنافذةٍ موسَّعة:
    /// <c>خُصّص 2,300.00 على فاتورةٍ بـ1,150</c>.</para>
    ///
    /// <para><b>والقفلُ لكلّ مستندٍ لا للمنشأة</b>: تخصيصٌ على فاتورةٍ لا ينتظر تخصيصاً على
    /// غيرها. <b>ويُؤخذ بترتيبٍ ثابت</b> حين تحمل الدفعةُ مستنداتٍ عدّة — فدفعتان تأخذان
    /// قفلَين بترتيبين متعاكسين تتقافلان.</para>
    ///
    /// <para>والاشتقاقُ بـFNV-1a لا <c>GetHashCode</c> — <b>فالأخيرُ مُعشّى لكلّ عملية
    /// تشغيل</b>، فجهازان يحسبان مفتاحين مختلفين للمستند نفسه ولا يتزاحمان على قفلٍ واحد.
    /// وهو درسُ <c>DocumentSequenceLock</c> بحرفه.</para>
    /// </summary>
    private static async Task AcquireDocumentLocksAsync(
        DatabaseFacade database, long companyId,
        IReadOnlyList<(PaymentAllocationDocumentType Type, long DocumentId, string Number, decimal Amount)> lines,
        CancellationToken ct)
    {
        if (!database.IsNpgsql())
            return;

        if (database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "تخصيصُ المدفوعات يجري خارج معاملة — والقفلُ الاستشاريّ يُحرَّر فوراً "
                + "فلا يحمي قراءةَ المتبقي ولا كتابةَ التخصيص.");

        var keys = lines
            .Select(l => KeyFor(l.Type, l.DocumentId))
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        foreach (var key in keys)
        {
            await database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0}::int, {1}::int)",
                new object[] { companyId, key },
                ct);
        }
    }

    /// <summary>مفتاحٌ ثابتٌ من نوع المستند ورقمه — FNV-1a، للسبب المشروح أعلاه.</summary>
    internal static int KeyFor(PaymentAllocationDocumentType type, long documentId)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;

            var hash = offset;
            foreach (var b in System.Text.Encoding.UTF8.GetBytes($"ALLOC|{type}|{documentId}"))
            {
                hash ^= b;
                hash *= prime;
            }

            return (int)hash;
        }
    }

    public async Task ReverseVoucherAllocationsAsync(
        long companyId, long? receiptVoucherId, long? paymentVoucherId, CancellationToken ct = default)
    {
        IQueryable<PaymentAllocationLine> query = db.PaymentAllocationLines.Where(a => a.CompanyId == companyId);
        if (receiptVoucherId is long rid)
            query = query.Where(a => a.ReceiptVoucherId == rid);
        else if (paymentVoucherId is long pid)
            query = query.Where(a => a.PaymentVoucherId == pid);
        else
            return;

        var rows = await query.ToListAsync(ct);
        db.PaymentAllocationLines.RemoveRange(rows);
    }

    private async Task<decimal> RemainingAsync(
        long companyId, PaymentAllocationDocumentType type, long documentId, decimal original, CancellationToken ct)
    {
        var allocated = await db.PaymentAllocationLines
            .Where(a => a.CompanyId == companyId
                        && a.DocumentType == type
                        && a.DocumentId == documentId)
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;

        // خصم إشعارات الدائن من أصل فاتورة المبيعات
        if (type == PaymentAllocationDocumentType.SalesInvoice)
        {
            var credited = await db.CreditNotes
                .Where(n => n.CompanyId == companyId
                            && n.OriginalInvoiceId == documentId
                            && n.Status == CreditNoteStatus.Posted)
                .SumGrandTotalAsync(ct);
            original = Math.Max(0m, original - credited);
        }

        return Math.Round(Math.Max(0m, original - allocated), 2, MidpointRounding.AwayFromZero);
    }

    private async Task<decimal> OriginalAmountAsync(
        long companyId, PaymentAllocationDocumentType type, long documentId, CancellationToken ct) =>
        type switch
        {
            PaymentAllocationDocumentType.SalesInvoice =>
                (await db.SalesInvoices.SingleAsync(i => i.CompanyId == companyId && i.Id == documentId, ct)).GrandTotal(),
            PaymentAllocationDocumentType.PurchaseInvoice =>
                (await db.PurchaseInvoices.SingleAsync(i => i.CompanyId == companyId && i.Id == documentId, ct)).GrandTotal(),
            PaymentAllocationDocumentType.ProgressBilling =>
                (await db.ProgressBillings.SingleAsync(b => b.CompanyId == companyId && b.Id == documentId, ct)).GrandTotal,
            _ => throw new AccountingException("نوع مستند التخصيص غير مدعوم."),
        };
}
