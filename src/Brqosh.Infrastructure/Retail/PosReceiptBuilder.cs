using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Zatca;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Zatca;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Retail;

/// <summary>سطرُ صنفٍ في الإيصال.</summary>
public sealed record PosReceiptLine(string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);

/// <summary>
/// إيصالُ نقطة البيع — <b>فاتورةٌ ضريبيّةٌ مبسّطة</b>: البائعُ ورقمُ تسجيله، والرقمُ والتاريخُ والوقت، والأصناف، والوعاءُ والضريبةُ
/// والإجماليّ، ورمزُ QR أو تنبيهٌ يقول لمَ لا رمز.
/// <para>‏<c>PrintedQr</c> <b>الرمزُ المختارُ للطباعة</b> من <see cref="ZatcaHandOverCopy.MarkFor"/> — لا رمزُ المستند — وباسمٍ غيرِ
/// ‏<c>QrTlvBase64</c> عمداً: الحارسُ (<c>NoCallerReadsTheLocalQrAroundTheHandOverCopy</c>) يعدّ قراءاتِ رمز المستند خارج القارئ الواحد.</para>
/// </summary>
public sealed record PosReceipt(
    string SellerName, string SellerVatNumber, string InvoiceNumber, DateTime IssuedAt,
    IReadOnlyList<PosReceiptLine> Lines, decimal Subtotal, decimal VatAmount, decimal Total,
    string? PrintedQr, string? Notice);

/// <summary>
/// ‏(جاهزيّةُ الإطلاق · ح-١٢٣ · ‏`ui-ux-i18n-printing#8`) <b>الإيصالُ يُبنى هنا ويُطبع في الشاشة.</b>
///
/// <para>‏كانت نافذةُ الإيصال تعرض الاسمَ والرقمَ والمبالغَ والرمز وتُغلق فقط — بلا رقم تسجيل البائع ولا تاريخٍ ولا وقتٍ ولا أصناف،
/// ‏وبلا طباعة. <b>والرمزُ من القارئ الواحد</b> (<see cref="ZatcaHandOverCopy.MarkFor"/>) كبقيّة المطبوعات، فلا يعود الإيصالُ
/// ‏إلى رمزنا صامتاً لمستندٍ لم يُعتمد.</para>
/// </summary>
public static class PosReceiptBuilder
{
    public static PosReceipt Build(SalesInvoice invoice, Company company, ZatcaEInvoiceDocument? zatca)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(company);

        var mark = ZatcaHandOverCopy.MarkFor(zatca, () => null);
        var issuedAt = invoice.PostedAtUtc is DateTime posted
            ? posted.ToLocalTime()
            : invoice.InvoiceDate.ToDateTime(TimeOnly.MinValue);

        return new PosReceipt(
            company.NameAr, company.VatRegistrationNumber, invoice.InvoiceNumber ?? "—", issuedAt,
            invoice.Lines.Select(l => new PosReceiptLine(l.Description, l.Quantity, l.UnitPrice, l.LineTotal)).ToList(),
            invoice.Subtotal, invoice.VatAmount, invoice.GrandTotal(),
            mark.QrTlvBase64, mark.ClearanceNotice);
    }

    /// <summary>
    /// <b>آخرُ إيصالٍ في الوردية</b> — وكانت «طباعة آخر إيصال» تأخذ آخرَ فاتورة نقطة بيعٍ في المنشأة كلِّها، فيطبع كاشيرٌ إيصالَ
    /// ‏زميله. و<c>null</c> لورديةٍ بلا بيع.
    /// </summary>
    public static async Task<PosReceipt?> LastOfShiftAsync(
        BrqoshDbContext db, long companyId, long shiftId, CancellationToken ct = default)
    {
        var invoice = await db.SalesInvoices
            .Where(i => i.CompanyId == companyId && i.PosShiftId == shiftId)
            .OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync(ct);
        if (invoice is null)
            return null;

        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);
        var zatca = await db.ZatcaEInvoiceDocuments.FirstOrDefaultAsync(d =>
            d.CompanyId == companyId && d.DocumentType == ZatcaDocumentType.SalesInvoice
            && d.SourceDocumentId == invoice.Id, ct);

        return Build(invoice, company, zatca);
    }
}
