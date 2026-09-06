using Brqosh.Domain.Accounting;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Domain.Retail;
using Brqosh.Domain.Tax;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Retail;

public record PosSaleResult(
    SalesInvoice Invoice,
    long ShiftId,
    ZatcaEInvoiceDocument? ZatcaDocument,
    string? ZatcaMessage);

/// <summary>بيع نقطة البيع — ترحيل فاتورة + ZATCA + إرسال تلقائي في المحاكاة.</summary>
public class PosSaleService(BrqoshDbContext db)
{
    public async Task<PosSaleResult> SellAsync(
        long companyId, long shiftId, long? customerId,
        IReadOnlyList<(long ProductId, decimal Qty)> lines, bool isVatRegistered,
        PosPaymentMethod paymentMethod, string username, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();
        SalesInvoice invoice = null!;
        await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var shift = await db.PosShifts.SingleAsync(s => s.CompanyId == companyId && s.Id == shiftId, token);

            long effectiveCustomerId;
            if (customerId is long cid && cid > 0)
            {
                effectiveCustomerId = cid;
            }
            else
            {
                var walkIn = await db.Customers
                    .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.NameAr == "عميل نقدي", token);
                if (walkIn is null)
                {
                    walkIn = Customer.Create(companyId, "عميل نقدي", "");
                    db.Customers.Add(walkIn);
                    await db.SaveChangesAsync(token);
                }
                effectiveCustomerId = walkIn.Id;
            }

            // (CEEF-228) كان الوسيط `isVatRegistered` يُستقبَل ولا يُستعمل ولو مرّة في جسم الدالة،
            // فتُحصَّل 15% من منشأةٍ بلا رقم ضريبيّ ويُقيَّد التزامٌ مُختلَق تجاه الهيئة.
            // منشأةٌ غير مسجَّلة في ضريبة القيمة المضافة لا تُصدر ضريبةً أصلاً: الفئة «خارج النطاق».
            var posVatCategory = isVatRegistered ? VatCategory.Standard : VatCategory.OutOfScope;

            // (CEEF-229 · م1-١ب الشقّ الثاني) والفئةُ «خارج النطاق» تقول سببَها: رمزٌ نظاميّ
            // ونصٌّ يكتبه المكلَّف — والنصُّ هنا معلومٌ يقيناً، فلا يُسأل عنه الكاشير.
            //
            // وهذه المنشأةُ خارج تفويض الفوترة الإلكترونية أصلاً (لا رقمَ ضريبيّ لها، فحارسُ
            // هوية البائع يمنع توليدَ مستندٍ لها). لكنّ السببَ يبقى مكتوباً في السطر — فلو
            // سُجّلت لاحقاً بقيت مستنداتُها القديمة صادقةً عن نفسها.
            var posReasonCode = isVatRegistered ? null : VatExemptionReason.OutOfScopeCode;
            var posReasonText = isVatRegistered
                ? null
                : "المنشأة غير مسجَّلة في ضريبة القيمة المضافة.";

            invoice = SalesInvoice.CreateDraft(companyId, effectiveCustomerId, DateOnly.FromDateTime(DateTime.Today));
            foreach (var (productId, qty) in lines)
            {
                var product = await db.Products.SingleAsync(p => p.Id == productId, token);
                invoice.AddLine(product.NameAr, qty, product.SalePrice, productId, posVatCategory,
                    posReasonCode, posReasonText);
            }

            invoice.MarkPosSale(shiftId, paymentMethod);

            // (CEEF-230 · ط6-٧) ‏**والتوليدُ يقع هنا الآن** — داخل `PostCoreAsync` نفسِه،
            // ‏فلا يُستدعى بعده. وكان يُستدعى صراحةً في آخر هذه المعاملة، **وهو ما جعله
            // ‏عادةً تُنسخ لا مساراً يُسلَك**: نسختْه نقطةُ البيع ونسيته الزيارةُ الطبّية
            // ‏وحسابُ النزيل والفوترةُ الدورية.
            await new Sales.SalesInvoicePostingService(db).PostCoreAsync(invoice, token);
            shift.RecordSale(invoice.GrandTotal(), paymentMethod);
            await db.SaveChangesAsync(token);

            await new AuditService(db).LogAsync(companyId, username, "بيع POS",
                "SalesInvoice", invoice.InvoiceNumber ?? invoice.Id.ToString(),
                $"مبلغ: {invoice.GrandTotal():N2} — {PaymentLabel(paymentMethod)}");

            return invoice;
        }, ct);

        var zatcaDoc = await ZatcaPostingOrchestrator.SubmitAfterCommitAsync(
            db, companyId, ZatcaDocumentType.SalesInvoice, invoice.Id, ct);

        // ‏(م-٢ · ن-٠٤) ‏**جدولُ تسمياتٍ رابعٌ كان يكذب في الاتجاهين — وحُذف.**
        //
        // ‏كان هنا `switch` يُطبع على إيصال العميل:
        // ‏  Reported ⟸ «تم الإبلاغ للهيئة **(محاكاة)**»   ← وهو النجاحُ الحقيقيّ، يُوسَم محاكاةً
        // ‏  Cleared  ⟸ «تم الاعتماد **(محاكاة)**»         ← كذلك
        // ‏  _        ⟸ «تم الإرسال»                       ← يبتلع **Rejected** و**SimulatedLocally**
        //
        // ‏فالفاتورةُ التي رفضتها الهيئةُ تُطبع للعميل «تم الإرسال»، والتي أُبلغت فعلاً
        // ‏تُطبع «(محاكاة)». **والاتجاهان كلاهما خطأ في مستندٍ ضريبيّ.**
        //
        // ‏و`NameAr()` في `ZatcaEnums` هو الجدولُ الصادق: يُسمّي كلَّ حالةٍ باسمها،
        // ‏و«محاكاة محلية — لم يُرسل للهيئة» صريحةٌ فيه. **وكان موجوداً ولا يُستدعى هنا.**
        // ‏فحُذف الرابعُ وبقي الأوّل — لا جدولان لشيءٍ واحدٍ يفترقان.
        string? zatcaMessage = zatcaDoc?.ComplianceStatus.NameAr();

        return new PosSaleResult(invoice, shiftId, zatcaDoc, zatcaMessage);
    }

    private static string PaymentLabel(PosPaymentMethod method) => method switch
    {
        PosPaymentMethod.Cash => "نقداً",
        PosPaymentMethod.Card => "بطاقة",
        PosPaymentMethod.Mixed => "مختلط",
        _ => method.ToString(),
    };
}
