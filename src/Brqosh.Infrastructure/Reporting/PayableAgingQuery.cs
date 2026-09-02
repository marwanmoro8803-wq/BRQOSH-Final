using Brqosh.Domain.Purchasing;
using Brqosh.Domain.Treasury;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Infrastructure.Reporting;

/// <summary>سطرُ أعمارٍ لموردٍ واحد — والمجموعُ هو رصيدُه في الدفتر المساعد.</summary>
public record PayableAgingRow(
    long SupplierId,
    string SupplierNameAr,
    decimal Current,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal UnappliedDebit)
{
    /// <summary>
    /// مجموعُ الخانات — <b>ويجب أن يساوي رصيدَ المورد في الدفتر المساعد</b>.
    ///
    /// <para>وهي هويّةُ <c>هـ-٢٠</c>، مرآةُ <c>هـ-١٤</c> على الجانب الدائن. <b>وتقريرُ
    /// أعمارٍ لا يُطابق كشفَ حساب المورد لا يُصدَّق في شيء</b>.</para>
    /// </summary>
    public decimal Total => Current + Days31To60 + Days61To90 + Over90 - UnappliedDebit;
}

/// <summary>
/// (CEEF-231 · ط4-٣ب) <b>أعمارُ ديون الموردين — والجانبُ الآخر كان وحدَه مبنياً.</b>
///
/// <para><b>ما كان:</b> أعمارُ العملاء بُنيت في <c>ط4-٣</c>، <b>وجانبُ الموردين بقي بلا
/// شيء</b>. فالمالكُ يعرف مَن عليه له ولا يعرف <b>لمن هو عليه ومنذ متى</b>.</para>
///
/// <para><b>وهو أخطرُ من جانب العملاء لا أهون</b>: تأخّرُ العميل يُكلّف مالاً لم يصل،
/// <b>وتأخّرُ السداد يُكلّف المنشأةَ علاقتَها بمورّدها</b> — يُوقف التوريد، وتُسحب مهلةُ
/// الائتمان، <b>وتتوقّف المنشأةُ عن العمل لأنّها لم تعرف أنّ فاتورةً بلغت التسعين</b>.
/// وفي نظام الفوترة الإلكترونية كذلك: <b>ضريبةُ مدخلاتٍ خُصمت عن فاتورةٍ لم تُسدَّد</b>
/// تُستردّ بعد اثني عشر شهراً، ولا يُعرف موعدُها بغير هذا التقرير.</para>
///
/// <para><b>والقراراتُ الثلاثةُ هي عينُها في جانب العملاء</b> — وذلك مقصود: العمرُ من
/// تاريخ المستند (فلا مهلةَ سدادٍ في النظام، <b>و«متأخّر» ادّعاءٌ بلا سند</b>) ·
/// والسدادُ بالتخصيص ثمّ بالأقدم · <b>والفائضُ يُعرض مديناً</b>. فتقريران بمنطقين
/// مختلفين لجانبَي الميزان <b>يجعلان الفرقَ بينهما غيرَ مفهوم</b>.</para>
/// </summary>
public class PayableAgingQuery(BrqoshDbContext db)
{
    private sealed record OpenDocument(DateOnly Date, long DocumentId, decimal Amount)
    {
        public decimal Remaining { get; set; } = Amount;
    }

    public async Task<IReadOnlyList<PayableAgingRow>> ExecuteAsync(
        long companyId, DateOnly asOf, CancellationToken ct = default)
    {
        // ‏**والمرجعُ مؤرَّخٌ كالخانات** — وهو ما أخرجته طفرةٌ نجت في جانب العملاء:
        // ‏رصيدُ «الآن» يُقابَل بخاناتِ «٣٠ يونيو» فيُخرج فرقاً يُنسب إلى التقرير وهو
        // ‏في المرجع. **فوسيطُ التاريخ أُضيف إلى `SupplierBalanceQuery` لأجل هذا.**
        var balances = await new SupplierBalanceQuery(db).AllBalancesAsync(companyId, asOf);

        var names = await db.Suppliers.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.CompanyId == companyId)
            .ToDictionaryAsync(s => s.Id, s => s.NameAr, ct);

        var invoices = await db.PurchaseInvoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.Status == PurchaseInvoiceStatus.Posted
                        && i.InvoiceDate <= asOf)
            .Select(i => new { i.Id, i.SupplierId, i.InvoiceDate, i.Subtotal, i.VatAmount })
            .ToListAsync(ct);

        var allocations = await db.PaymentAllocationLines.AsNoTracking()
            .Where(a => a.CompanyId == companyId
                        && a.DocumentType == PaymentAllocationDocumentType.PurchaseInvoice
                        && a.AllocationDate <= asOf)
            .ToListAsync(ct);

        var rows = new List<PayableAgingRow>();

        foreach (var (supplierId, balance) in balances)
        {
            if (balance == 0m) continue;

            var open = invoices
                .Where(i => i.SupplierId == supplierId)
                .Select(i => new OpenDocument(i.InvoiceDate, i.Id, i.Subtotal + i.VatAmount))
                .OrderBy(d => d.Date).ThenBy(d => d.DocumentId)
                .ToList();

            var invoiced = open.Sum(d => d.Amount);

            // ‏**وكلُّ ما ليس فاتورةَ مشترياتٍ يُشتقّ من الفرق**: سنداتُ صرفٍ وشيكاتٌ
            // ‏وإشعاراتُ مدينٍ ومحتجزُ مقاولين. **فالتقريرُ يُطابق الدفترَ المساعد بالبناء
            // ‏لا بالمصادفة**، ولا يُعيد جمعَ ما جمعه المرجع.
            var debit = invoiced - balance;

            ApplyExplicit(open, allocations);

            var unapplied = ApplyOldestFirst(open, RemainingDebit(debit, open, invoiced));

            var buckets = Bucket(open, asOf);
            rows.Add(new PayableAgingRow(supplierId,
                names.TryGetValue(supplierId, out var name) ? name : $"#{supplierId}",
                buckets.Current, buckets.Days31To60, buckets.Days61To90, buckets.Over90,
                unapplied));
        }

        return [.. rows.OrderByDescending(r => r.Total)];
    }

    /// <summary>
    /// يُطفئ ما خُصّص صراحةً على فاتورته.
    /// </summary>
    /// <remarks>
    /// <b>والتخصيصُ الصريح يسبق الأقدم</b>: منشأةٌ تُسدّد فاتورةَ الشهر وتترك قديمةً
    /// متنازَعاً عليها — <b>وإطفاءُ الأقدم يُظهر النزاعَ مسدَّداً والجاريةَ متأخّرة</b>.
    /// </remarks>
    private static void ApplyExplicit(List<OpenDocument> open, List<PaymentAllocationLine> allocations)
    {
        foreach (var document in open)
        {
            var applied = allocations.Where(a => a.DocumentId == document.DocumentId).Sum(a => a.Amount);
            if (applied <= 0m) continue;
            document.Remaining = Math.Max(0m, document.Remaining - applied);
        }
    }

    /// <summary>ما بقي من المدفوع بعد ما خُصّص صراحةً — ولا يقلّ عن صفر.</summary>
    private static decimal RemainingDebit(decimal debit, List<OpenDocument> open, decimal invoiced)
    {
        var explicitlyApplied = invoiced - open.Sum(d => d.Remaining);
        return Math.Max(0m, debit - explicitlyApplied);
    }

    /// <summary>يُطفئ الأقدمَ فالأقدم — <b>ويُعيد ما فاض عن كلّ الفواتير</b>.</summary>
    private static decimal ApplyOldestFirst(List<OpenDocument> open, decimal debit)
    {
        foreach (var document in open)
        {
            if (debit <= 0m) break;
            var applied = Math.Min(document.Remaining, debit);
            document.Remaining -= applied;
            debit -= applied;
        }

        // ‏**والفائضُ يُعرض ولا يُبتلع**: دفعةٌ مقدَّمةٌ لمورّدٍ أو إشعارُ مدينٍ بأكثر ممّا
        // ‏عليه رصيدٌ **مدين**. <b>وإخفاؤه يجعل مجموعَ التقرير يخالف كشفَ الحساب.</b>
        return debit;
    }

    private static (decimal Current, decimal Days31To60, decimal Days61To90, decimal Over90)
        Bucket(List<OpenDocument> open, DateOnly asOf)
    {
        decimal current = 0m, d31 = 0m, d61 = 0m, over = 0m;

        foreach (var document in open.Where(d => d.Remaining > 0m))
        {
            var age = asOf.DayNumber - document.Date.DayNumber;

            if (age <= 30) current += document.Remaining;
            else if (age <= 60) d31 += document.Remaining;
            else if (age <= 90) d61 += document.Remaining;
            else over += document.Remaining;
        }

        return (Round(current), Round(d31), Round(d61), Round(over));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
