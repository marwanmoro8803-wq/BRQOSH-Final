using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Sales;
using Brqosh.Domain.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Brqosh.Infrastructure.Persistence;

/// <summary>
/// (دفاترٌ بغير الريال) <b>مستندٌ بسعر صرفٍ ١ يُوسَم بعملة دفاتر منشأته عند الحفظ.</b>
///
/// <para><b>ما كان:</b> كلُّ مستندٍ يُنشأ بـ<c>SAR</c> أصلاً، ومنشأةٌ دفاترُها بالدولار تُصدر فاتورةً بلا تعديل فتُوسَم «ريالاً» وتُطبع
/// وتُخصَّص عليها السنداتُ بعملةٍ لا تخصّها — والمستنداتُ تُنشأ من أكثر من عشرين مساراً (واجهاتٌ وعقودٌ ونقاطُ بيعٍ وتصنيعٌ)، فلا يصحّ
/// أن يتذكّر كلٌّ منها عملةَ الدفاتر.</para>
///
/// <para><b>فالحفظُ يصحّح الوسمَ مرّةً:</b> ما كان <c>SAR</c> وسعرُه ١ وعملةُ دفاتر منشأته غيرُه يُوسَم بعملتها. ولا يمسّ مستنداً بعملةٍ
/// أجنبيّةٍ صريحة (سعرُه ≠ ١)، ولا منشأةً دفاترُها بالريال (لا استعلامَ أصلاً).</para>
/// </summary>
public sealed class BooksCurrencyInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is BrqoshDbContext db)
        {
            var cache = new Dictionary<long, string>();
            foreach (var (companyId, align) in Pending(db))
            {
                if (!cache.TryGetValue(companyId, out var code))
                    cache[companyId] = code = db.Companies.IgnoreQueryFilters().Where(c => c.Id == companyId)
                        .Select(c => c.FunctionalCurrencyCode).FirstOrDefault() ?? "SAR";
                if (code != "SAR")
                    align(code);
            }
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is BrqoshDbContext db)
        {
            var cache = new Dictionary<long, string>();
            foreach (var (companyId, align) in Pending(db))
            {
                if (!cache.TryGetValue(companyId, out var code))
                    cache[companyId] = code = await db.Companies.IgnoreQueryFilters().Where(c => c.Id == companyId)
                        .Select(c => c.FunctionalCurrencyCode).FirstOrDefaultAsync(cancellationToken) ?? "SAR";
                if (code != "SAR")
                    align(code);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>المستنداتُ المضافةُ أو المعدَّلةُ التي وسمُها <c>SAR</c> وسعرُها ١ — مع ما يضبط وسمَها.</summary>
    private static List<(long CompanyId, Action<string> Align)> Pending(DbContext db)
    {
        var list = new List<(long, Action<string>)>();
        foreach (var e in db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            switch (e.Entity)
            {
                case SalesInvoice d when d.CurrencyCode == "SAR" && d.ExchangeRate == 1m: list.Add((d.CompanyId, d.AlignToBooksCurrency)); break;
                case PurchaseInvoice d when d.CurrencyCode == "SAR" && d.ExchangeRate == 1m: list.Add((d.CompanyId, d.AlignToBooksCurrency)); break;
                case CreditNote d when d.CurrencyCode == "SAR" && d.ExchangeRate == 1m: list.Add((d.CompanyId, d.AlignToBooksCurrency)); break;
                case DebitNote d when d.CurrencyCode == "SAR" && d.ExchangeRate == 1m: list.Add((d.CompanyId, d.AlignToBooksCurrency)); break;
                case ReceiptVoucher d when d.CurrencyCode == "SAR" && d.ExchangeRate == 1m: list.Add((d.CompanyId, d.AlignToBooksCurrency)); break;
                case PaymentVoucher d when d.CurrencyCode == "SAR" && d.ExchangeRate == 1m: list.Add((d.CompanyId, d.AlignToBooksCurrency)); break;
            }
        }

        return list;
    }
}
