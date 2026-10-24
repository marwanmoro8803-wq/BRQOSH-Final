using Brqosh.Domain.Accounting;
using Brqosh.Domain.Companies;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Companies;

/// <summary>
/// (أسئلةُ المالك 2026-10-24 · العملة) <b>عملةُ المنشأة لا تتغيّر بعد أن تُقيَّد بها دفاترُها.</b>
///
/// <para><b>ما كان:</b> شاشةُ الإعدادات تستدعي <c>Company.SetCurrencies</c> مباشرةً — فتبديلُ «SAR» إلى «USD» على منشأةٍ فيها قيودٌ
/// مرحَّلة يُعيد تسميةَ كلِّ مبلغٍ قائمٍ بالدولار دون أن يتغيّر رقمٌ واحد: دفاترٌ بآلاف الريالات تُقرأ آلافَ الدولارات،
/// ولا قيدَ ولا أثرَ تدقيقٍ على ذلك.</para>
///
/// <para><b>والحارس</b> (على نسق <see cref="BusinessProfileChangeService"/>): التغييرُ للمدير وحده، ويُرفض متى وُجدت للمنشأة
/// قيودٌ أو مستنداتٌ (فواتير وسندات) بأيّ حالة، ويُدقَّق متى مرّ. أمّا الإعادةُ إلى القيمة نفسِها فلا شيء.</para>
/// </summary>
public class CompanyCurrencyChangeService(BrqoshDbContext db)
{
    public async Task ChangeAsync(
        long companyId, string baseCurrencyCode, string functionalCurrencyCode, CancellationToken ct = default)
    {
        SecurityContext.EnsureAdministrator();
        var company = await db.Companies.SingleAsync(c => c.Id == companyId, ct);

        var previousBase = company.BaseCurrencyCode;
        var previousFunctional = company.FunctionalCurrencyCode;

        // ‏التطبيعُ والتحقّقُ من الكتالوج يجريان في النطاق نفسِه، فلا يُقارَن نصٌّ خامٌ بنصٍّ مطبَّع.
        var newBase = Company.NormalizeCurrency(baseCurrencyCode);
        var newFunctional = Company.NormalizeCurrency(functionalCurrencyCode);
        if (newBase == previousBase && newFunctional == previousFunctional)
            return;

        if (await HasBooksAsync(companyId, ct))
            throw new CurrencyLockedException(previousFunctional);

        company.SetCurrencies(newBase, newFunctional);
        await db.SaveChangesAsync(ct);
        await new AuditService(db).LogAsync(companyId, SecurityContext.ResolveUsername(), "تعديل", "Company",
            companyId.ToString(),
            $"العملة: عرض {previousBase}←{newBase} · قياس {previousFunctional}←{newFunctional}",
            null, ct);
    }

    /// <summary>هل قُيّد في دفاتر المنشأة شيءٌ بعملتها؟ — قيودٌ أو مستنداتٌ ماليّة، بأيّ حالة.</summary>
    internal async Task<bool> HasBooksAsync(long companyId, CancellationToken ct = default) =>
        await db.JournalEntries.AnyAsync(x => x.CompanyId == companyId, ct)
        || await db.SalesInvoices.AnyAsync(x => x.CompanyId == companyId, ct)
        || await db.PurchaseInvoices.AnyAsync(x => x.CompanyId == companyId, ct)
        || await db.ReceiptVouchers.AnyAsync(x => x.CompanyId == companyId, ct)
        || await db.PaymentVouchers.AnyAsync(x => x.CompanyId == companyId, ct);
}

/// <summary>رفضُ تغيير العملة لأنّ الدفاترَ قُيّدت بها — يحمل العملةَ الحاليّة لتعرضها الشاشةُ.</summary>
public sealed class CurrencyLockedException(string currentCurrency)
    : AccountingException($"لا تُغيَّر عملة المنشأة ({currentCurrency}) بعد تسجيل قيودٍ أو مستنداتٍ بها — " +
                          "المبالغُ المخزَّنةُ تبقى أرقامَها فتُقرأ بعملةٍ لم تُقيَّد بها. أنشئ منشأةً جديدةً بالعملة المطلوبة.")
{
    public string CurrentCurrency { get; } = currentCurrency;
}
