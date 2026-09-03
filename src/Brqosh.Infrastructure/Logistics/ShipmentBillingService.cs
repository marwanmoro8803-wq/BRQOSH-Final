using Brqosh.Domain.Accounting;
using Brqosh.Domain.Logistics;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Sales;
using Brqosh.Infrastructure.Security;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Logistics;

/// <summary>حصيلةُ فوترةِ أجورٍ — <b>و<c>InvoiceId</c> لاغٍ حين لا شحنةَ تستحقّ الفوترة</b>.</summary>
public record ShipmentBillingResult(long? InvoiceId, int Shipments, decimal Freight);

/// <summary>
/// (CEEF-232 · ل-١) <b>أجرةُ الشحن — إيرادُ القطاع كلِّه، ولم تكن تبلغ الأستاذ أبداً.</b>
///
/// <para><b>ما كان:</b> <c>Shipment.FreightCharge</c> يُقرأ في موضعين لا ثالثَ لهما —
/// <b>عرضٌ على الشاشة</b>، و<b>حسابُ ربحيّة الخطّ</b> في <c>DeliveryTripService</c>.
/// <b>ولا خدمةَ ترحيلٍ تمسّه، ولا فاتورةَ تحمله.</b></para>
///
/// <para><b>والأثرُ أنّ القطاع يظهر خاسراً بكامل أجرته:</b> تكاليفُ الرحلات تُرحَّل على
/// <c>5206</c> (<c>DeliveryTripService.SettleAsync</c>) <b>والإيرادُ خارجَ الدفاتر</b>.
/// فقائمةُ الدخل تحمل مصروفَ اللوجستيات بلا إيراده، <b>وتقريرُ ربحيّة الخطّ يطرح مصروفاً
/// مُرحَّلاً من إيرادٍ غيرِ مُرحَّل</b> — فلا يُطابق قائمةَ الدخل أبداً ولا يُمكن تسويتُه.</para>
///
/// <para><b>والفاتورةُ هي الطريق — لا قيدٌ مباشر:</b> أجرةُ الشحن <b>توريدٌ خاضعٌ نظاماً</b>
/// يلزمه مستندُ ضريبةٍ إلكترونيّ. <b>وقيدٌ على الذمم بلا مستندٍ يكسر هويّةَ هـ-١</b> —
/// دفترُ العملاء يُبنى من المستندات — <b>ويُخرج الشحنَ من إقرار الضريبة</b>. فالفاتورةُ
/// تُصلح الثلاثة معاً: الأستاذَ والدفترَ والإقرار.</para>
///
/// <para><b>والأجرةُ تُستحقّ بالتسليم — وبالإرجاع كذلك.</b> والنطاقُ يقولها صراحةً
/// (<c>AReturnedShipment_CollectsNothingButKeepsItsFreight</c>): <b>الناقلُ نقل، والإرجاعُ
/// لا يُسقط أجرته</b>. <b>وشحنةٌ أُلغيت قبل الانطلاق لا أجرةَ لها</b>.</para>
///
/// <para><b>وهويّةُ ل-١:</b> مجموعُ أجور الشحنات المفوترة = إيرادُ الشحن في الأستاذ،
/// <b>وكلُّ شحنةٍ تُفوتَر مرّةً واحدة</b>.</para>
/// </summary>
public class ShipmentBillingService(BrqoshDbContext db)
{
    /// <summary>
    /// يُصدر فاتورةَ أجورٍ واحدةً لتاجرٍ عن كلّ شحناته المستحقّة غيرِ المفوترة.
    /// </summary>
    /// <remarks>
    /// <b>وفاتورةٌ واحدةٌ لا فاتورةٌ لكلّ بوليصة</b>: تاجرٌ يُرسل مئةَ شحنةٍ في الشهر
    /// <b>لا يريد مئةَ فاتورة</b> — والسطرُ لكلّ بوليصةٍ يُبقي التفصيلَ الذي يُنازَع فيه.
    /// </remarks>
    public async Task<ShipmentBillingResult> BillShipperAsync(
        long companyId, long shipperCustomerId, DateOnly on, CancellationToken ct = default)
    {
        SecurityContext.EnsureCanPost();

        var result = await UnitOfWork.ExecuteInTransactionAsync(db, async token =>
        {
            var due = await db.Shipments
                .Where(s => s.CompanyId == companyId
                            && s.ShipperCustomerId == shipperCustomerId
                            && s.FreightInvoiceId == null
                            && s.FreightCharge > 0m
                            && (s.Status == ShipmentStatus.Delivered
                                || s.Status == ShipmentStatus.Returned))
                .OrderBy(s => s.ShipDate).ThenBy(s => s.Id)
                .ToListAsync(token);

            // ‏**ولا فاتورةَ بلا سطر** — وتشغيلةٌ لا عملَ لها تصمت ولا تعتذر.
            if (due.Count == 0)
                return new ShipmentBillingResult(null, 0, 0m);

            var invoice = SalesInvoice.CreateDraft(companyId, shipperCustomerId, on);
            foreach (var shipment in due)
            {
                invoice.AddLine(
                    $"أجرة شحن — بوليصة {shipment.WaybillNumber} إلى {shipment.DestinationCity}",
                    1m, shipment.FreightCharge);
            }

            await new SalesInvoicePostingService(db).PostCoreAsync(invoice, token);

            foreach (var shipment in due)
                shipment.LinkFreightInvoice(invoice.Id);

            await db.SaveChangesAsync(token);

            return new ShipmentBillingResult(
                invoice.Id, due.Count, due.Sum(s => s.FreightCharge));
        }, ct);

        // ‏**والإبلاغُ بعد الـcommit** — على اصطلاح المغادرة في الضيافة: فاتورةُ الأجرة
        // ‏فاتورةُ ضريبةٍ كاملة، **والتاجرُ يُطالَب بها**.
        if (result.InvoiceId is long invoiceId)
        {
            await ZatcaPostingOrchestrator.SubmitAfterCommitAsync(
                db, companyId, ZatcaDocumentType.SalesInvoice, invoiceId, ct);
        }

        return result;
    }
}
