using Brqosh.Domain.Accounting;
using Brqosh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Purchasing;

/// <summary>
/// (CEEF-229 · م1-١٩) <b>حسابا دورة الشراء الجديدان — يُنشآن للمنشآت القائمة أيضاً.</b>
///
/// <para><b>ولماذا لا يكفي إضافتُهما إلى القالب:</b> <c>SeedChartOfAccountsAsync</c> يعود عند
/// أوّل حسابٍ موجود، فصفٌّ جديدٌ في القالب <b>لا يصل قاعدةً حيّةً أبداً</b>. وحلُّ الحساب
/// بـ<c>SingleAsync</c> يرمي <c>InvalidOperationException</c> خامّاً لا رسالةً عربية —
/// فيصير أوّلُ استلامٍ بعد التحديث انهياراً غامضاً.</para>
///
/// <para>والنمطُ منقولٌ عن <c>FxRevaluationService.EnsureFxAccountsAsync</c>: يُضمَن عند
/// موضع الاستعمال لا عند الإقلاع، فلا يعتمد على ترتيبٍ في التهيئة.</para>
/// </summary>
internal static class PurchaseAccountProvisioning
{
    /// <summary>حسابُ «بضاعة مستلمة غير مفوترة» — التزامٌ متداول تحت المجموعة 21.</summary>
    internal static Task<Account> GoodsReceivedNotInvoicedAsync(
        BrqoshDbContext db, long companyId, CancellationToken ct = default) =>
        EnsureAsync(db, companyId, SaudiChartOfAccounts.SystemCodes.GoodsReceivedNotInvoiced,
            "بضاعة مستلمة غير مفوترة", "Goods Received Not Invoiced",
            AccountType.Liability, "21", isSystem: true, ct);

    /// <summary>حسابُ فروق أسعار المشتريات — مصروفٌ تحت المجموعة 51، وغيرُ نظاميّ عمداً.</summary>
    internal static Task<Account> PriceVarianceAsync(
        BrqoshDbContext db, long companyId, CancellationToken ct = default) =>
        EnsureAsync(db, companyId, "5108",
            "فروق أسعار المشتريات", "Purchase Price Variance",
            AccountType.Expense, "51", isSystem: false, ct);

    private static async Task<Account> EnsureAsync(
        BrqoshDbContext db, long companyId, string code, string nameAr, string nameEn,
        AccountType type, string parentCode, bool isSystem, CancellationToken ct)
    {
        var existing = await db.Accounts
            .SingleOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code, ct);
        if (existing is not null)
            return existing;

        var parent = await db.Accounts
            .SingleOrDefaultAsync(a => a.CompanyId == companyId && a.Code == parentCode, ct)
            ?? throw new AccountingException(
                $"المجموعة «{parentCode}» غير موجودة في دليل الحسابات — لا يمكن إنشاء «{nameAr}».");

        var account = Account.Create(code, nameAr, nameEn, type, parent.Id,
            isGroup: false, isSystem: isSystem, companyId);

        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account;
    }
}
